using System.Text.Json;
using System.Text.RegularExpressions;
using Journeys.API.CampaignAgent.Workflow.Steps;
using Journeys.CampaignAgent.Remediation;
using Journeys.Core.Models;
using Journeys.Core.Utility;

namespace Journeys.API.CampaignAgent.Workflow;

/// <summary>
/// Coach-first verification playbook: test account capture, get_account confirmation, SESSION hints.
/// Tools are never hidden — coaching only.
/// </summary>
public static class VerificationCoach
{
    private static readonly Regex TestAccountFallbackRegex = new(
        @"\b(test_[a-zA-Z0-9_]+)\b",
        RegexOptions.Compiled);

    private static readonly string[] AccountTriggerPhrases =
    [
        "test with", "use the", "use ", "user ", "account ", "test account", "for the test",
        "external id", "ext account", "ext id"
    ];

    private static readonly Regex QuotedAccountIdRegex = new(
        @"['""](?<id>[a-zA-Z0-9_][a-zA-Z0-9_-]{1,})['""]",
        RegexOptions.Compiled);

    public static bool Applies(CampaignWorkflowState state) =>
        CampaignWorkflowChecklist.IsBriefCaptured(state)
        && state.CampaignKind == CampaignWorkflowKind.EventDriven
        && state.Phase == CampaignWorkflowPhase.Verification;

    public static void TryCaptureTestAccountFromUserMessage(CampaignWorkflowState state, string message)
    {
        if (HasTestIntent(message))
            state.Artifacts.VerificationUserTestIntentThisTurn = true;

        if (!HasTestIntent(message))
            return;

        var extracted = TryExtractTestAccountId(message);
        if (string.IsNullOrWhiteSpace(extracted))
            return;

        if (string.Equals(state.Artifacts.VerificationTestAccountId, extracted, StringComparison.OrdinalIgnoreCase))
            return;

        state.Artifacts.VerificationTestAccountId = extracted;
        state.Artifacts.VerificationAccountConfirmed = false;
        state.Artifacts.VerificationAccountDigest = null;
    }

