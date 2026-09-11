using System.Text.Json;
using System.Text.RegularExpressions;
using Journeys.CampaignAgent.Remediation;
using Journeys.Core.Models;
using Journeys.Core.Utility;

namespace Journeys.API.CampaignAgent.Workflow;

/// <summary>
/// Tracks validate_campaign outcomes and emits SESSION coaching hints.
/// </summary>
public static class CampaignValidationCoach
{
    private static readonly Regex ViolationCodeRegex = new(
        @"\[violation=([^\]]+)\]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static void ApplyValidateOutcome(
        CampaignWorkflowState state,
        string toolName,
        string resultJson,
        bool toolLooksSuccessful)
    {
        if (!IsValidateTool(toolName))
            return;

        var normalized = ToolResultJsonNormalizer.Unwrap(resultJson) ?? resultJson;

        if (!toolLooksSuccessful)
        {
            if (HasValidationErrors(normalized))
                ApplyValidateFailure(state, toolName, normalized);
            return;
        }

        if (!TryParseOutcome(normalized, out var isValid, out var errorCount, out var warningCount, out var topWarnings, out var fingerprint))
        {
            if (HasValidationErrors(normalized))
                ApplyValidateFailure(state, toolName, normalized);
            return;
        }

        var artifact = new CampaignValidationArtifact
        {
            ValidatedAtUtc = DateTime.UtcNow.ToString("o"),
            PayloadFingerprint = fingerprint,
            IsValid = isValid,
            ErrorCount = errorCount,
            WarningCount = warningCount,
            TopWarningCodes = topWarnings,
            Phase = state.Phase.ToString(),
            UpsertFailedSinceValidate = false
        };

        Write(state, artifact);

        RecordValidateSummaryCounts(state, normalized, isValid);

        if (!isValid)
        {
            artifact.LastErrorCodes = ExtractTopErrorCodes(normalized);
            Write(state, artifact);
            RecordFailureCycle(state, toolName, normalized);
            state.Artifacts.LastToolRemediationSummary =
                BuildValidationRemediation(normalized)
                ?? "validate_campaign reported hard errors — fix validation.errors and follow nextSteps before upsert_campaign.";
            CaptureViolationCode(state, normalized);
            state.Artifacts.LastValidateFailedBeforeUpsert = true;
            return;
        }

        state.Artifacts.LastValidateFailedBeforeUpsert = false;

        if (state.Artifacts.LastValidateHadJourneyIntent == true
            && (state.Artifacts.LastValidateRuleSetCount ?? 0) == 0)
        {
            state.Artifacts.LastToolRemediationSummary =
                CampaignAgentGuidanceText.ValidatePassedRuleSetCountZero;
            return;
        }

        if ((state.Artifacts.LastValidateRuleSetCount ?? 0) > 0
            || GetJourneyRuleSetCount(state) > 0)
        {
            ClearValidationStall(state);
        }

        if (isValid && warningCount > 0)
        {
            state.Artifacts.LastToolRemediationSummary =
                $"Validation passed with {warningCount} advisory warning(s). "
                + "Address top warnings, re-validate, then upsert unless the user explicitly asked to save now.";
        }
    }

    private static void ApplyValidateFailure(CampaignWorkflowState state, string toolName, string resultJson)
    {
        var codes = ExtractTopErrorCodes(resultJson);
        var artifact = Read(state) ?? new CampaignValidationArtifact();
        artifact.ValidatedAtUtc = DateTime.UtcNow.ToString("o");
        artifact.IsValid = false;
        artifact.ErrorCount = Math.Max(codes.Count, 1);
        artifact.LastErrorCodes = codes;
        artifact.Phase = state.Phase.ToString();
        artifact.UpsertFailedSinceValidate = false;
        Write(state, artifact);

        state.Artifacts.LastToolRemediationSummary =
            BuildValidationRemediation(resultJson)
            ?? "validate_campaign reported hard errors — fix validation.errors and follow nextSteps before upsert_campaign.";

        CaptureViolationCode(state, resultJson);
        state.Artifacts.LastValidateFailedBeforeUpsert = true;
        RecordFailureCycle(state, toolName, resultJson);
    }

    public static void ApplyUpsertFailure(CampaignWorkflowState state, string resultJson)
    {
        MarkUpsertFailedSinceValidate(state);

        state.Artifacts.LastToolRemediationSummary =
            BuildValidationRemediation(resultJson)
            ?? "Upsert failed — call validate_campaign for all errors and nextSteps before retry. "
               + TruncateJson(resultJson, 400);

        RecordFailureCycle(state, "upsert_campaign", resultJson);
    }

