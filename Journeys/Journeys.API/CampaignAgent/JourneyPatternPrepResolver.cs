using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Journeys.Core.Utility;

namespace Journeys.API.CampaignAgent;

internal static class JourneyPatternPrepResolver
{
    public static bool ShouldRun(CampaignWorkflowState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!CampaignWorkflowChecklist.IsBriefCaptured(state))
            return false;
        if (state.Artifacts.JourneyPatternPrepComplete)
            return false;
        if (state.Artifacts.ValidationStalled)
            return false;
        if (PointAccountManifestBuilder.Parse(state.Artifacts.PointAccountManifest).Items.Count == 0)
            return false;
        if (JourneyPatternArtifacts.GetJourneyRuleSetCount(state) > 0)
            return false;

        return JourneyPatternClassifier.Resolve(state) != null;
    }

    public static bool TryPrepare(CampaignWorkflowState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!ShouldRun(state))
            return false;

        var patternId = JourneyPatternClassifier.Resolve(state);
        if (patternId == null)
            return false;

        state.Artifacts.JourneyPatternId = patternId;
        state.Artifacts.JourneyPatternSkeletonJson = JourneyPatternSkeletonBuilder.TryBuild(patternId, state);
        state.Artifacts.JourneyPatternPrepComplete = true;
        return true;
    }
}
