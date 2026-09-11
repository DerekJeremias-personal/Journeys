using Journeys.API.CampaignAgent.Workflow.Steps;
using Journeys.Core.Models;

namespace Journeys.API.CampaignAgent.Workflow;

public static class CampaignWorkflowChecklist
{
    private static readonly IWorkflowStepExitCriteria[] Items =
    [
        new DataAnalysisExitCriteria(),
        new EventModelsExitCriteria(),
        new CampaignBuildExitCriteria(),
        new VerificationExitCriteria(),
    ];

    public static bool IsBriefCaptured(CampaignWorkflowState state) =>
        !string.IsNullOrWhiteSpace(state.Artifacts.CampaignDesignBriefProposed);

    public static bool IsItemComplete(CampaignWorkflowState state, CampaignWorkflowPhase phase)
    {
        if (phase == CampaignWorkflowPhase.EventModels
            && (state.UserSkippedEventModels || state.CampaignKind == CampaignWorkflowKind.TagFirst))
            return true;

        if (phase == CampaignWorkflowPhase.Verification)
            return new VerificationExitCriteria().IsMet(state);

        if (phase == CampaignWorkflowPhase.DataAnalysis)
            return !string.IsNullOrWhiteSpace(state.Artifacts.CampaignDesignBriefProposed);

        if (CampaignWorkflowPhaseNormalizer.IsBuildPhase(phase))
            return new CampaignBuildExitCriteria().IsMet(state);

        var item = Items.FirstOrDefault(i => i.Phase == phase);
        return item?.IsMet(state) ?? false;
    }

    public static bool IsWorkflowComplete(CampaignWorkflowState state)
    {
        if (!IsItemComplete(state, CampaignWorkflowPhase.DataAnalysis)) return false;
        if (!IsItemComplete(state, CampaignWorkflowPhase.EventModels)) return false;
        if (!IsItemComplete(state, CampaignWorkflowPhase.CampaignBuild)) return false;
        return VerificationDeliveryGuard.IsVerificationSatisfiedForWorkflowComplete(state);
    }

    public static string FormatDigest(CampaignWorkflowState state)
    {
        static string Mark(bool complete) => complete ? "✓" : "○";
        return string.Join(" ",
        [
            $"[{Mark(IsItemComplete(state, CampaignWorkflowPhase.DataAnalysis))} Brief]",
            $"[{Mark(IsItemComplete(state, CampaignWorkflowPhase.EventModels))} Events]",
            $"[{Mark(IsItemComplete(state, CampaignWorkflowPhase.CampaignBuild))} Build]",
            $"[{Mark(IsItemComplete(state, CampaignWorkflowPhase.Verification))} Verify]",
        ]);
    }
}