    public static void RecordFailureCycle(CampaignWorkflowState state, string toolName, string resultJson)
    {
        if (!CampaignJourneyDeliveryGuard.Enabled)
            return;
        if (!IsValidateOrUpsert(toolName))
            return;
        if (!IsValidationCoachingPhase(state.Phase) && state.Phase != CampaignWorkflowPhase.Verification)
            return;
        if (GetJourneyRuleSetCount(state) > 0)
            return;

        state.Artifacts.ValidationStallCycleCount++;

        var artifact = Read(state) ?? new CampaignValidationArtifact();
        var codes = ExtractTopErrorCodes(resultJson);
        if (codes.Count > 0)
            artifact.LastErrorCodes = codes;
        Write(state, artifact);

        var remediation = BuildValidationRemediation(resultJson);
        if (!string.IsNullOrWhiteSpace(remediation))
            state.Artifacts.LastToolRemediationSummary = remediation;

        if (state.Artifacts.ValidationStallCycleCount >= 2)
            TripValidationStall(state);

        if (IsUpsertWithoutRevalidate(state, toolName))
        {
            state.Artifacts.LastValidateViolationCode = "REVALIDATE_BEFORE_UPSERT";
            var revalidateMsg =
                "Re-validate before upsert — call validate_campaign and fix all errors until IsValid before upsert_campaign.";
            state.Artifacts.LastToolRemediationSummary = string.IsNullOrWhiteSpace(state.Artifacts.LastToolRemediationSummary)
                ? revalidateMsg
                : state.Artifacts.LastToolRemediationSummary + " " + revalidateMsg;
        }
    }

    private static bool IsUpsertWithoutRevalidate(CampaignWorkflowState state, string toolName) =>
        state.Artifacts.LastValidateFailedBeforeUpsert
        && (string.Equals(toolName, "upsert_campaign", StringComparison.OrdinalIgnoreCase)
            || string.Equals(toolName, "UpsertCampaign", StringComparison.OrdinalIgnoreCase));

    public static void ClearValidationStall(CampaignWorkflowState state)
    {
        state.Artifacts.ValidationStalled = false;
        state.Artifacts.ValidationStallCycleCount = 0;
        state.Artifacts.ValidationStallTrippedAtUtc = null;
    }

    public static void ClearLastValidateDeliveryArtifacts(CampaignWorkflowState state)
    {
        state.Artifacts.LastValidateRuleSetCount = null;
        state.Artifacts.LastValidateIsValid = null;
        state.Artifacts.LastValidateHadJourneyIntent = null;
        state.Artifacts.LastValidateViolationCode = null;
        state.Artifacts.LastValidateFailedBeforeUpsert = false;
    }

    private static void CaptureViolationCode(CampaignWorkflowState state, string resultJson)
    {
        var code = ValidateLoopCoach.MapViolationCode(resultJson);
        if (string.IsNullOrWhiteSpace(code))
        {
            var codes = ExtractTopErrorCodes(resultJson);
            code = codes.Count > 0 ? codes[0] : null;
        }

        state.Artifacts.LastValidateViolationCode = code;
    }

    private static void RecordValidateSummaryCounts(
        CampaignWorkflowState state,
        string resultJson,
        bool isValid)
    {
        if (!TryReadSummaryCounts(resultJson, out var ruleSetCount, out var hasJourney))
            return;

        state.Artifacts.LastValidateRuleSetCount = ruleSetCount;
        state.Artifacts.LastValidateIsValid = isValid;
        state.Artifacts.LastValidateHadJourneyIntent = hasJourney;
    }

