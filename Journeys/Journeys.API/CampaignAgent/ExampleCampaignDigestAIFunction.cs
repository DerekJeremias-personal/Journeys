using System;
using System.Threading;
using System.Threading.Tasks;
using Journeys.CampaignAgent.Remediation;
using Journeys.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Journeys.API.CampaignAgent;

internal sealed class ExampleCampaignDigestAIFunction : DelegatingAIFunction
{
    private readonly bool _enabled;
    private readonly ILogger? _logger;
    private readonly Func<CampaignWorkflowState>? _getWorkflowState;

    public ExampleCampaignDigestAIFunction(
        AIFunction innerFunction,
        bool enabled,
        ILogger? logger,
        Func<CampaignWorkflowState>? getWorkflowState = null)
        : base(innerFunction)
    {
        _enabled = enabled;
        _logger = logger;
        _getWorkflowState = getWorkflowState;
    }

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var result = await InnerFunction.InvokeAsync(arguments, cancellationToken).ConfigureAwait(false);
        if (!_enabled)
            return result;

        try
        {
            if (!ToolResultTextExtractor.TryExtractText(result, out var text, out var rewrap) || string.IsNullOrWhiteSpace(text))
            {
                _logger?.LogWarning(
                    "Example campaign digest could not extract text from {Tool} result (type {Type}); returning raw result.",
                    Name, result?.GetType().Name ?? "null");
                return result;
            }

            var state = _getWorkflowState?.Invoke();
            var useStructuralExcerpt = state != null
                && state.Artifacts.JourneyPatternPrepComplete
                && !state.Artifacts.JourneyPatternExampleFullResultShown;

            var digest = ExampleCampaignDigester.Digest(text, useStructuralExcerpt);
            if (!digest.Transformed)
                return result;

            if (useStructuralExcerpt && state != null)
                state.Artifacts.JourneyPatternExampleFullResultShown = true;

            _logger?.LogInformation(
                "Example campaign digest for {Tool}: {Original} -> {Digest} chars (structural={Structural}).",
                Name, digest.OriginalChars, digest.DigestChars, useStructuralExcerpt);

            return rewrap(digest.Json);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Example campaign digest failed for {Tool}; returning original result.", Name);
            return result;
        }
    }
}
