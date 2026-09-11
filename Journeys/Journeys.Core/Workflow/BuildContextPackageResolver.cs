using Journeys.Core.Models;

namespace Journeys.Core.Workflow;

public static class BuildContextPackageResolver
{
    private static readonly HashSet<string> PatUpsertToolNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "upsert_point_account_type",
        "UpsertPointAccountType"
    };

    public static BuildContextPackage Resolve(
        CampaignWorkflowState state,
        IReadOnlyList<string>? toolsThisTurn = null)
    {
        var subStep = WorkflowSkillRegistry.ResolveBuildSubStep(state);
        var brief = state.Artifacts.CampaignDesignBriefApproved
                    ?? state.Artifacts.CampaignDesignBriefProposed;
        var episode = RuleEpisodeInferrer.Infer(
            brief,
            null,
            state.Artifacts.LastValidateViolationCode);

        var includesPatUpsert = toolsThisTurn?.Any(t => PatUpsertToolNames.Contains(t)) == true;

        return new BuildContextPackage(
            subStep,
            episode,
            state.Artifacts.LastValidateViolationCode,
            state.Artifacts.JourneyPatternId,
            includesPatUpsert);
    }
}
