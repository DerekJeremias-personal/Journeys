using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace Journeys.API.CampaignAgent;

public sealed class CampaignAgentTurnMetricsCollector
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly Stopwatch _turn = Stopwatch.StartNew();
    private long _loadHistoryMs, _promptComposeMs, _llmMs, _persistMs, _workflowSaveMs;
    private readonly List<ToolCallMetric> _tools = new();
    private readonly Dictionary<string, long> _toolStartTicks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _pendingToolNames = new(StringComparer.Ordinal);

    public void MarkLoadHistoryDone(long ms) => _loadHistoryMs = ms;
    public void AddPromptCompose(long ms) => _promptComposeMs += ms;
    public void AddLlm(long ms) => _llmMs += ms;
    public void AddPersist(long ms) => _persistMs += ms;
    public void AddWorkflowSave(long ms) => _workflowSaveMs += ms;

    public void ToolStarted(string callId, string tool)
    {
        _toolStartTicks[callId] = Stopwatch.GetTimestamp();
        _pendingToolNames[callId] = tool;
    }

    public void ToolFinished(string callId, string tool, string outcomeClass)
    {
        if (!_toolStartTicks.Remove(callId, out var start)) return;
        _pendingToolNames.Remove(callId);
        var ms = (long)((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
        _tools.Add(new ToolCallMetric(tool, callId, ms, outcomeClass));
    }

    public void ObserveStreamingUpdate(ChatResponseUpdate update)
    {
        foreach (var content in update.Contents)
        {
            if (content is FunctionCallContent fcc
                && !fcc.InformationalOnly
                && !string.IsNullOrEmpty(fcc.CallId))
            {
                ToolStarted(fcc.CallId, fcc.Name ?? "unknown");
            }
        }

        foreach (var content in update.Contents)
        {
            if (content is not FunctionResultContent frc || string.IsNullOrEmpty(frc.CallId))
                continue;

            var tool = _pendingToolNames.GetValueOrDefault(frc.CallId, "unknown");
            ToolFinished(frc.CallId, tool, ClassifyOutcome(frc.Result, frc.Exception));
        }
    }

    public string BuildJson(string requestId)
    {
        var total = _turn.ElapsedMilliseconds;
        var toolsMs = _tools.Sum(t => t.DurationMs);
        var payload = new
        {
            schemaVersion = 1,
            requestId,
            turnEndedAtUtc = DateTimeOffset.UtcNow,
            totalMs = total,
            spans = new
            {
                loadHistoryMs = _loadHistoryMs,
                promptComposeMs = _promptComposeMs,
                llmMs = _llmMs,
                toolsMs,
                persistMs = _persistMs,
                workflowSaveMs = _workflowSaveMs
            },
            toolCalls = _tools.Take(50).Select(t => new
            {
                tool = t.Tool,
                callId = t.CallId,
                durationMs = t.DurationMs,
                outcomeClass = t.OutcomeClass
            }),
            truncated = _tools.Count > 50 ? true : (bool?)null
        };
        return JsonSerializer.Serialize(payload, JsonOpts);
    }

    public int? GetToolDurationMs(string callId)
    {
        var metric = _tools.FirstOrDefault(t => t.CallId == callId);
        return metric is null ? null : (int)metric.DurationMs;
    }

    private static string ClassifyOutcome(object? result, Exception? exception)
    {
        if (exception is not null) return "exception";
        var text = result switch
        {
            null => string.Empty,
            string s => s,
            _ => JsonSerializer.Serialize(result, JsonOpts)
        };
        if (text.Contains("\"isError\":true", StringComparison.OrdinalIgnoreCase)
            || text.Contains("An error occurred invoking", StringComparison.OrdinalIgnoreCase))
            return "mcp_invocation_error";
        if (text.Contains("\"errors\"", StringComparison.Ordinal))
            return "validation_errors";
        return "ok";
    }

    private sealed record ToolCallMetric(string Tool, string CallId, long DurationMs, string OutcomeClass);
}
