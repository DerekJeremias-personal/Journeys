using Journeys.CampaignAgent.Remediation;
using Journeys.Core.Models;
using Journeys.Core.Utility;

namespace Journeys.Core.Workflow;

/// <summary>
/// Authoritative skill resolution, build sub-steps, and SESSION manifest lines.
/// </summary>
public static class WorkflowSkillRegistry
{
    public static CampaignWorkflowSkill ResolveActiveSkill(CampaignWorkflowState state)
    {
        if (!IsBriefCaptured(state))
            return CampaignWorkflowSkill.Brief;

        if (state.CampaignKind == CampaignWorkflowKind.EventDriven
            && !state.UserSkippedEventModels
            && !EventModelsReadiness.Evaluate(state).IsReady)
            return CampaignWorkflowSkill.EventModels;

        if (!IsBuildComplete(state))
            return CampaignWorkflowSkill.CampaignBuild;

        if (!IsVerificationComplete(state))
            return CampaignWorkflowSkill.Verification;

        return CampaignWorkflowSkill.Verification;
    }

    public static CampaignBuildSubStep ResolveBuildSubStep(CampaignWorkflowState state)
    {
        if (IsBuildComplete(state))
            return CampaignBuildSubStep.BuildComplete;

        if (PointAccountManifestBuilder.Parse(state.Artifacts.PointAccountManifest).Items.Count == 0)
            return CampaignBuildSubStep.PatRequired;

        return CampaignBuildSubStep.JourneyRequired;
    }

    public static string BuildNextStepLine(CampaignWorkflowState state) =>
        ResolveBuildSubStep(state) switch
        {
            CampaignBuildSubStep.PatRequired =>
                "upsert_point_account_type — expired sink PAT first, then spendable; do not inline manifest ids or fake GUIDs",
            CampaignBuildSubStep.JourneyRequired =>
                "validate_campaign until RuleSetCount > 0, then upsert_campaign — use children[] not nodes[]; RuleSet wrappers in journey.rules[]",
            CampaignBuildSubStep.BuildComplete =>
                "creation complete — proceed to verification when user asks to test",
            _ =>
                "optional upsert_campaign shell placeholder — PAT upserts required before journey JSON"
        };

    public static string BuildSessionSkillBlock(
        CampaignWorkflowState state,
        IReadOnlyList<string> toolsThisTurn)
    {
        var skill = ResolveActiveSkill(state);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("SKILL MANIFEST (authoritative — overrides phase prose if conflict)");
        sb.AppendLine($"- activeSkill: {skill}");
        if (skill == CampaignWorkflowSkill.CampaignBuild)
        {
            var subStep = ResolveBuildSubStep(state);
            sb.AppendLine($"- buildSubStep: {subStep.ToString().ToLowerInvariant()}");

            var brief = state.Artifacts.CampaignDesignBriefApproved
                        ?? state.Artifacts.CampaignDesignBriefProposed;
            var episode = RuleEpisodeInferrer.Infer(brief, null, state.Artifacts.LastValidateViolationCode);
            sb.AppendLine($"- ruleEpisode: {episode.ToString().ToLowerInvariant()}");

            if (subStep == CampaignBuildSubStep.PatRequired
                && toolsThisTurn.Any(t => string.Equals(t, "upsert_point_account_type", StringComparison.OrdinalIgnoreCase)
                                        || string.Equals(t, "UpsertPointAccountType", StringComparison.OrdinalIgnoreCase)))
            {
                sb.AppendLine("- patToolOnSurface: yes");
            }
        }

        var sortedTools = toolsThisTurn
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();
        sb.AppendLine($"- toolsThisTurn: {string.Join(", ", sortedTools)}");

        if (ResolveBuildSubStep(state) == CampaignBuildSubStep.PatRequired)
        {
            sb.AppendLine(
                "- blockedUntilPatManifest: validate_campaign (journey payload), upsert_campaign (journey payload)");
            sb.AppendLine(
                "- graduatesWhen: PointAccountManifest.items non-empty from upsert_point_account_type");
        }

        if (ResolveBuildSubStep(state) == CampaignBuildSubStep.JourneyRequired)
        {
            sb.AppendLine(
                "- blockedUntilJourneyPersisted: upsert_campaign (journey payload) until validate RuleSetCount > 0");
        }

        sb.AppendLine($"- nextStep: {BuildNextStepLine(state)}");
        sb.AppendLine("- crossSkill: event models → EventModels; draft test → Verification");
        return sb.ToString().TrimEnd();
    }

    private static bool IsBriefCaptured(CampaignWorkflowState state) =>
        !string.IsNullOrWhiteSpace(state.Artifacts.CampaignDesignBriefProposed);

    private static bool IsBuildComplete(CampaignWorkflowState state) =>
        CreationSnapshotArtifact.Read(state) is { CreationComplete: true };

    private static bool IsVerificationComplete(CampaignWorkflowState state) =>
        !string.IsNullOrWhiteSpace(state.Artifacts.VerificationRecord)
        && ToolResultSuccessEvaluator.LooksSuccessful(state.Artifacts.VerificationRecord);
}
