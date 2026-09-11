using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.Core.Workflow;

namespace Journeys.API.CampaignAgent.Workflow;

public static class CampaignWorkflowFocusResolver
{
    public static CampaignWorkflowPhase Resolve(CampaignWorkflowState state, string? userMessage)
    {
        if (!string.IsNullOrWhiteSpace(userMessage))
        {
            var msg = userMessage.Trim();
            if (WorkflowUserPhraseCatalog.ContainsAny(msg, WorkflowUserPhraseCatalog.RewindDataPhrases))
                return CampaignWorkflowPhase.DataAnalysis;
            if (WorkflowUserPhraseCatalog.ContainsAny(msg, WorkflowUserPhraseCatalog.RewindEventModelPhrases))
                return CampaignWorkflowPhase.EventModels;
            if (WorkflowUserPhraseCatalog.ContainsAny(msg, WorkflowUserPhraseCatalog.CampaignFixPhrases))
                return CampaignWorkflowPhase.CampaignBuild;
            if (WorkflowUserPhraseCatalog.ContainsAny(msg, WorkflowUserPhraseCatalog.TagFirstPhrases))
                return CampaignWorkflowPhase.CampaignBuild;
            if (WorkflowUserPhraseCatalog.ContainsAny(msg, WorkflowUserPhraseCatalog.FocusVerificationPhrases)
                || WorkflowUserPhraseCatalog.ContainsAny(msg, WorkflowUserPhraseCatalog.JourneyVerificationIntentPhrases))
                return CampaignWorkflowPhase.Verification;
            if (WorkflowUserPhraseCatalog.ContainsAny(msg, WorkflowUserPhraseCatalog.FocusJourneyPhrases))
                return CampaignWorkflowPhase.CampaignBuild;
            if (WorkflowUserPhraseCatalog.ContainsAny(msg, WorkflowUserPhraseCatalog.FocusPatPhrases))
                return CampaignWorkflowPhase.CampaignBuild;
            if (WorkflowUserPhraseCatalog.ContainsAny(msg, WorkflowUserPhraseCatalog.FocusEventModelPhrases))
                return CampaignWorkflowPhase.EventModels;
            if (PendingEventModelSpecArtifact.Read(state) != null
                && (WorkflowUserPhraseCatalog.ContainsAny(msg, WorkflowUserPhraseCatalog.FocusEventModelPhrases)
                    || EventModelCreateNewIntent.LooksLikeRequest(msg)))
                return CampaignWorkflowPhase.EventModels;
        }

        return CoachDefaultFocus(state);
    }

    public static CampaignWorkflowPhase CoachDefaultFocus(CampaignWorkflowState state)
    {
        if (!CampaignWorkflowChecklist.IsBriefCaptured(state))
            return CampaignWorkflowPhase.DataAnalysis;

        if (WorkflowSkillRegistry.ResolveBuildSubStep(state) == CampaignBuildSubStep.PatRequired
            && !CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(state))
        {
            return CampaignWorkflowPhase.CampaignBuild;
        }

        if (!CampaignWorkflowChecklist.IsItemComplete(state, CampaignWorkflowPhase.EventModels))
            return CampaignWorkflowPhase.EventModels;
        if (!CampaignWorkflowChecklist.IsItemComplete(state, CampaignWorkflowPhase.CampaignBuild))
            return CampaignWorkflowPhase.CampaignBuild;
        if (!CampaignWorkflowChecklist.IsItemComplete(state, CampaignWorkflowPhase.Verification))
            return CampaignWorkflowPhase.Verification;
        return CampaignWorkflowPhase.Done;
    }
}
