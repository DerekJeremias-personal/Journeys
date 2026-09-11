using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.DTO.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace Journeys.API.CampaignAgent;

internal static class EventModelPrepResolver
{
    public static bool ShouldAttemptAutoResolve(CampaignWorkflowState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Artifacts.UserRequestedNewEventModel)
            return false;
        if (state.ModelGatePassed)
            return false;
        if (state.Artifacts.AwaitingApproval != CampaignWorkflowApprovalKind.None)
            return false;
        if (EventModelsReadiness.Evaluate(state).IsReady)
            return false;

        var set = EventModelCandidatesArtifact.Read(state);
        if (set == null || string.IsNullOrWhiteSpace(set.RecommendedDefaultId))
            return false;

        var rec = set.Candidates.FirstOrDefault(c =>
            string.Equals(c.EventModelId, set.RecommendedDefaultId, StringComparison.OrdinalIgnoreCase));
        return rec is { IsStrongMatch: true };
    }

    public static bool ApplyStrongMatchModelJson(CampaignWorkflowState state, string rawModelJson)
    {
        ArgumentNullException.ThrowIfNull(state);

        CampaignWorkflowStepManager.ApplyToolResults(state, [("get_model", rawModelJson)]);
        CampaignWorkflowStepManager.TryUpdateChecklist(state);
        return EventModelsReadiness.Evaluate(state).IsReady;
    }

    public static async Task<bool> TryAutoResolveStrongMatchAsync(
        CampaignWorkflowState state,
        McpClient mcp,
        IReadOnlyList<AITool> tools,
        string tenantId,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!ShouldAttemptAutoResolve(state))
            return false;

        var set = EventModelCandidatesArtifact.Read(state)!;
        var modelId = set.RecommendedDefaultId!;

        var raw = await EventModelSingleModelFetcher
            .FetchAsync(mcp, tools, tenantId, modelId, logger, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        return ApplyStrongMatchModelJson(state, raw);
    }
}
