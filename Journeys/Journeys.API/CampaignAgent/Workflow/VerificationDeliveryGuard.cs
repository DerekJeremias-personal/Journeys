using Journeys.API.CampaignAgent.Workflow.Steps;
using Journeys.Core.Models;
using Journeys.Core.Utility;

namespace Journeys.API.CampaignAgent.Workflow;

public static class VerificationDeliveryGuard
{
    public static bool ShouldSurfaceVerificationCoach(CampaignWorkflowState state) =>
        state.CampaignKind == CampaignWorkflowKind.EventDriven
        && (state.Phase == CampaignWorkflowPhase.Verification
            || state.Artifacts.VerificationUserTestIntentThisTurn
            || (CreationSnapshotArtifact.IsCreationComplete(state)
                && !new VerificationExitCriteria().IsMet(state)));

    public static bool IsVerificationSatisfiedForWorkflowComplete(CampaignWorkflowState state)
    {
        if (state.CampaignKind == CampaignWorkflowKind.TagFirst)
            return CampaignWorkflowChecklist.IsItemComplete(state, CampaignWorkflowPhase.CampaignJourney);

        return CampaignWorkflowChecklist.IsItemComplete(state, CampaignWorkflowPhase.Verification);
    }

    public static bool ShouldContinueForVerification(CampaignWorkflowState state, int continuationHopsUsed)
    {
        if (CampaignJourneyDeliveryGuard.ShouldBlockContinuation(state))
            return false;
        if (continuationHopsUsed >= 2)
            return false;
        if (state.Artifacts.VerificationBlockedNoAllowlist)
            return false;
        if (state.Artifacts.VerificationToolsInvokedThisSegment)
            return false;
        if (!ShouldSurfaceVerificationCoach(state))
            return false;
        if (!state.Artifacts.VerificationUserTestIntentThisTurn
            && state.Phase != CampaignWorkflowPhase.Verification)
            return false;
        if (new VerificationExitCriteria().IsMet(state))
            return false;

        return true;
    }

    public static bool IsVerificationTool(string? toolName) =>
        string.Equals(toolName, "get_account", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "GetAccount", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "get_campaign_assistant_context", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "GetCampaignAssistantContext", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "process_event", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "ProcessEvent", StringComparison.OrdinalIgnoreCase);
}
