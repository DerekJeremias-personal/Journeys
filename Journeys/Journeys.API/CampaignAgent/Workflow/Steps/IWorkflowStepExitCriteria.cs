using Journeys.Core.Models;

namespace Journeys.API.CampaignAgent.Workflow.Steps;

public interface IWorkflowStepExitCriteria
{
    CampaignWorkflowPhase Phase { get; }
    bool IsMet(CampaignWorkflowState state);
}
