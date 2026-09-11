using Journeys.Core.Models;

namespace Journeys.API.CampaignAgent.Workflow.Steps;

public sealed class DataAnalysisExitCriteria : IWorkflowStepExitCriteria
{
    public CampaignWorkflowPhase Phase => CampaignWorkflowPhase.DataAnalysis;

    public bool IsMet(CampaignWorkflowState state) =>
        !string.IsNullOrWhiteSpace(state.Artifacts.CampaignDesignBriefProposed);
}
