using Journeys.Core.Models;
using Journeys.Core.Utility;

namespace Journeys.API.CampaignAgent.Workflow.Steps;

public sealed class PointAccountTypesExitCriteria : IWorkflowStepExitCriteria
{
    public CampaignWorkflowPhase Phase => CampaignWorkflowPhase.PointAccountTypes;

    public bool IsMet(CampaignWorkflowState state) =>
        PointAccountManifestGate.IsSatisfied(state, out _);
}
