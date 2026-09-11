using Journeys.CampaignAgent.Remediation;
using Journeys.Core.Models;
using Journeys.Core.Utility;

namespace Journeys.API.CampaignAgent.Workflow.Steps;

public sealed class VerificationExitCriteria : IWorkflowStepExitCriteria
{
    public CampaignWorkflowPhase Phase => CampaignWorkflowPhase.Verification;

    public bool IsMet(CampaignWorkflowState state) =>
        !string.IsNullOrWhiteSpace(state.Artifacts.VerificationRecord)
        && ToolResultSuccessEvaluator.LooksSuccessful(state.Artifacts.VerificationRecord)
        && !VerificationRecordManifestBridge.IsManifestSeedOnly(state.Artifacts.VerificationRecord);
}
