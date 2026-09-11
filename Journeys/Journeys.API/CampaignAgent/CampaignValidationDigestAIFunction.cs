using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Journeys.CampaignAgent.Remediation;
using Journeys.Core.Utility;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Journeys.API.CampaignAgent;

/// <summary>
/// Wraps validate_campaign: compact ack on clean/warning results; compact error summary on hard failures.
/// </summary>
internal sealed class CampaignValidationDigestAIFunction : DelegatingAIFunction
{
    private readonly bool _enabled;
    private readonly ILogger? _logger;

    public CampaignValidationDigestAIFunction(AIFunction innerFunction, bool enabled, ILogger? logger)
        : base(innerFunction)
    {
        _enabled = enabled;
        _logger = logger;
    }

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var fingerprint = CampaignPayloadFingerprint.TryCompute(ExtractCampaignJson(arguments));

        var result = await InnerFunction.InvokeAsync(arguments, cancellationToken).ConfigureAwait(false);
        if (!_enabled)
            return result;

        try
        {
            if (!TryExtractText(result, out var text, out var rewrap) || string.IsNullOrWhiteSpace(text))
            {
                _logger?.LogWarning(
                    "Campaign validation digest could not extract text from {Tool} result (type {Type}); returning raw result.",
                    Name, result?.GetType().Name ?? "null");
                return result;
            }

            var digest = CampaignValidationDigester.Digest(text, fingerprint);
            if (!digest.Transformed)
                return result;

            _logger?.LogInformation(
                "Campaign validation digest for {Tool}: {Original} -> {Digest} chars.",
                Name, digest.OriginalChars, digest.DigestChars);

            return rewrap(digest.Json);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Campaign validation digest failed for {Tool}; returning original result.", Name);
            return result;
        }
    }

    private static string? ExtractCampaignJson(AIFunctionArguments arguments)
    {
        if (arguments == null)
            return null;

        foreach (var key in new[] { "campaignJson", "CampaignJson" })
        {
            if (!arguments.TryGetValue(key, out var value) || value == null)
                continue;

            return value switch
            {
                string s => s,
                JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString(),
                _ => value.ToString()
            };
        }

        return null;
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
