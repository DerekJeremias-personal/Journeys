using System;
using System.Threading;
using System.Threading.Tasks;
using Journeys.CampaignAgent.Remediation;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Journeys.API.CampaignAgent;

internal sealed class RulesEngineContractDigestAIFunction : DelegatingAIFunction
{
    private readonly bool _enabled;
    private readonly ILogger? _logger;

    public RulesEngineContractDigestAIFunction(AIFunction innerFunction, bool enabled, ILogger? logger)
        : base(innerFunction)
    {
        _enabled = enabled;
        _logger = logger;
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
                    "Rules contract digest could not extract text from {Tool} result (type {Type}); returning raw result.",
                    Name, result?.GetType().Name ?? "null");
                return result;
            }

            var digest = RulesEngineContractDigester.Digest(text);
            if (!digest.Transformed)
                return result;

            _logger?.LogInformation(
                "Rules contract digest for {Tool}: {Original} -> {Digest} chars.",
                Name, digest.OriginalChars, digest.DigestChars);

            return rewrap(digest.Json);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Rules contract digest failed for {Tool}; returning original result.", Name);
            return result;
        }
    }
}
