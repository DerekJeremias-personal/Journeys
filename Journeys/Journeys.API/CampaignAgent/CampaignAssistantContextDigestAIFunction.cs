using System;
using System.Threading;
using System.Threading.Tasks;
using Journeys.Core.Utility;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Journeys.API.CampaignAgent;

internal sealed class CampaignAssistantContextDigestAIFunction : DelegatingAIFunction
{
    private readonly bool _enabled;
    private readonly ILogger? _logger;

    public CampaignAssistantContextDigestAIFunction(AIFunction innerFunction, bool enabled, ILogger? logger)
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
                    "Assistant context digest could not extract text from {Tool} result (type {Type}); returning raw result.",
                    Name, result?.GetType().Name ?? "null");
                return result;
            }

            var digest = CampaignAssistantContextDigester.Digest(text);
            if (!digest.Transformed)
                return result;

            _logger?.LogInformation(
                "Assistant context digest for {Tool}: {Original} -> {Digest} chars.",
                Name, digest.OriginalChars, digest.DigestChars);

            return rewrap(digest.Json);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Assistant context digest failed for {Tool}; returning original result.", Name);
            return result;
        }
    }
}
