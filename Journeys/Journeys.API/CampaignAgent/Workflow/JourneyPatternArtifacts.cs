using Journeys.Core.Models;
using Journeys.Core.Utility;

namespace Journeys.API.CampaignAgent.Workflow;

internal static class JourneyPatternArtifacts
{
    public static int GetJourneyRuleSetCount(CampaignWorkflowState state)
    {
        var snap = CreationSnapshotArtifact.Read(state);
        if (snap is { JourneyRuleSetCount: > 0 })
            return snap.JourneyRuleSetCount;

        if (state.Artifacts.LastValidateRuleSetCount is > 0)
            return state.Artifacts.LastValidateRuleSetCount.Value;

        return 0;
    }

    public static void ClearIfJourneyPersisted(CampaignWorkflowState state)
    {
        if (GetJourneyRuleSetCount(state) <= 0
            && !CreationSnapshotArtifact.IsCreationComplete(state))
        {
            return;
        }

        state.Artifacts.JourneyPatternSkeletonJson = null;
        state.Artifacts.JourneyPatternPrepComplete = false;
        state.Artifacts.JourneyPatternRecipesFullResultShown = false;
        state.Artifacts.JourneyPatternExampleFullResultShown = false;
        state.Artifacts.JourneyExampleCampaignFetchCount = 0;
        JourneyContractSummaryPinArtifacts.Clear(state);
    }
}
