using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Journeys.CampaignAgent.Remediation;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Journeys.API.CampaignAgent;

/// <summary>
/// Wraps the upsert_campaign tool. Delegates metadata to the inner function and digests SUCCESSFUL
/// results via <see cref="CampaignMutationDigester"/> (failures pass through) so the model view,
/// persistence, and the history budget all see the compact ack. Fail-open on any problem.
/// </summary>
internal sealed class CampaignMutationDigestAIFunction : DelegatingAIFunction
{
    private readonly bool _enabled;
    private readonly ILogger? _logger;

    public CampaignMutationDigestAIFunction(AIFunction innerFunction, bool enabled, ILogger? logger)
        : base(innerFunction)
    {
        _enabled = enabled;
        _logger = logger;
    }

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        // The inner tool invocation stays outside the try/catch: its own failures must propagate.
        var result = await InnerFunction.InvokeAsync(arguments, cancellationToken).ConfigureAwait(false);
        if (!_enabled)
            return result;

        try
        {
            if (!TryExtractText(result, out var text, out var rewrap) || string.IsNullOrWhiteSpace(text))
            {
                _logger?.LogWarning(
                    "Campaign mutation digest could not extract text from {Tool} result (type {Type}); returning raw result.",
                    Name, result?.GetType().Name ?? "null");
                return result;
            }

            var digest = CampaignMutationDigester.Digest(text);
            if (!digest.Transformed)
                return result; // failure or non-campaign success: pass through (expected, not warned)

            _logger?.LogInformation(
                "Campaign mutation digest for {Tool}: {Original} -> {Digest} chars.",
                Name, digest.OriginalChars, digest.DigestChars);

            return rewrap(digest.Json);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Campaign mutation digest failed for {Tool}; returning original result.", Name);
            return result;
        }
    }

    private static bool TryExtractText(object? result, out string text, out Func<string, object?> rewrap)
    {
        switch (result)
        {
            case string s:
                text = s; rewrap = static d => d; return true;
            case JsonElement je:
                text = je.ValueKind == JsonValueKind.String ? je.GetString() ?? string.Empty : je.GetRawText();
                rewrap = static d => d; return true;
            case TextContent tc:
                text = tc.Text ?? string.Empty; rewrap = static d => new TextContent(d); return true;
            case IEnumerable<AIContent> contents:
            {
                // Intentionally lossy: only TextContent items are considered and the rewrap collapses
                // the result to a single TextContent. Mutation results are not expected to carry
                // non-text contents, so dropping them here is acceptable.
                var texts = contents.OfType<TextContent>().ToList();
                if (texts.Count == 0) { text = string.Empty; rewrap = static d => d; return false; }
                text = string.Concat(texts.Select(t => t.Text));
                rewrap = d => new List<AIContent> { new TextContent(d) };
                return true;
            }
            default:
                text = string.Empty; rewrap = static d => d; return false;
        }
    }
}