    public static string? GetCoachHint(CampaignWorkflowState state)
    {
        if (!Applies(state)
            && !state.Artifacts.VerificationUserTestIntentThisTurn
            && !ShouldApplyDraftFirstCoach(state))
            return null;

        var draftFirstPrefix = ShouldApplyDraftFirstCoach(state)
            && !state.Artifacts.VerificationBlockedNoAllowlist
            && !state.Artifacts.TenantTestAccountsLoadFailed
            ? "Coach: Do NOT tell the user Live promotion is required for process_event. "
              + "Draft is testable with process_event(campaignId) from THREAD CONTEXT. "
            : null;

        var id = state.Artifacts.VerificationTestAccountId;
        var confirmed = state.Artifacts.VerificationAccountConfirmed;
        var lastFailure = state.Artifacts.LastToolRemediationSummary ?? "";
        var creationComplete = CreationSnapshotArtifact.IsCreationComplete(state);
        var allowlist = ReadAllowlist(state);

        if (state.Artifacts.TenantTestAccountsLoadFailed)
        {
            return "Coach: Could not load tenant test-account config — retry or configure "
                   + "campaignTestAccountExtIds in admin.";
        }

        if (state.Artifacts.VerificationBlockedNoAllowlist)
        {
            return "Coach: Draft verification is not configured — no test accounts on tenant. "
                   + "Testing cannot complete until an admin adds campaignTestAccountExtIds.";
        }

        if (!string.IsNullOrWhiteSpace(id) && allowlist.Count > 0 && !IsOnAllowlist(state, id))
        {
            return $"Coach: '{id}' is not on the tenant allowlist. Use one of: {FormatAllowlist(allowlist)}, "
                   + $"or ask admin to add '{id}'.";
        }

        if (allowlist.Count > 0 && string.IsNullOrWhiteSpace(id))
        {
            if (allowlist.Count == 1)
            {
                var sole = allowlist[0];
                return $"Coach: Available test account: {sole}. Call get_account('{sole}') then process_event "
                       + "with campaignId from THREAD CONTEXT.";
            }

            return $"Coach: Available test accounts: {FormatAllowlist(allowlist)}. Which should I use?";
        }

        if (VerificationDebugContext.IsEarlyVerifyAttempt(state)
            && !CreationSnapshotArtifact.IsCreationComplete(state))
        {
            return Finish(VerificationDebugContext.BuildEarlyVerifyPlaybookHint(state), draftFirstPrefix);
        }

        if (VerificationDebugContext.IsActiveDebug(state))
        {
            return Finish(VerificationDebugContext.BuildVerifyPlaybookHint(state), draftFirstPrefix);
        }

        if (ShouldWarnPostVerifyRedundancy(state))
        {
            return Finish(BuildPostVerifySuccessRemediation(state), draftFirstPrefix);
        }

        var navigationCoach = NavigationCoach.GetCoachHint(state);
        if (!string.IsNullOrWhiteSpace(navigationCoach))
            return Finish(navigationCoach, draftFirstPrefix);

        if (!string.IsNullOrWhiteSpace(lastFailure)
            && lastFailure.Contains("draftTestingNotPermitted", StringComparison.OrdinalIgnoreCase))
        {
            var acct = id ?? "the test account";
            return Finish(
                $"Coach: Account '{acct}' is not on the tenant campaign test allowlist (Tenant.campaignTestAccountExtIds). Use an allowlisted test account or ask an admin to add it.",
                draftFirstPrefix);
        }

        if (creationComplete && (Applies(state) || state.Artifacts.VerificationUserTestIntentThisTurn || ShouldApplyDraftFirstCoach(state)))
        {
            var snap = CreationSnapshotArtifact.Read(state);
            var campaignId = snap?.CampaignId ?? "unknown";
            var campaignHint = !string.IsNullOrWhiteSpace(snap?.CampaignId)
                ? $" Pass campaignId={snap!.CampaignId} on process_event for exclusive Draft evaluation."
                : " Pass campaignId from THREAD CONTEXT on process_event for exclusive Draft evaluation.";

            if (!string.IsNullOrWhiteSpace(id) && !confirmed)
            {
                return Finish(
                    $"Coach: Campaign and PATs already created (CreationSnapshot, campaign {campaignId}). "
                    + $"Do not call upsert_point_account_type or upsert_campaign. "
                    + $"Confirm test account via get_account('{id}') then process_event.",
                    draftFirstPrefix);
            }

            if (confirmed)
            {
                return Finish(
                    "Coach: Use get_campaign_assistant_context sampleScaffold for process_event. "
                    + "Do not recreate PATs or campaign. Prefer PointAccountManifest in WORKFLOW ARTIFACTS "
                    + "over empty assistant context manifest. "
                    + "Avoid get_model_attributes_for_rules/get_model rediscovery — use resolvedEventModelIds in SALIENT FACTS."
                    + campaignHint,
                    draftFirstPrefix);
            }

            return Finish(
                "Coach: Campaign and PATs already created (CreationSnapshot). Do not call "
                + "upsert_point_account_type or upsert_campaign. Ask the user which test account to use, "
                + "then get_account before process_event. "
                + "Avoid get_model_attributes_for_rules/get_model unless the user changed catalog data.",
                draftFirstPrefix);
        }

        if (confirmed
            && state.Artifacts.VerificationProcessEventCampaignApplied
            && !state.Artifacts.VerificationProcessEventRulesApplied)
        {
            return NavigationCoach.GetCoachHint(state)
                   ?? "Coach: process_event applied the campaign but no rule sets fired. Fix journey navigation Entry/Transition, then re-test.";
        }

        if (confirmed
            && !string.IsNullOrWhiteSpace(lastFailure)
            && !ToolResultSuccessEvaluator.LooksSuccessful(lastFailure)
            && (lastFailure.Contains("process_event", StringComparison.OrdinalIgnoreCase)
                || lastFailure.Contains("processEvent", StringComparison.OrdinalIgnoreCase)
                || lastFailure.Contains("errors", StringComparison.OrdinalIgnoreCase)
                || lastFailure.Contains("isError", StringComparison.OrdinalIgnoreCase)))
        {
            return "Coach: Verification not complete. Apply _agentRemediation hints. Do not claim production-ready until process_event succeeds with non-empty AppliedRuleSetIds.";
        }

        if (!string.IsNullOrWhiteSpace(id) && !confirmed
            && lastFailure.Contains("Account not found", StringComparison.OrdinalIgnoreCase))
        {
            return $"Coach: Account '{id}' not found. Ask the user for a valid test account. Do not substitute an invented id.";
        }

        if (!string.IsNullOrWhiteSpace(id) && !confirmed)
        {
            return $"Coach: Call get_account('{id}') to confirm the account exists before process_event.";
        }

        if (confirmed)
        {
            var snap = CreationSnapshotArtifact.Read(state);
            var campaignId = snap?.CampaignId;
            var campaignHint = !string.IsNullOrWhiteSpace(campaignId)
                ? $" Pass campaignId={campaignId} on process_event for exclusive Draft evaluation."
                : " Pass campaignId from THREAD CONTEXT on process_event for exclusive Draft evaluation.";
            return "Coach: Use get_campaign_assistant_context sampleScaffold for the process_event fixture; account link field = processingContract.accountLink.symbolPath."
                   + campaignHint;
        }

        if (!string.IsNullOrWhiteSpace(draftFirstPrefix))
            return draftFirstPrefix.TrimEnd();

        return "Coach: Ask the user which test account to use (external id). Do not invent account ids.";
    }

