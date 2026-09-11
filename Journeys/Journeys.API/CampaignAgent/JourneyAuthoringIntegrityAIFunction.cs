using System.Text.Json;
using Journeys.Core.Models;
using Journeys.Core.Services;
using Journeys.Core.Workflow;
using Journeys.DTO.Exceptions;
using Microsoft.Extensions.AI;

namespace Journeys.API.CampaignAgent;

internal sealed class JourneyAuthoringIntegrityAIFunction : DelegatingAIFunction
{
    private readonly Func<CampaignWorkflowState> _getState;
    private readonly bool _isUpsert;

    public JourneyAuthoringIntegrityAIFunction(AIFunction inner, Func<CampaignWorkflowState> getState, bool isUpsert)
        : base(inner)
    {
        _getState = getState;
        _isUpsert = isUpsert;
    }

    protected override ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        var state = _getState();
        var json = ExtractCampaignJson(arguments);
        if (string.IsNullOrWhiteSpace(json))
            return InnerFunction.InvokeAsync(arguments, cancellationToken);

        try
        {
            using var doc = JsonDocument.Parse(json);
            CampaignJourneyAuthoringShapeValidator.ValidateInlineManifestForbidden(
                doc.RootElement,
                JourneyDeliveryGate.WorkflowPatCount(state));
        }
        catch (APIErrorsException ex)
        {
            return ValueTask.FromResult<object?>(SerializeShapeErrors(ex));
        }
        catch (JsonException)
        {
            // malformed JSON — let inner tool handle it
        }

        if (_isUpsert && JourneyDeliveryGate.ShouldBlockJourneyUpsert(state, json))
        {
            JourneyDeliveryGate.ApplyBlock(state);
            return ValueTask.FromResult<object?>(JourneyDeliveryGate.BuildBlockedToolResult());
        }

        return InnerFunction.InvokeAsync(arguments, cancellationToken);
    }

    private static string SerializeShapeErrors(APIErrorsException ex)
    {
        var errors = ex.Errors.ToDictionary(
            kvp => kvp.Key,
            kvp => (object?)kvp.Value);
        return JsonSerializer.Serialize(new { errors, _agentRemediation = ex.Errors.Values.FirstOrDefault() });
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
