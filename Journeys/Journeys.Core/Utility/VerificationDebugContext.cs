using System.Text.Json;
using Journeys.CampaignAgent.Remediation;
using Journeys.Core.Models;
using Journeys.Core.Workflow;

namespace Journeys.Core.Utility;

/// <summary>
/// Policy for post-creation verification debug — when to suppress anti-rediscovery and prefer rule-path fixes.
/// </summary>
public static class VerificationDebugContext
{
    private static readonly string[] RulePathSteeringPhrases =
    [
        "price", "qty", "quantity", "path", "symbol", "property", "rule", "numericproperty",
        "valueprovider", "aggregate", "points per", "don't change the model", "do not change the model",
        "don't change model", "use what you have", "everything needed", "what is the problem"
    ];

    private static readonly string[] CatalogReloadPhrases =
    [
        "reload model", "refresh catalog", "re-fetch model", "refetch model", "fetch model again",
        "reload the model", "refresh the model", "updated the catalog", "changed the model"
    ];

    public static void TryCaptureSteeringFromUserMessage(CampaignWorkflowState state, string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (LooksLikeRulePathSteering(message) || LooksLikeKeepModelIntent(message))
            state.Artifacts.VerificationDebugSteeringThisTurn = true;
    }

    public static bool IsEarlyVerifyAttempt(CampaignWorkflowState state)
    {
        if (WorkflowSkillRegistry.ResolveBuildSubStep(state) != CampaignBuildSubStep.JourneyRequired)
            return false;

        return HasFailedProcessEvent(state) || HasProcessEventPayloadShapeFailure(state);
    }

    public static bool IsActiveDebug(CampaignWorkflowState state)
    {
        if (IsEarlyVerifyAttempt(state)
            && (HasFailedProcessEvent(state) || HasProcessEventPayloadShapeFailure(state)))
            return true;

        return IsPostCreationActiveDebug(state);
    }

    public static bool HasSuccessfulPointDeposit(CampaignWorkflowState state)
    {
        var summary = TryParseVerificationRecord(state);
        return summary is { RulesApplied: true, TotalPointsDeposited: > 0 };
    }

    public static bool HasZeroPointAwardFailure(CampaignWorkflowState state)
    {
        var summary = TryParseVerificationRecord(state);
        return summary is { RulesApplied: true, TotalPointsDeposited: <= 0 }
               && ToolResultSuccessEvaluator.LooksSuccessful(state.Artifacts.VerificationRecord);
    }

    public static bool HasProcessEventPayloadShapeFailure(CampaignWorkflowState state)
    {
        if (HasFailedProcessEvent(state))
            return LooksLikePayloadShapeError(state.Artifacts.VerificationRecord);

        var summary = TryParseVerificationRecord(state);
        return summary?.HasPayloadShapeError == true;
    }

    public static bool ShouldSuppressAntiRediscovery(CampaignWorkflowState state, string? userMessageThisTurn = null)
    {
        if (!CreationSnapshotArtifact.IsCreationComplete(state))
            return false;

        if (!IsPostCreationActiveDebug(state))
            return false;

        var message = userMessageThisTurn;
        if (!string.IsNullOrWhiteSpace(message) && LooksLikeCatalogReloadIntent(message))
            return false;

        if (state.Artifacts.VerificationDebugSteeringThisTurn)
            return true;

        return HasFailedProcessEvent(state)
               || state.Artifacts.VerificationProcessEventCampaignApplied && !state.Artifacts.VerificationProcessEventRulesApplied;
    }

    public static string BuildEarlyVerifyPlaybookHint(CampaignWorkflowState state) =>
        BuildDepositPointsOutcomeSection(state)
        + "Fix journey outcomes via upsert_campaign — do not save_model or rebuild get_model during journey authoring verify. ";

    public static string BuildVerifyPlaybookHint(CampaignWorkflowState state)
    {
        var snap = CreationSnapshotArtifact.Read(state);
        var campaignId = snap?.CampaignId ?? "from THREAD CONTEXT";
        var hint = "Coach: Fix rule paths in upsert_campaign (NumericPropertyRule / PathValueProvider / ValueProvider) "
                   + "using get_campaign_assistant_context sampleScaffold and resolvedEventModelIds in SALIENT FACTS. "
                   + $"Use campaignId={campaignId} from CreationSnapshot or the latest upsert_campaign digest on every process_event — "
                   + "do not upsert_campaign unless journey rules or outcomes actually change. "
                   + BuildDepositPointsOutcomeSection(state)
                   + $"Re-test with process_event(campaignId={campaignId}). "
                   + "Do not save_model or rebuild get_model unless the user explicitly changed catalog symbols.";
        if (!string.IsNullOrWhiteSpace(snap?.CampaignExternalId))
            hint += $" Use entity Id {campaignId} for process_event and preview_tier_move — external ref {snap.CampaignExternalId} is informational only.";
        return hint;
    }

