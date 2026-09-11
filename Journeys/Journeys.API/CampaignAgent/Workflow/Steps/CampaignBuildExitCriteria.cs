using Journeys.Core.Models;
using Journeys.Core.Utility;

namespace Journeys.API.CampaignAgent.Workflow.Steps;

public sealed class CampaignBuildExitCriteria : IWorkflowStepExitCriteria
{
    public CampaignWorkflowPhase Phase => CampaignWorkflowPhase.CampaignBuild;

    public bool IsMet(CampaignWorkflowState state) =>
        CreationSnapshotArtifact.Read(state) is { CreationComplete: true };
}
