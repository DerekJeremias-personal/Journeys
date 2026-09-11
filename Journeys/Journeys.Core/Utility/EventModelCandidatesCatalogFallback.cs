using Journeys.Core.Models;

namespace Journeys.Core.Utility;

/// <summary>
/// When Backend MCP catalog fetch failed (or prep did not run), rank event-model candidates from
/// list_models / get_all_models tool results the agent already called.
/// </summary>
public static class EventModelCandidatesCatalogFallback
{
    public static bool NeedsCandidateRanking(CampaignWorkflowState state) =>
        !string.IsNullOrWhiteSpace(state.Artifacts.CampaignDesignBriefProposed)
        && !state.UserSkippedEventModels
        && state.CampaignKind != CampaignWorkflowKind.TagFirst
        && !state.ModelGatePassed;

    public static bool ShouldAttemptMcpCatalogFetch(CampaignWorkflowState state)
    {
        if (!NeedsCandidateRanking(state))
            return false;

        var set = EventModelCandidatesArtifact.Read(state);
        if (set == null)
            return true;

        if (set.FetchFailed)
            return true;

        return set.Candidates.Count == 0;
    }

    public static bool ShouldRefreshFromToolResult(CampaignWorkflowState state) =>
        NeedsCandidateRanking(state) && ShouldAttemptMcpCatalogFetch(state);

    public static bool TryApplyFromCatalogToolResult(CampaignWorkflowState state, string toolJson)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!ShouldRefreshFromToolResult(state))
            return false;

        if (!ModelCatalogToolResultParser.TryParseModels(toolJson, out var models) || models.Count == 0)
            return false;

        var brief = !string.IsNullOrWhiteSpace(state.Artifacts.CampaignDesignBriefApproved)
            ? state.Artifacts.CampaignDesignBriefApproved
            : state.Artifacts.CampaignDesignBriefProposed;
        state.Artifacts.EarningIntent = EarningIntentResolver.Resolve(brief);

        var set = EventModelRanker.Rank(models);
        EventModelCandidatesArtifact.Write(state, set);

        if (set.Candidates.Count > 0
            && !state.Artifacts.UserRequestedNewEventModel
            && state.Artifacts.AwaitingApproval == CampaignWorkflowApprovalKind.None)
        {
            state.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.EventModelSelection;
        }

        return true;
    }
}
