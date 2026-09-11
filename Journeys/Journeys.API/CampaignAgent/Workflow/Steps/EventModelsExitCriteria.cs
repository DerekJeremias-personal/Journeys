using Journeys.API.CampaignAgent.Workflow.Steps;
using Journeys.Core.Models;
using Journeys.Core.Utility;

namespace Journeys.API.CampaignAgent.Workflow.Steps;

public sealed class EventModelsExitCriteria : IWorkflowStepExitCriteria
{
    public CampaignWorkflowPhase Phase => CampaignWorkflowPhase.EventModels;

    public bool IsMet(CampaignWorkflowState state) =>
        EventModelsReadiness.Evaluate(state).IsReady;
}
