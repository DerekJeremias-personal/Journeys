using Journeys.Core.Models;
using Journeys.Core.Workflow;
using Microsoft.Extensions.AI;

namespace Journeys.API.CampaignAgent;

internal sealed class CampaignBuildGateAIFunction : DelegatingAIFunction
{
    private readonly Func<CampaignWorkflowState> _getState;

    public CampaignBuildGateAIFunction(AIFunction inner, Func<CampaignWorkflowState> getState)
        : base(inner) => _getState = getState;

    protected override ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        var state = _getState();
        var json = ExtractCampaignJson(arguments);
        if (CampaignBuildGate.ShouldBlockJourneyPayload(state, json))
        {
            CampaignBuildGate.ApplyBlock(state);
            return ValueTask.FromResult<object?>(CampaignBuildGate.BuildBlockedToolResult());
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
