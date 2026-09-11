using Journeys.Core.Models;
using Journeys.Core.Utility;

namespace Journeys.API.CampaignAgent.Workflow;

/// <summary>
/// When the Events gate is open but PAT upserts were filtered or the agent deferred to HTTP/paste-GUID
/// instructions, steers the agent to call upsert_point_account_type now.
/// </summary>
public static class PatMutatorDeferralCoach
{
    public static string? TryGetHint(CampaignWorkflowState state)
    {
        if (!ShouldApply(state))
            return null;

        var message =
            "Coach: Events gate is OPEN — upsert_point_account_type is available on this turn. "
            + "A prior PAT upsert failed because mutators were filtered from the tool surface, not because MCP is missing. "
            + "Do NOT ask the user to create PATs via HTTP or paste GUIDs. "
            + "Call upsert_point_account_type now (expired sink PATs first, then earning PATs that reference them). "
            + "PointAccountManifest must be non-empty before journey authoring.";

        if (state.Artifacts.PatHttpDeferralShown
            || MutatorSurfaceDeferralPatterns.LooksLikeDeferral(state.Artifacts.LastToolRemediationSummary))
        {
            message += " Prior turn incorrectly deferred PAT creation — retry with upsert tools now.";
        }

        return message;
    }

    internal static bool ShouldApply(CampaignWorkflowState state)
    {
        if (!CampaignWorkflowChecklist.IsBriefCaptured(state))
            return false;
        if (state.CampaignKind != CampaignWorkflowKind.EventDriven)
            return false;
        if (CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(state))
            return false;
        if (PointAccountManifestBuilder.Parse(state.Artifacts.PointAccountManifest).Items.Count > 0)
            return false;

        return state.Artifacts.DeferredMutatorRetry
               || state.Artifacts.PatHttpDeferralShown
               || MutatorSurfaceDeferralPatterns.LooksLikeDeferral(state.Artifacts.LastToolRemediationSummary);
    }
}
