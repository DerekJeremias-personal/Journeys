using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Microsoft.Extensions.AI;

namespace Journeys.API.CampaignAgent;

internal sealed class LivePromotionGuardUpsertAIFunction : DelegatingAIFunction
{
    private readonly Func<CampaignWorkflowState> _getState;

    public LivePromotionGuardUpsertAIFunction(AIFunction inner, Func<CampaignWorkflowState> getState)
        : base(inner) => _getState = getState;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var state = _getState();
        var json = ExtractCampaignJson(arguments);
        if (LivePromotionGuard.ShouldBlockLivePromotion(state, json))
        {
            LivePromotionGuard.ApplyBlock(state);
            return ValueTask.FromResult<object?>(LivePromotionGuard.BuildBlockedToolResult());
        }

        return InnerFunction.InvokeAsync(arguments, cancellationToken);
    }

    private static string? ExtractCampaignJson(AIFunctionArguments arguments)
    {
        if (arguments.TryGetValue("campaignJson", out var raw) && raw is string s)
            return s;
        if (arguments.TryGetValue("CampaignJson", out raw) && raw is string p)
            return p;
        return null;
    }
}
