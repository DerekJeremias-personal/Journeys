using Journeys.Core.Models;
using Journeys.Core.Utility;

namespace Journeys.API.CampaignAgent.Workflow.Steps;

public sealed class CampaignJourneyExitCriteria : IWorkflowStepExitCriteria
{
    public CampaignWorkflowPhase Phase => CampaignWorkflowPhase.CampaignJourney;

    public bool IsMet(CampaignWorkflowState state)
    {
        var snap = CreationSnapshotArtifact.Read(state);
        if (snap is { JourneyRuleSetCount: > 0 })
            return true;

        return JourneyDigestUtility.TryReadRuleSetCountFromDigest(
                   state.Artifacts.JourneyDigestApproved)
               > 0
               || JourneyDigestUtility.TryReadRuleSetCountFromDigest(
                   state.Artifacts.JourneyDigestProposed)
               > 0;
    }
}
