using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Journeys.API.CampaignAgent;

internal static class CampaignAgentToolAuditRecorder
{
    private static readonly Regex BearerPattern = new(@"(Bearer\s+)[^\s""]+", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
    private static readonly Regex SkAntPattern = new(@"(sk-ant-)[A-Za-z0-9_-]{20,}", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    public static void RecordFromChatResponse(
        ChatResponse response,
        CampaignAgentToolAuditScope scope,
        ICampaignAgentToolAuditSink sink,
        JsonSerializerOptions jsonOpts,
        ILogger logger)
    {
        var pending = new Dictionary<string, (string Name, string ArgsJson)>(StringComparer.Ordinal);
        foreach (var msg in response.Messages)
        {
            if (msg.Role == ChatRole.Assistant)
            {
                foreach (var c in msg.Contents)
                {
                    if (c is FunctionCallContent fcc && !fcc.InformationalOnly && !string.IsNullOrEmpty(fcc.CallId))
                        pending[fcc.CallId] = (fcc.Name, SerializeArgs(fcc.Arguments, jsonOpts));
                }
            }
            else if (msg.Role == ChatRole.Tool)
            {
                foreach (var c in msg.Contents)
                {
                    if (c is not FunctionResultContent frc || string.IsNullOrEmpty(frc.CallId))
                        continue;
                    if (!pending.TryGetValue(frc.CallId, out var p))
                        continue;
                    pending.Remove(frc.CallId);
                    TryEnqueueCompleted(scope, sink, jsonOpts, logger, frc.CallId, p.Name, p.ArgsJson, frc.Result, frc.Exception, durationMs: null);
                }
            }
        }

        foreach (var kv in pending)
            logger.LogWarning("Campaign agent tool audit: unmatched tool call {CallId} ({Name}) after non-streaming response.", kv.Key, kv.Value.Name);
    }

    public static void FlushOrphans(
        Dictionary<string, CampaignAgentPendingToolCall> pending,
        CampaignAgentToolAuditScope scope,
        ICampaignAgentToolAuditSink sink,
        JsonSerializerOptions jsonOpts,
        ILogger logger)
    {
        foreach (var kv in pending)
        {
            logger.LogWarning(
                "Campaign agent tool audit: tool call {CallId} ({Name}) had no matching result before stream end.",
                kv.Key,
                kv.Value.ToolName);
            TryEnqueueCompleted(
                scope,
                sink,
                jsonOpts,
                logger,
                kv.Key,
                kv.Value.ToolName,
                kv.Value.ArgsJson,
                result: null,
                exception: new InvalidOperationException("Stream ended before tool result was observed."),
                durationMs: null);
        }

        pending.Clear();
    }

    public static void TryEnqueueCompleted(
        CampaignAgentToolAuditScope scope,
        ICampaignAgentToolAuditSink sink,
        JsonSerializerOptions jsonOpts,
        ILogger logger,
        string toolCallId,
        string toolName,
        string argsJson,
        object? result,
        Exception? exception,
        double? durationMs)
    {
        try
        {
            var eventId = Guid.NewGuid().ToString("N");
            var writtenAt = DateTimeOffset.UtcNow;
            var (compactBlob, previewBlob) = CampaignAgentToolAuditPath.BuildBlobNames(
                scope.RootPrefix,
                scope.TenantId,
                scope.OwnerUserId,
                scope.AuditDayUtc,
                scope.ConversationId);

            var outcome = exception is null ? "succeeded" : "failed";
            var argsBytes = Encoding.UTF8.GetByteCount(argsJson);
            var resultJson = SerializeResult(result, jsonOpts);
            var resultBytes = Encoding.UTF8.GetByteCount(resultJson);
            var argsSha = Sha256Hex(argsJson);
            var resultSha = Sha256Hex(resultJson);

            var compact = JsonSerializer.Serialize(
                new
                {
                    schemaVersion = 1,
                    eventId,
                    writtenAt,
                    tenantId = scope.TenantId,
                    ownerUserId = scope.OwnerUserId,
                    conversationId = scope.ConversationId,
                    requestId = scope.RequestId,
                    modelId = scope.ModelId,
                    toolCallId,
                    toolName,
                    durationMs,
                    outcome,
                    errorType = exception?.GetType().FullName,
                    errorMessage = Truncate(exception?.Message, 500),
                    argsBytes,
                    argsSha256 = argsSha,
                    resultBytes,
                    resultSha256 = resultSha
                },
                jsonOpts);

            var argsPreview = Truncate(Redact(argsJson), scope.MaxPreviewChars);
            var resultPreview = Truncate(Redact(resultJson), scope.MaxPreviewChars);
            var preview = JsonSerializer.Serialize(
                new
                {
                    schemaVersion = 1,
                    eventId,
                    writtenAt,
                    tenantId = scope.TenantId,
                    ownerUserId = scope.OwnerUserId,
                    conversationId = scope.ConversationId,
                    requestId = scope.RequestId,
                    toolCallId,
                    toolName,
                    argsPreview,
                    resultPreview
                },
                jsonOpts);

            var item = new CampaignAgentToolAuditBatchItem(compactBlob, previewBlob, compact, preview);
            if (!sink.TryEnqueue(item))
                logger.LogWarning("Campaign agent tool audit queue dropped event for tool {Tool} call {CallId}.", toolName, toolCallId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Campaign agent tool audit enqueue build failed for tool {Tool}.", toolName);
        }
    }

    private static string SerializeArgs(IDictionary<string, object?>? arguments, JsonSerializerOptions jsonOpts) =>
        arguments is null || arguments.Count == 0
            ? "{}"
            : JsonSerializer.Serialize(arguments, jsonOpts);

    private static string SerializeResult(object? result, JsonSerializerOptions jsonOpts) =>
        result switch
        {
            null => "null",
            string s => JsonSerializer.Serialize(s, jsonOpts),
            _ => JsonSerializer.Serialize(result, jsonOpts)
        };

    private static string Sha256Hex(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Redact(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        var t = BearerPattern.Replace(text, "$1[REDACTED]");
        t = SkAntPattern.Replace(t, "$1[REDACTED]");
        return t;
    }

    private static string Truncate(string? text, int maxChars)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        if (text.Length <= maxChars)
            return text;
        return text[..maxChars] + "…(truncated)";
    }
}