    private static bool IsPostCreationActiveDebug(CampaignWorkflowState state)
    {
        if (!CreationSnapshotArtifact.IsCreationComplete(state))
            return false;

        if (!IsVerifyPhaseOrIntent(state))
            return false;

        if (HasFailedProcessEvent(state))
            return true;

        if (state.Artifacts.VerificationDebugSteeringThisTurn)
            return true;

        if (state.Artifacts.VerificationProcessEventCampaignApplied
            && !state.Artifacts.VerificationProcessEventRulesApplied)
            return true;

        return HasZeroPointAwardFailure(state) || HasProcessEventPayloadShapeFailure(state);
    }

    internal static ProcessEventSummary? TryParseVerificationRecord(CampaignWorkflowState state)
    {
        var record = state.Artifacts.VerificationRecord;
        if (string.IsNullOrWhiteSpace(record))
            return null;

        record = ToolResultJsonNormalizer.Unwrap(record) ?? record;
        try
        {
            using var doc = JsonDocument.Parse(record);
            return ParseProcessEventRoot(doc.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static string BuildDepositPointsOutcomeSection(CampaignWorkflowState state)
    {
        var baseGuidance = "For DepositPointsOutcome on order totals use PointsPerDollar + PathValueProvider on event.ordertotal "
                           + "(or the attribute symbol from resolvedEventModelIds). "
                           + "Do not use EarnRateMultiplier, AggregateValueProvider on items/price, or SimpleCalculationProvider on deposit outcomes. "
                           + "List fields in process_event payloads must be arrays (e.g. discounts: [] not 0). ";

        if (HasProcessEventPayloadShapeFailure(state))
            return baseGuidance + "Fix payload cast/type errors before changing journey outcomes. ";

        if (HasZeroPointAwardFailure(state))
            return baseGuidance + "Rules fired but awarded 0 points — fix DepositPointsOutcome ValueProvider/PointsPerDollar, not rule entry paths. ";

        return baseGuidance;
    }

    public static bool LooksLikeCatalogReloadIntent(string message) =>
        WorkflowUserPhraseCatalog.ContainsAny(message, CatalogReloadPhrases);

    public static bool LooksLikeRulePathSteering(string message) =>
        WorkflowUserPhraseCatalog.ContainsAny(message, RulePathSteeringPhrases);

    public static bool LooksLikeKeepModelIntent(string message)
    {
        var lower = message.ToLowerInvariant();
        return lower.Contains("don't change the model", StringComparison.Ordinal)
               || lower.Contains("do not change the model", StringComparison.Ordinal)
               || lower.Contains("don't change model", StringComparison.Ordinal)
               || lower.Contains("use what you have", StringComparison.Ordinal)
               || lower.Contains("everything needed", StringComparison.Ordinal);
    }

    private static bool IsVerifyPhaseOrIntent(CampaignWorkflowState state) =>
        state.Phase is CampaignWorkflowPhase.Verification or CampaignWorkflowPhase.Done
        || state.Artifacts.VerificationUserTestIntentThisTurn;

    private static bool HasFailedProcessEvent(CampaignWorkflowState state)
    {
        var record = state.Artifacts.VerificationRecord;
        if (string.IsNullOrWhiteSpace(record))
            return false;

        return !ToolResultSuccessEvaluator.LooksSuccessful(record);
    }

    private static bool LooksLikePayloadShapeError(string? record)
    {
        if (string.IsNullOrWhiteSpace(record))
            return false;

        return record.Contains("InvalidCastException", StringComparison.OrdinalIgnoreCase)
               || record.Contains("Unsupported type", StringComparison.OrdinalIgnoreCase)
               || record.Contains("Expected type 'List'", StringComparison.OrdinalIgnoreCase)
               || record.Contains("NotSupportedException", StringComparison.OrdinalIgnoreCase)
               || record.Contains("SimpleCalculation only supports", StringComparison.OrdinalIgnoreCase);
    }

    private static ProcessEventSummary ParseProcessEventRoot(JsonElement root)
    {
        var rulesApplied = ReadStringArray(root, "AppliedRuleSetIds", "appliedRuleSetIds").Count > 0;
        var points = SumPointsDeposited(root);
        var shapeError = LooksLikePayloadShapeError(root.GetRawText());
        if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            shapeError = true;

        return new ProcessEventSummary(rulesApplied, points, shapeError);
    }

    private static decimal SumPointsDeposited(JsonElement root)
    {
        if (!root.TryGetProperty("OutcomeStates", out var states)
            && !root.TryGetProperty("outcomeStates", out states))
            return 0;

        if (states.ValueKind != JsonValueKind.Array)
            return 0;

        decimal total = 0;
        foreach (var state in states.EnumerateArray())
        {
            if (state.TryGetProperty("PointsDeposited", out var pd) && pd.ValueKind == JsonValueKind.Number)
                total += pd.GetDecimal();
            else if (state.TryGetProperty("pointsDeposited", out pd) && pd.ValueKind == JsonValueKind.Number)
                total += pd.GetDecimal();
        }

        return total;
    }

    private static List<string> ReadStringArray(JsonElement root, string pascal, string camel)
    {
        var list = new List<string>();
        if (!root.TryGetProperty(pascal, out var el) && !root.TryGetProperty(camel, out el))
            return list;
        if (el.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                list.Add(item.GetString()!);
        }

        return list;
    }

    internal readonly record struct ProcessEventSummary(bool RulesApplied, decimal TotalPointsDeposited, bool HasPayloadShapeError);
}