    private static bool TryReadSummaryCounts(
        string resultJson,
        out int ruleSetCount,
        out bool hasJourney)
    {
        ruleSetCount = 0;
        hasJourney = false;

        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            if (!root.TryGetProperty("summary", out var summary) || summary.ValueKind != JsonValueKind.Object)
                return false;

            if (summary.TryGetProperty("ruleSetCount", out var rsc) && rsc.TryGetInt32(out var n))
                ruleSetCount = n;
            else if (summary.TryGetProperty("RuleSetCount", out rsc) && rsc.TryGetInt32(out n))
                ruleSetCount = n;

            if (summary.TryGetProperty("hasJourney", out var hj)
                && hj.ValueKind is JsonValueKind.True or JsonValueKind.False)
                hasJourney = hj.GetBoolean();
            else if (summary.TryGetProperty("HasJourney", out hj)
                     && hj.ValueKind is JsonValueKind.True or JsonValueKind.False)
                hasJourney = hj.GetBoolean();

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string? GetStallCoachHint(CampaignWorkflowState state)
    {
        if (!state.Artifacts.ValidationStalled)
            return null;

        var artifact = Read(state);
        var codes = artifact?.LastErrorCodes.Count > 0
            ? artifact!.LastErrorCodes
            : [];
        var codeList = codes.Count > 0
            ? string.Join(", ", codes.Take(3))
            : "see last validate/upsert errors";
        var remediation = BuildRemediationFromCodes(codes);

        return "Coach: Host paused autonomous retries after "
               + state.Artifacts.ValidationStallCycleCount
               + " failed validate/upsert cycles with journey.ruleSetCount still 0. "
               + "Top errors: " + codeList + ". "
               + (remediation ?? CampaignAgentGuidanceText.RuleSetWrapperShortHint)
               + "Send a new message describing tier structure or fixes to resume.";
    }

    public static void MarkUpsertFailedSinceValidate(CampaignWorkflowState state)
    {
        var artifact = Read(state) ?? new CampaignValidationArtifact();
        artifact.IsValid = false;
        artifact.UpsertFailedSinceValidate = true;
        artifact.ErrorCount = Math.Max(artifact.ErrorCount, 1);
        Write(state, artifact);
    }

    public static string? GetCoachHint(CampaignWorkflowState state)
    {
        if (!IsValidationCoachingPhase(state.Phase))
            return null;

        var artifact = Read(state);

        if (artifact?.UpsertFailedSinceValidate == true)
        {
            return "Coach: Upsert failed — call validate_campaign for all errors and nextSteps before retry.";
        }

        if (artifact != null && !artifact.IsValid && artifact.ErrorCount > 0)
        {
            var remediation = BuildRemediationFromCodes(artifact.LastErrorCodes);
            return remediation is not null
                ? "Coach: " + remediation
                : "Coach: Fix validation.errors from last validate; follow nextSteps.";
        }

        if (artifact is { IsValid: true, WarningCount: > 0 })
        {
            return $"Coach: Validation passed with {artifact.WarningCount} warning(s) — address before upsert unless the user asked to save now.";
        }

        if (artifact == null || string.IsNullOrWhiteSpace(artifact.ValidatedAtUtc))
        {
            return "Coach: Call validate_campaign before upsert_campaign with the full intended campaign JSON.";
        }

        return null;
    }

    public static CampaignValidationArtifact? Read(CampaignWorkflowState state)
    {
        var json = state.Artifacts.CampaignValidationSummary;
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<CampaignValidationArtifact>(json, JsonOpts);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string? FormatPromptLine(CampaignWorkflowState state)
    {
        var artifact = Read(state);
        if (artifact == null || string.IsNullOrWhiteSpace(artifact.ValidatedAtUtc))
            return null;

        var at = artifact.ValidatedAtUtc.Length > 16
            ? artifact.ValidatedAtUtc[..16] + "Z"
            : artifact.ValidatedAtUtc;

        var fp = string.IsNullOrWhiteSpace(artifact.PayloadFingerprint) ? "—" : artifact.PayloadFingerprint;
        var phase = string.IsNullOrWhiteSpace(artifact.Phase) ? state.Phase.ToString() : artifact.Phase;

        return $"Validation: isValid={artifact.IsValid.ToString().ToLowerInvariant()}, "
               + $"warnings={artifact.WarningCount}, fingerprint={fp} ({phase}, {at})";
    }

    private static void TripValidationStall(CampaignWorkflowState state)
    {
        state.Artifacts.ValidationStalled = true;
        state.Artifacts.ValidationStallTrippedAtUtc = DateTime.UtcNow.ToString("o");
        var hint = GetStallCoachHint(state);
        if (!string.IsNullOrWhiteSpace(hint))
            state.Artifacts.LastToolRemediationSummary = hint.Replace("Coach: ", string.Empty, StringComparison.Ordinal);
    }

    private static int GetJourneyRuleSetCount(CampaignWorkflowState state) =>
        CreationSnapshotArtifact.Read(state)?.JourneyRuleSetCount ?? 0;

    private static bool IsValidateOrUpsert(string toolName) =>
        IsValidateTool(toolName)
        || string.Equals(toolName, "upsert_campaign", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "UpsertCampaign", StringComparison.OrdinalIgnoreCase);

    private static List<string> ExtractTopErrorCodes(string resultJson)
    {
        var codes = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        resultJson = ToolResultJsonNormalizer.Unwrap(resultJson) ?? resultJson;

        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("validation", out var validation)
                && validation.ValueKind == JsonValueKind.Object
                && validation.TryGetProperty("errors", out var errors)
                && errors.ValueKind == JsonValueKind.Array)
            {
                foreach (var error in errors.EnumerateArray())
                {
                    if (codes.Count >= 3)
                        break;
                    if (error.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String)
                    {
                        TryAddCode(codes, seen, code.GetString());
                        continue;
                    }
                    if (error.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                        TryAddViolationCodesFromText(codes, seen, message.GetString());
                }
            }

            if (codes.Count < 3
                && root.TryGetProperty("errors", out var errorsObj)
                && errorsObj.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in errorsObj.EnumerateObject())
                {
                    if (codes.Count >= 3)
                        break;
                    if (prop.Value.ValueKind == JsonValueKind.String)
                        TryAddViolationCodesFromText(codes, seen, prop.Value.GetString());
                }
            }
        }
        catch (JsonException)
        {
            // ignore malformed tool JSON
        }

        return codes;
    }