    private static string Finish(string hint, string? draftFirstPrefix) =>
        draftFirstPrefix is null ? hint : draftFirstPrefix + hint;

    internal static bool ShouldApplyDraftFirstCoach(CampaignWorkflowState state)
    {
        if (state.CampaignKind != CampaignWorkflowKind.EventDriven)
            return false;
        var snap = CreationSnapshotArtifact.Read(state);
        if (snap is not { CreationComplete: true })
            return false;
        if (!string.Equals(snap.CampaignStatus, CampaignStatusStrings.Draft, StringComparison.OrdinalIgnoreCase))
            return false;
        if (new VerificationExitCriteria().IsMet(state))
            return false;
        return state.Phase is CampaignWorkflowPhase.CampaignJourney
            or CampaignWorkflowPhase.Verification
            or CampaignWorkflowPhase.Done
            || state.Artifacts.VerificationUserTestIntentThisTurn;
    }

    public static void ApplyGetAccountOutcome(CampaignWorkflowState state, string resultJson, bool success)
    {
        if (!success)
        {
            state.Artifacts.VerificationAccountConfirmed = false;
            return;
        }

        var testId = state.Artifacts.VerificationTestAccountId;
        if (string.IsNullOrWhiteSpace(testId))
            return;

        if (AccountResponseMatchesTestId(resultJson, testId))
        {
            state.Artifacts.VerificationAccountConfirmed = true;
            state.Artifacts.VerificationAccountDigest = Truncate(resultJson, 500);
        }
    }

    public static void ClearVerificationArtifacts(CampaignWorkflowArtifactsDocument artifacts)
    {
        artifacts.VerificationTestAccountId = null;
        artifacts.VerificationAccountConfirmed = false;
        artifacts.VerificationAccountDigest = null;
        artifacts.VerificationRecord = null;
        artifacts.VerificationUserTestIntentThisTurn = false;
        artifacts.VerificationDebugSteeringThisTurn = false;
        artifacts.VerificationProcessEventCampaignApplied = false;
        artifacts.VerificationProcessEventRulesApplied = false;
    }

    public static string BuildAntiRebuildRemediation(CampaignWorkflowState state)
    {
        var snap = CreationSnapshotArtifact.Read(state);
        var id = snap?.CampaignId ?? "unknown";
        var ruleSets = snap?.JourneyRuleSetCount ?? 0;
        return $"Creation already complete (campaign {id}, {ruleSets} rule sets). "
               + "Use existing ids from WORKFLOW ARTIFACTS; only upsert to fix a failed process_event validation.";
    }

