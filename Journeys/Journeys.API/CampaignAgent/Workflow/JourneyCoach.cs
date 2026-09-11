using System.Text.Json;
using Journeys.CampaignAgent.Remediation;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.DTO.Models;

namespace Journeys.API.CampaignAgent.Workflow;

/// <summary>
/// Coaches journey authoring when upsert succeeds but no rule sets persisted, when assistant context
/// shows empty journey, or when MCP invocation errors occur on validate/upsert.
/// </summary>
public static class JourneyCoach
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static void ApplyUpsertOutcome(CampaignWorkflowState state, string resultJson, bool success)
    {
        if (!success)
            return;

        if (!IsJourneyAuthoringIncomplete(state))
            return;

        var snap = CreationSnapshotArtifact.Read(state);
        if (snap != null && snap.JourneyRuleSetCount > 0)
            return;

        state.Artifacts.LastToolRemediationSummary = CampaignAgentGuidanceText.EmptyJourneyRuleSetRemediation;
    }

    public static void ApplyAssistantContextOutcome(CampaignWorkflowState state, string resultJson, bool success)
    {
        if (!success || string.IsNullOrWhiteSpace(resultJson))
            return;

        resultJson = ToolResultJsonNormalizer.Unwrap(resultJson) ?? resultJson;
        if (!ToolResultSuccessEvaluator.LooksSuccessful(resultJson))
            return;

        CampaignAssistantContextDto? ctx;
        try
        {
            ctx = JsonSerializer.Deserialize<CampaignAssistantContextDto>(resultJson, JsonOpts);
        }
        catch (JsonException)
        {
            return;
        }

        if (ctx == null || string.IsNullOrWhiteSpace(ctx.CampaignId))
            return;

        var journey = ctx.Journey;
        var ruleSetCount = journey?.RuleSetCount ?? 0;
        var nodeCount = journey?.JourneyNodeCount ?? 0;
        var outcomeCount = journey?.OutcomeKindCounts?.Values.Sum() ?? 0;
        var priorRuleSetCount = CreationSnapshotArtifact.Read(state)?.JourneyRuleSetCount ?? 0;

        CreationSnapshotArtifact.MergeFromJourneyUpsert(
            state,
            ctx.CampaignId,
            ctx.Status,
            ruleSetCount,
            outcomeCount);

        if (journey != null)
        {
            var digestJson = JsonSerializer.Serialize(journey, JsonOpts);
            state.Artifacts.JourneyDigestProposed = digestJson;
            if (ruleSetCount > 0)
                state.Artifacts.JourneyDigestApproved = digestJson;
        }

        if (ruleSetCount > 0)
        {
            CampaignValidationCoach.ClearValidationStall(state);
            if (IsEmptyRuleSetRemediation(state.Artifacts.LastToolRemediationSummary))
                state.Artifacts.LastToolRemediationSummary = null;

            if (priorRuleSetCount == 0)
                TryApplyPostJourneyHandoff(state, ruleSetCount, ctx.Status);
        }
        else if (!string.IsNullOrWhiteSpace(state.Artifacts.CampaignShellRef))
        {
            state.Artifacts.LastToolRemediationSummary = CampaignAgentGuidanceText.EmptyJourneyRuleSetRemediation;
        }
    }

    public static void ApplyInvocationFailure(CampaignWorkflowState state, string toolName)
    {
        state.Artifacts.LastToolRemediationSummary = BuildInvocationFailureRemediation(toolName);

        if (IsValidateOrUpsertCampaign(toolName))
            CampaignValidationCoach.MarkUpsertFailedSinceValidate(state);
    }

    public static void ApplyExampleDiscoveryTool(CampaignWorkflowState state, string toolName)
    {
        if (!state.Artifacts.JourneyPatternPrepComplete)
            return;

        state.Artifacts.JourneyExampleCampaignFetchCount++;

        if (JourneyPatternArtifacts.GetJourneyRuleSetCount(state) > 0)
            return;

        if (state.Artifacts.JourneyExampleCampaignFetchCount >= 1)
        {
            state.Artifacts.LastToolRemediationSummary =
                "Journey pattern skeleton is pinned in WORKFLOW ARTIFACTS — apply validate nextSteps to cited paths; "
                + "do not list_example_campaigns or re-fetch examples.";
        }
    }

    public static string? GetCoachHint(CampaignWorkflowState state)
    {
        if (!IsJourneyAuthoringIncomplete(state))
            return null;

        if (!IsJourneyCoachPhase(state.Phase))
            return null;

        return "Coach: " + CampaignAgentGuidanceText.EmptyJourneyRuleSetRemediation;
    }

    internal static bool IsJourneyAuthoringIncomplete(CampaignWorkflowState state)
    {
        if (string.IsNullOrWhiteSpace(state.Artifacts.CampaignShellRef))
            return false;

        var snap = CreationSnapshotArtifact.Read(state);
        if (snap == null)
            return true;

        return snap.JourneyRuleSetCount <= 0;
    }

    private static bool IsJourneyCoachPhase(CampaignWorkflowPhase phase) =>
        phase is CampaignWorkflowPhase.CampaignBuild
            or CampaignWorkflowPhase.CampaignJourney
            or CampaignWorkflowPhase.Done
            or CampaignWorkflowPhase.Verification;

    private static bool IsValidateOrUpsertCampaign(string toolName) =>
        string.Equals(toolName, "validate_campaign", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "ValidateCampaign", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "upsert_campaign", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "UpsertCampaign", StringComparison.OrdinalIgnoreCase);

    private static bool IsEmptyRuleSetRemediation(string? summary) =>
        !string.IsNullOrWhiteSpace(summary)
        && summary.Contains(CampaignAgentGuidanceText.EmptyJourneyRuleSetMarker, StringComparison.OrdinalIgnoreCase);

    private static void TryApplyPostJourneyHandoff(CampaignWorkflowState state, int ruleSetCount, string? status)
    {
        if (state.Artifacts.PostJourneyVerifyHandoffShown)
            return;
        if (state.CampaignKind != CampaignWorkflowKind.EventDriven)
            return;
        if (!string.Equals(status, CampaignStatusStrings.Draft, StringComparison.OrdinalIgnoreCase))
            return;

        state.Artifacts.PostJourneyVerifyHandoffShown = true;
        state.Artifacts.LastToolRemediationSummary =
            $"Journey saved (ruleSetCount={ruleSetCount}). Next: draft verification — get_account then process_event with campaignId from THREAD CONTEXT. "
            + "Do not promote to Live until user explicitly requests.";
    }

    private static string BuildInvocationFailureRemediation(string toolName) =>
        $"{toolName} returned an MCP invocation error (not a validation body). "
        + "Do not describe this as transient connectivity or claim the journey saved. "
        + "Retry with a smaller payload: upsert one tier node at a time, or validate a single-node slice first. "
        + "Confirm persistence via get_campaign_assistant_context (ruleSetCount must be > 0).";
}
