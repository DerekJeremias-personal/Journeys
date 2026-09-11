using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.Core.Workflow;

namespace Journeys.API.CampaignAgent.Workflow;

/// <summary>
/// Unified coach for CampaignBuild — PAT upserts before journey, no inline manifest ids.
/// Absorbs post-open PAT deferral coaching (formerly PatMutatorDeferralCoach).
/// </summary>
public static class CampaignBuildCoach
{
    public static string? TryGetHint(CampaignWorkflowState state, IReadOnlyList<string>? toolsThisTurn = null)
    {
        if (!ShouldApply(state))
            return null;

        var validateHint = ValidateLoopCoach.TryGetHint(state.Artifacts.LastValidateViolationCode);
        if (!string.IsNullOrWhiteSpace(validateHint))
            return validateHint;

        var package = BuildContextPackageResolver.Resolve(state, toolsThisTurn);
        if (package.SubStep == CampaignBuildSubStep.PatRequired && package.ToolSurfaceIncludesPatUpsert)
        {
            return "Coach: upsert_point_account_type is on toolsThisTurn — invoke it now; do not claim the tool is missing.";
        }

        return "Coach: CampaignBuild — " + WorkflowSkillRegistry.BuildNextStepLine(state);
    }

    internal static bool ShouldApply(CampaignWorkflowState state)
    {
        if (string.IsNullOrWhiteSpace(state.Artifacts.CampaignDesignBriefProposed))
            return false;
        if (state.CampaignKind == CampaignWorkflowKind.EventDriven
            && !EventModelsReadiness.Evaluate(state).IsReady)
            return false;
        if (CreationSnapshotArtifact.Read(state) is { CreationComplete: true })
            return false;

        if (PointAccountManifestBuilder.Parse(state.Artifacts.PointAccountManifest).Items.Count > 0
            && !LooksLikeBuildStall(state))
            return false;

        return true;
    }

    private static bool LooksLikeBuildStall(CampaignWorkflowState state)
    {
        var summary = state.Artifacts.LastToolRemediationSummary ?? "";
        return state.Artifacts.DeferredMutatorRetry
               || state.Artifacts.PatHttpDeferralShown
               || state.Artifacts.PatUpsertPending
               || !string.IsNullOrWhiteSpace(state.Artifacts.LastValidateViolationCode)
               || summary.Contains("BUILD_GATE_PAT_MANIFEST_REQUIRED", StringComparison.OrdinalIgnoreCase)
               || summary.Contains("BUILD_GATE_JOURNEY_RULESETS_REQUIRED", StringComparison.OrdinalIgnoreCase)
               || state.Artifacts.LastValidateRuleSetCount is 0
               || summary.Contains("ruleSetCount=0", StringComparison.OrdinalIgnoreCase)
               || summary.Contains("journey.ruleSetCount is 0", StringComparison.OrdinalIgnoreCase)
               || MutatorSurfaceDeferralPatterns.LooksLikeDeferral(summary);
    }
}