    public static bool IsRedundantDiscoveryTool(string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            return false;

        return string.Equals(toolName, "get_model_attributes_for_rules", StringComparison.OrdinalIgnoreCase)
               || string.Equals(toolName, "GetModelAttributesForRules", StringComparison.OrdinalIgnoreCase)
               || string.Equals(toolName, "get_model", StringComparison.OrdinalIgnoreCase)
               || string.Equals(toolName, "GetModel", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsRedundantPostVerifyTool(string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            return false;

        return string.Equals(toolName, "list_example_campaigns", StringComparison.OrdinalIgnoreCase)
               || string.Equals(toolName, "ListExampleCampaigns", StringComparison.OrdinalIgnoreCase)
               || string.Equals(toolName, "get_example_campaign", StringComparison.OrdinalIgnoreCase)
               || string.Equals(toolName, "GetExampleCampaign", StringComparison.OrdinalIgnoreCase)
               || string.Equals(toolName, "get_rules_engine_contract_summary", StringComparison.OrdinalIgnoreCase)
               || string.Equals(toolName, "GetRulesEngineContractSummary", StringComparison.OrdinalIgnoreCase)
               || string.Equals(toolName, "get_journey_from_campaign", StringComparison.OrdinalIgnoreCase)
               || string.Equals(toolName, "GetJourneyFromCampaign", StringComparison.OrdinalIgnoreCase)
               || string.Equals(toolName, "get_point_account_type", StringComparison.OrdinalIgnoreCase)
               || string.Equals(toolName, "GetPointAccountType", StringComparison.OrdinalIgnoreCase)
               || string.Equals(toolName, "list_point_account_types", StringComparison.OrdinalIgnoreCase)
               || string.Equals(toolName, "ListPointAccountTypes", StringComparison.OrdinalIgnoreCase)
               || string.Equals(toolName, "upsert_point_account_type", StringComparison.OrdinalIgnoreCase)
               || string.Equals(toolName, "UpsertPointAccountType", StringComparison.OrdinalIgnoreCase)
               || string.Equals(toolName, "upsert_campaign", StringComparison.OrdinalIgnoreCase)
               || string.Equals(toolName, "UpsertCampaign", StringComparison.OrdinalIgnoreCase)
               || string.Equals(toolName, "validate_campaign", StringComparison.OrdinalIgnoreCase)
               || string.Equals(toolName, "ValidateCampaign", StringComparison.OrdinalIgnoreCase);
    }

    public static bool ShouldWarnPostVerifyRedundancy(CampaignWorkflowState state)
    {
        if (!CreationSnapshotArtifact.IsCreationComplete(state))
            return false;

        if (state.Phase != CampaignWorkflowPhase.Verification
            && !state.Artifacts.VerificationUserTestIntentThisTurn)
            return false;

        return VerificationDebugContext.HasSuccessfulPointDeposit(state);
    }

    public static string BuildPostVerifySuccessRemediation(CampaignWorkflowState state)
    {
        var snap = CreationSnapshotArtifact.Read(state);
        var campaignId = snap?.CampaignId ?? "from CreationSnapshot";
        var multiTierHint = snap is { JourneyRuleSetCount: >= 3 }
            ? " Bronze verified — optionally process_event Silver/Gold with tier-appropriate spend; no upsert unless validate errors."
            : string.Empty;
        return "Coach: Verification succeeded — points were deposited." + multiTierHint
               + " Do not call list_example_campaigns, get_example_campaign, get_rules_engine_contract_summary, "
               + "get_journey_from_campaign, upsert_point_account_type, or upsert_campaign unless the user asks to change the journey. "
               + $"Summarize the process_event result for campaignId={campaignId} and ask whether to promote or adjust.";
    }

    public static string BuildAntiRediscoveryRemediation(CampaignWorkflowState state) =>
        "Event models and attributes were resolved during creation. "
        + "Do not re-call get_model_attributes_for_rules or get_model unless process_event validation requires a specific symbol path fix. "
        + "Use WORKFLOW ARTIFACTS resolvedEventModelIds and get_campaign_assistant_context sampleScaffold.";

    public static bool ShouldWarnRedundantDiscovery(CampaignWorkflowState state)
    {
        if (!CreationSnapshotArtifact.IsCreationComplete(state))
            return false;

        if (VerificationDebugContext.ShouldSuppressAntiRediscovery(state))
            return false;

        return state.Phase is CampaignWorkflowPhase.Verification or CampaignWorkflowPhase.Done;
    }

    private static bool HasTestIntent(string message) =>
        WorkflowUserPhraseCatalog.ContainsAny(message, WorkflowUserPhraseCatalog.JourneyVerificationIntentPhrases)
        || WorkflowUserPhraseCatalog.ContainsAny(message, WorkflowUserPhraseCatalog.FocusVerificationPhrases);

    public static string? TryExtractTestAccountId(string message)
    {
        var quoted = QuotedAccountIdRegex.Match(message);
        if (quoted.Success)
            return quoted.Groups["id"].Value;

        var lower = message.ToLowerInvariant();
        foreach (var trigger in AccountTriggerPhrases.OrderByDescending(t => t.Length))
        {
            var idx = lower.IndexOf(trigger, StringComparison.Ordinal);
            if (idx < 0)
                continue;

            var remainder = message[(idx + trigger.Length)..].Trim();
            var pattern = trigger is "external id" or "ext account" or "ext id"
                ? @"^(the\s+)?(?<id>[a-zA-Z0-9_][a-zA-Z0-9_-]{1,})"
                : @"^(the\s+)?(?<id>[a-zA-Z][a-zA-Z0-9_]{2,})";
            var match = Regex.Match(remainder, pattern);
            if (match.Success)
                return match.Groups["id"].Value;
        }

        var fallback = TestAccountFallbackRegex.Match(message);
        return fallback.Success ? fallback.Groups[1].Value : null;
    }

    internal static IReadOnlyList<string> ReadAllowlist(CampaignWorkflowState state)
    {
        var json = state.Artifacts.TenantTestAccountAllowlistJson;
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    internal static bool IsOnAllowlist(CampaignWorkflowState state, string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        return ReadAllowlist(state).Any(a => string.Equals(a, id, StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatAllowlist(IReadOnlyList<string> allowlist) =>
        string.Join(", ", allowlist);

    private static bool AccountResponseMatchesTestId(string resultJson, string testId)
    {
        if (resultJson.Contains(testId, StringComparison.OrdinalIgnoreCase))
            return true;

        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;
            if (TryGetStringProperty(root, "extAccountId", out var ext) && Matches(ext, testId))
                return true;
            if (TryGetStringProperty(root, "ExtAccountId", out ext) && Matches(ext, testId))
                return true;
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static bool TryGetStringProperty(JsonElement root, string name, out string? value)
    {
        value = null;
        if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String)
            return false;
        value = el.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool Matches(string? a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static string Truncate(string json, int maxChars) =>
        json.Length <= maxChars ? json : json[..maxChars] + "...";
}
