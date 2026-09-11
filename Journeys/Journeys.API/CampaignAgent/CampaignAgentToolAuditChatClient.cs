using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Journeys.API.CampaignAgent;

/// <summary>
/// Outermost <see cref="IChatClient"/> wrapper that records tool call → tool result latency and enqueues NDJSON audit lines.
/// </summary>
internal sealed class CampaignAgentToolAuditChatClient : IChatClient, IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IChatClient _inner;
    private readonly ICampaignAgentToolAuditSink _sink;
    private readonly CampaignAgentToolAuditScope _scope;
    private readonly ILogger<CampaignAgentToolAuditChatClient> _logger;

    public CampaignAgentToolAuditChatClient(
        IChatClient inner,
        ICampaignAgentToolAuditSink sink,
        CampaignAgentToolAuditScope scope,
        ILogger<CampaignAgentToolAuditChatClient> logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => _inner.GetService(serviceType, serviceKey);

    public void Dispose() => (_inner as IDisposable)?.Dispose();

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var response = await _inner.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        if (_scope.Enabled)
            CampaignAgentToolAuditRecorder.RecordFromChatResponse(response, _scope, _sink, JsonOpts, _logger);
        return response;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var pending = new Dictionary<string, CampaignAgentPendingToolCall>(StringComparer.Ordinal);
        await foreach (var update in _inner.GetStreamingResponseAsync(messages, options, cancellationToken).ConfigureAwait(false))
        {
            if (_scope.Enabled)
                ObserveStreamingUpdate(update, pending);
            yield return update;
        }

        if (_scope.Enabled)
            CampaignAgentToolAuditRecorder.FlushOrphans(pending, _scope, _sink, JsonOpts, _logger);
    }

    private void ObserveStreamingUpdate(ChatResponseUpdate update, Dictionary<string, CampaignAgentPendingToolCall> pending)
    {
        foreach (var c in update.Contents)
        {
            if (c is FunctionCallContent fcc && !fcc.InformationalOnly && !string.IsNullOrEmpty(fcc.CallId))
            {
                var argsJson = fcc.Arguments is null || fcc.Arguments.Count == 0
                    ? "{}"
                    : JsonSerializer.Serialize(fcc.Arguments, JsonOpts);
                pending[fcc.CallId] = new CampaignAgentPendingToolCall(fcc.Name, argsJson, Stopwatch.GetTimestamp());
            }
        }

        foreach (var c in update.Contents)
        {
            if (c is not FunctionResultContent frc || string.IsNullOrEmpty(frc.CallId))
                continue;
            if (!pending.TryGetValue(frc.CallId, out var started))
                continue;

            pending.Remove(frc.CallId);
            var elapsedMs = (Stopwatch.GetTimestamp() - started.StartTimestamp) * 1000.0 / Stopwatch.Frequency;
            CampaignAgentToolAuditRecorder.TryEnqueueCompleted(
                _scope,
                _sink,
                JsonOpts,
                _logger,
                frc.CallId,
                started.ToolName,
                started.ArgsJson,
                frc.Result,
                frc.Exception,
                elapsedMs);
        }
    }
}