    private static string? BuildValidationRemediation(string resultJson)
    {
        var codes = ExtractTopErrorCodes(resultJson);
        if (codes.Count > 0)
            return BuildRemediationFromCodes(codes);

        var unwrapped = ToolResultJsonNormalizer.Unwrap(resultJson) ?? resultJson;
        if (HasNavigationErrors(unwrapped))
            return BuildNavigationRemediation();

        return null;
    }

    private static string? BuildRemediationFromCodes(IReadOnlyList<string> codes)
    {
        if (codes.Count == 0)
            return null;

        if (codes.Any(c => c.Equals("JOURNEY_CHILDREN_NOT_NODES", StringComparison.OrdinalIgnoreCase)
                           || c.StartsWith("JOURNEY_NAV_", StringComparison.OrdinalIgnoreCase)))
        {
            return "Fix journey tree shape from JourneyAuthoringTemplate — children[] not nodes[]; root Entry + tier Transition. "
                   + "Re-validate before upsert — do not upsert until IsValid.";
        }

        if (codes.Any(c => c.Equals("TIER_A_OUTCOME_PAT_ALIAS_MISUSED", StringComparison.OrdinalIgnoreCase)
                           || c.Equals("TIER_A_DEPOSIT_MISSING_AFFECTED_PAT", StringComparison.OrdinalIgnoreCase)
                           || c.Equals("TIER_A_SPEND_MISSING_AFFECTED_PAT", StringComparison.OrdinalIgnoreCase)
                           || c.Equals("TIER_A_EXPIRE_MISSING_AFFECTED_PAT", StringComparison.OrdinalIgnoreCase)))
        {
            return CampaignAgentGuidanceText.JsonCasingAffectedPatHint;
        }

        if (codes.Count(c => c.Contains("MISSING_LEFT_PROVIDER", StringComparison.Ordinal)
                          || c.Contains("MISSING_RIGHT_PROVIDER", StringComparison.Ordinal)
                          || c.Contains("MISSING_EVALUATOR", StringComparison.Ordinal)) >= 2)
        {
            return CampaignAgentGuidanceText.JsonCasingPascalCaseRuleProvidersHint
                   + " Fix cited navConstraint/ruleJsonElement providers, then re-validate.";
        }

        if (codes.Any(c => c.Equals("TIER_A_MISSING_TYPE_DISCRIMINATOR", StringComparison.OrdinalIgnoreCase)))
        {
            return "Add $type on cited provider/navigation objects (e.g. SimpleNavigationCriteria, PointBalanceProvider, PathValueProvider) "
                   + "using GetRulesEngineContractSummary typeDiscriminatorCatalog, then re-validate.";
        }

        if (codes.Any(c => c.StartsWith("TIER_A_", StringComparison.OrdinalIgnoreCase)))
        {
            return "Fix cited Tier A fields from validation.errors (ruleSet, nodeId, rulePath, kind, field), "
                   + "then re-validate before upsert_campaign.";
        }

        return null;
    }

    private static string BuildNavigationRemediation() =>
        CampaignAgentGuidanceText.ValidateNavigationRemediation;

    private static bool HasNavigationErrors(string json) =>
        json.Contains("journey.navigation.", StringComparison.OrdinalIgnoreCase)
        || json.Contains("JOURNEY_NAV_", StringComparison.OrdinalIgnoreCase);

