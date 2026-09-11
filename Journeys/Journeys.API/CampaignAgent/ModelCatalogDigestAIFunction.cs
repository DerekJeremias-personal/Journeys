using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Journeys.Core.Utility;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Journeys.API.CampaignAgent;

/// <summary>
/// Wraps a catalog tool (get_all_models / list_models). Optionally injects a tag filter before invoke,
/// then digests the result via <see cref="ModelCatalogDigester"/> so the model view, persistence, and the
/// history budget all see the compact version. Fail-open: returns the inner result unchanged on any
/// problem extracting/transforming text.
/// </summary>
internal sealed class ModelCatalogDigestAIFunction : DelegatingAIFunction
{
    private readonly string? _forcedTag;
    private readonly bool _enabled;
    private readonly ILogger? _logger;

    public ModelCatalogDigestAIFunction(AIFunction innerFunction, string? forcedTag, bool enabled, ILogger? logger)
        : base(innerFunction)
    {
        _forcedTag = forcedTag;
        _enabled = enabled;
        _logger = logger;
    }

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var invokeArgs = TryInjectTag(arguments, out var merged) ? merged : arguments;

        // The inner tool invocation stays outside the try/catch: its own failures must propagate
        // unchanged. Only the digest post-processing below is guarded so it can never break a live
        // tool call.
        var result = await InnerFunction.InvokeAsync(invokeArgs, cancellationToken).ConfigureAwait(false);
        if (!_enabled)
            return result;

        try
        {
            if (!TryExtractText(result, out var text, out var rewrap) || string.IsNullOrWhiteSpace(text))
            {
                _logger?.LogWarning(
                    "Model catalog digest could not extract text from {Tool} result (type {Type}); returning raw result.",
                    Name, result?.GetType().Name ?? "null");
                return result;
            }

            var digest = ModelCatalogDigester.Digest(text, _forcedTag);
            if (!digest.Transformed)
            {
                _logger?.LogWarning(
                    "Model catalog digest did not transform {Tool} result (len {Len}); shape unrecognized, returning raw result.",
                    Name, text.Length);
                return result;
            }

            _logger?.LogInformation(
                "Model catalog digest for {Tool}: {Original} -> {Digest} chars, {Slim}/{Total} models slimmed.",
                Name, digest.OriginalChars, digest.DigestChars, digest.RetainedCount, digest.ModelCount);

            return rewrap(digest.Json);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Model catalog digest failed for {Tool}; returning original result.", Name);
            return result;
        }
    }

    private bool TryInjectTag(AIFunctionArguments arguments, out AIFunctionArguments merged)
    {
        merged = arguments;
        if (string.IsNullOrWhiteSpace(_forcedTag))
            return false;

        foreach (var kv in arguments)
        {
            if (string.Equals(kv.Key, "tag", StringComparison.OrdinalIgnoreCase)
                && kv.Value is string s
                && !string.IsNullOrWhiteSpace(s))
            {
                return false;
            }
        }

        try
        {
            merged = new AIFunctionArguments();
            foreach (var kv in arguments)
                merged[kv.Key] = kv.Value;
            merged["tag"] = _forcedTag;
            _logger?.LogDebug("Injected tag={Tag} into {Tool} arguments.", _forcedTag, Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to inject tag into {Tool}; calling without tag.", Name);
            merged = arguments;
            return false;
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
                // the result to a single TextContent. Catalog results are not expected to carry
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
