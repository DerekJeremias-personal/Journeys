using System;
using System.Threading;
using System.Threading.Tasks;
using Journeys.API.CampaignAgent.Workflow;
using Journeys.CampaignAgent.Remediation;
using Journeys.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Journeys.API.CampaignAgent;

internal sealed class RulePatternRecipesDigestAIFunction : DelegatingAIFunction
{
    private readonly bool _enabled;
    private readonly ILogger? _logger;
    private readonly Func<CampaignWorkflowState>? _getWorkflowState;

    public RulePatternRecipesDigestAIFunction(
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
                    "Pattern recipes digest could not extract text from {Tool} result (type {Type}); returning raw result.",
                    Name, result?.GetType().Name ?? "null");
                return result;
            }

            var state = _getWorkflowState?.Invoke();
            string? fullPatternId = null;
            if (state != null
                && state.Artifacts.JourneyPatternPrepComplete
                && !state.Artifacts.JourneyPatternRecipesFullResultShown
                && !string.IsNullOrWhiteSpace(state.Artifacts.JourneyPatternId))
            {
                fullPatternId = state.Artifacts.JourneyPatternId;
            }

            var digest = RulePatternRecipesDigester.Digest(text, fullPatternId);
            if (!digest.Transformed)
                return result;

            if (fullPatternId != null && state != null)
                state.Artifacts.JourneyPatternRecipesFullResultShown = true;

            _logger?.LogInformation(
                "Pattern recipes digest for {Tool}: {Original} -> {Digest} chars (fullSkeleton={Full}).",
                Name, digest.OriginalChars, digest.DigestChars, fullPatternId ?? "(none)");

            return rewrap(digest.Json);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Pattern recipes digest failed for {Tool}; returning original result.", Name);
            return result;
        }
    }
}
