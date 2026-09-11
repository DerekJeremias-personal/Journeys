using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.Core.Workflow;

namespace Journeys.API.CampaignAgent.Workflow;

/// <summary>
/// Arms PAT handoff artifacts when the agent narrates mutator-surface deferral while the Events gate is still closed.
/// </summary>
public static class PatGateStallCapture
{
    public static bool TryCaptureFromAssistantText(CampaignWorkflowState state, string? plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
            return false;
        if (!CampaignWorkflowChecklist.IsBriefCaptured(state))
            return false;
        if (state.UserSkippedEventModels || state.CampaignKind == CampaignWorkflowKind.TagFirst)
            return false;
        if (!CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(state))
            return false;
        if (PointAccountManifestBuilder.Parse(state.Artifacts.PointAccountManifest).Items.Count > 0)
            return false;
        if (!MutatorSurfaceDeferralPatterns.LooksLikeDeferral(plainText))
            return false;
        if (!MentionsPatCreation(plainText))
            return false;

        state.Artifacts.PatHttpDeferralShown = true;
        state.Artifacts.PatUpsertPending = true;
        return true;
    }

    private static bool MentionsPatCreation(string text) =>
        text.Contains("upsert_point_account_type", StringComparison.OrdinalIgnoreCase)
        || text.Contains("point account type", StringComparison.OrdinalIgnoreCase);
}
