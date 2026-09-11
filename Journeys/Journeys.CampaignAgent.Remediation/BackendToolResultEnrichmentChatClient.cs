using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Journeys.CampaignAgent.Remediation;

/// <summary>
/// Enriches failed Backend MCP tool results (e.g. SaveModel validation) with <c>_agentRemediation</c> hints for the model.
/// Register inside the audit wrapper so audits reflect enriched payloads.
/// </summary>
public sealed class BackendToolResultEnrichmentChatClient : IChatClient, IDisposable
{
    private readonly IChatClient _inner;
    private readonly BackendToolRemediationOptions _options;
    private readonly ILogger<BackendToolResultEnrichmentChatClient> _logger;
    private readonly HashSet<string> _allowlist;
    private readonly HashSet<string> _journeysAllowlist;

    public BackendToolResultEnrichmentChatClient(
        IChatClient inner,
        IOptions<BackendToolRemediationOptions> options,
        ILogger<BackendToolResultEnrichmentChatClient> logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _allowlist = new HashSet<string>(_options.ToolAllowlist ?? [], StringComparer.OrdinalIgnoreCase);
        _journeysAllowlist = new HashSet<string>(_options.JourneysToolAllowlist ?? [], StringComparer.OrdinalIgnoreCase);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        _inner.GetService(serviceType, serviceKey);

    public void Dispose() => (_inner as IDisposable)?.Dispose();

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var pending = BuildPendingToolNamesFromHistory(messages);
        var enrichments = 0;
        var enrichedCallIds = new HashSet<string>(StringComparer.Ordinal);

        var response = await _inner.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        if (response.Messages is not { Count: > 0 } msgs)
            return response;

        var enrichedMessages = new List<ChatMessage>();
        foreach (var msg in msgs)
        {
            TrackCalls(msg, pending);
            enrichedMessages.Add(EnrichMessage(msg, pending, enrichedCallIds, ref enrichments));
        }

        return new ChatResponse(enrichedMessages) { ModelId = response.ModelId, CreatedAt = response.CreatedAt };
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var pending = BuildPendingToolNamesFromHistory(messages);
        var enrichments = 0;
        var enrichedCallIds = new HashSet<string>(StringComparer.Ordinal);

        await foreach (var update in _inner.GetStreamingResponseAsync(messages, options, cancellationToken).ConfigureAwait(false))
        {
            foreach (var c in update.Contents)
            {
                if (c is FunctionCallContent fcc && !string.IsNullOrEmpty(fcc.CallId))
                    pending[fcc.CallId] = fcc.Name;
            }

            yield return EnrichStreamingUpdate(update, pending, enrichedCallIds, ref enrichments);
        }
    }

    private static Dictionary<string, string> BuildPendingToolNamesFromHistory(IEnumerable<ChatMessage> messages)
    {
        var pending = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var msg in messages)
            TrackCalls(msg, pending);
        return pending;
    }

    private static void TrackCalls(ChatMessage msg, Dictionary<string, string> pending)
    {
        if (msg.Contents is null)
            return;

        foreach (var c in msg.Contents)
        {
            if (c is FunctionCallContent fcc && !string.IsNullOrEmpty(fcc.CallId))
                pending[fcc.CallId] = fcc.Name;
        }
    }

    private ChatMessage EnrichMessage(
        ChatMessage msg,
        Dictionary<string, string> pending,
        HashSet<string> enrichedCallIds,
        ref int enrichments)
    {
        if (msg.Contents is null || msg.Contents.Count == 0)
            return msg;

        var contents = new List<AIContent>();
        var changed = false;
        foreach (var c in msg.Contents)
        {
            if (c is FunctionResultContent frc)
            {
                var enriched = TryEnrichFunctionResult(frc, pending, enrichedCallIds, ref enrichments);
                contents.Add(enriched ?? frc);
                if (enriched is not null)
                    changed = true;
            }
            else
                contents.Add(c);
        }

        return changed
            ? new ChatMessage(msg.Role, contents) { AuthorName = msg.AuthorName, CreatedAt = msg.CreatedAt }
            : msg;
    }

    private ChatResponseUpdate EnrichStreamingUpdate(
        ChatResponseUpdate update,
        Dictionary<string, string> pending,
        HashSet<string> enrichedCallIds,
        ref int enrichments)
    {
        if (update.Contents is null || update.Contents.Count == 0)
            return update;

        var contents = new List<AIContent>();
        var changed = false;
        foreach (var c in update.Contents)
        {
            if (c is FunctionResultContent frc)
            {
                var enriched = TryEnrichFunctionResult(frc, pending, enrichedCallIds, ref enrichments);
                contents.Add(enriched ?? frc);
                if (enriched is not null)
                    changed = true;
            }
            else
                contents.Add(c);
        }

        if (!changed)
            return update;

        return new ChatResponseUpdate(update.Role, contents)
        {
            AuthorName = update.AuthorName,
            CreatedAt = update.CreatedAt,
            ConversationId = update.ConversationId,
            FinishReason = update.FinishReason,
            MessageId = update.MessageId,
            ModelId = update.ModelId,
            ResponseId = update.ResponseId
        };
    }

    private FunctionResultContent? TryEnrichFunctionResult(
        FunctionResultContent frc,
        Dictionary<string, string> pending,
        HashSet<string> enrichedCallIds,
        ref int enrichments)
    {
        if (!_options.Enabled || enrichments >= _options.MaxEnrichmentsPerTurn)
            return null;

        if (string.IsNullOrEmpty(frc.CallId) || !enrichedCallIds.Add(frc.CallId))
            return null;

        var toolName = pending.TryGetValue(frc.CallId, out var n) ? n : "";
        if (string.IsNullOrEmpty(toolName))
            return null;

        var useJourneysCatalog = false;
        if (!_allowlist.Contains(toolName))
        {
            if (!_journeysAllowlist.Contains(toolName))
                return null;
            useJourneysCatalog = true;
        }

        var json = BackendToolResultEnricher.SerializeResultToJson(frc.Result);
        BackendToolFailure? failure = null;
        if (!BackendToolResultInterpreter.TryParse(toolName, json, out failure) || failure is null)
        {
            if (useJourneysCatalog)
            {
                var advisory = JourneysToolRemediationCatalog.TryBuildValidateAdvisory(toolName, json);
                if (advisory is not null
                    && BackendToolResultEnricher.TryEnrich(json, advisory, out var advisoryJson)
                    && advisoryJson is not null)
                {
                    enrichments++;
                    _logger.LogInformation(
                        "Backend tool remediation applied for {Tool} (callId={CallId}, rules={Rules})",
                        toolName, frc.CallId, string.Join(",", advisory.MatchedRules));
                    return new FunctionResultContent(frc.CallId, advisoryJson) { Exception = frc.Exception };
                }
            }

            if (!McpHostFailureParser.TryParse(toolName, json, out failure) || failure is null)
                return null;
        }

        var payload = useJourneysCatalog
            ? JourneysToolRemediationCatalog.Build(toolName, failure)
            : BackendModelValidationRemediationCatalog.Build(toolName, failure);
        if (payload is null)
            return null;

        if (!BackendToolResultEnricher.TryEnrich(json, payload, out var enrichedJson) || enrichedJson is null)
            return null;

        enrichments++;

        _logger.LogInformation(
            "Backend tool remediation applied for {Tool} (callId={CallId}, rules={Rules})",
            toolName,
            frc.CallId,
            string.Join(",", payload.MatchedRules));

        return new FunctionResultContent(frc.CallId, enrichedJson) { Exception = frc.Exception };
    }
}