    private static void TryAddViolationCodesFromText(List<string> codes, HashSet<string> seen, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        foreach (Match match in ViolationCodeRegex.Matches(text))
        {
            if (codes.Count >= 3)
                break;
            TryAddCode(codes, seen, match.Groups[1].Value);
        }
    }

    private static void TryAddCode(List<string> codes, HashSet<string> seen, string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || !seen.Add(code))
            return;
        codes.Add(code);
    }

    private static void Write(CampaignWorkflowState state, CampaignValidationArtifact artifact) =>
        state.Artifacts.CampaignValidationSummary = JsonSerializer.Serialize(artifact, JsonOpts);

    private static bool TryParseOutcome(
        string resultJson,
        out bool isValid,
        out int errorCount,
        out int warningCount,
        out List<string> topWarnings,
        out string? fingerprint)
    {
        isValid = false;
        errorCount = 0;
        warningCount = 0;
        topWarnings = new List<string>();
        fingerprint = null;

        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            if (root.TryGetProperty("validateAck", out var ack) && ack.ValueKind == JsonValueKind.True)
            {
                if (root.TryGetProperty("isValid", out var validEl)
                    && validEl.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    isValid = validEl.GetBoolean();

                if (root.TryGetProperty("payloadFingerprint", out var fp) && fp.ValueKind == JsonValueKind.String)
                    fingerprint = fp.GetString();

                if (root.TryGetProperty("summary", out var summary) && summary.ValueKind == JsonValueKind.Object)
                {
                    if (summary.TryGetProperty("errorCount", out var ec))
                        errorCount = ec.GetInt32();
                    if (summary.TryGetProperty("warningCount", out var wc))
                        warningCount = wc.GetInt32();
                }

                if (root.TryGetProperty("topWarnings", out var tw) && tw.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in tw.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                            topWarnings.Add(item.GetString() ?? "");
                    }
                }

                return true;
            }

            if (!root.TryGetProperty("isValid", out var isValidEl))
                return false;

            isValid = isValidEl.ValueKind == JsonValueKind.True && isValidEl.GetBoolean();

            if (root.TryGetProperty("summary", out var fullSummary) && fullSummary.ValueKind == JsonValueKind.Object)
            {
                if (fullSummary.TryGetProperty("errorCount", out var ec))
                    errorCount = ec.GetInt32();
                if (fullSummary.TryGetProperty("warningCount", out var wc))
                    warningCount = wc.GetInt32();
            }

            if (root.TryGetProperty("validation", out var validation) && validation.ValueKind == JsonValueKind.Object)
            {
                if (validation.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
                {
                    errorCount = Math.Max(errorCount, errors.GetArrayLength());
                }

                if (validation.TryGetProperty("warnings", out var warnings) && warnings.ValueKind == JsonValueKind.Array)
                {
                    warningCount = Math.Max(warningCount, warnings.GetArrayLength());
                    foreach (var warning in warnings.EnumerateArray())
                    {
                        if (topWarnings.Count >= 3)
                            break;
                        if (warning.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String)
                            topWarnings.Add(code.GetString() ?? "");
                    }
                }
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsValidationCoachingPhase(CampaignWorkflowPhase phase) =>
        phase is CampaignWorkflowPhase.CampaignSetup
            or CampaignWorkflowPhase.CampaignJourney
            or CampaignWorkflowPhase.Verification;

    private static bool IsValidateTool(string toolName) =>
        string.Equals(toolName, "ValidateCampaign", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "validate_campaign", StringComparison.OrdinalIgnoreCase);

    private static bool HasValidationErrors(string resultJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            if (root.TryGetProperty("isValid", out var isValid) && isValid.ValueKind == JsonValueKind.False)
                return true;

            if (root.TryGetProperty("errors", out var errors)
                && errors.ValueKind == JsonValueKind.Object
                && errors.EnumerateObject().Any())
                return true;

            if (root.TryGetProperty("validation", out var validation)
                && validation.ValueKind == JsonValueKind.Object
                && validation.TryGetProperty("errors", out var validationErrors)
                && validationErrors.ValueKind == JsonValueKind.Array
                && validationErrors.GetArrayLength() > 0)
                return true;
        }
        catch (JsonException)
        {
            // ignore malformed tool JSON
        }

        return false;
    }

    private static string TruncateJson(string json, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(json))
            return string.Empty;

        var text = json.Trim();
        return text.Length <= maxChars ? text : text[..maxChars] + "...";
    }
}
