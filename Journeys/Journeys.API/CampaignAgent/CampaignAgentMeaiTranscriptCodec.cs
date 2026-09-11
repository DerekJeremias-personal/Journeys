using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace Journeys.API.CampaignAgent;

/// <summary>
/// Round-trips <see cref="ChatMessage"/> instances produced by MEAI tool-calling (assistant + tool roles)
/// for CampaignAgent Cosmos persistence. Legacy threads use plain-text assistant rows without this envelope.
/// </summary>
internal static class CampaignAgentMeaiTranscriptCodec
{
    public const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonWrite = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions JsonRead = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Detects persisted MEAI transcript JSON (assistant rows with structured contents).
    /// </summary>
    public static bool IsMeaiTranscriptEnvelope(string? content)
    {
        if (string.IsNullOrWhiteSpace(content) || !content.TrimStart().StartsWith('{'))
            return false;
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            return root.TryGetProperty("v", out var v) && v.TryGetInt32(out var ver) && ver == SchemaVersion
                   && root.TryGetProperty("meai", out var meai) && meai.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Serializes a <see cref="ChatMessage"/> from the completion response to a single string stored in <see cref="Journeys.Core.Models.AgentMessage.Content"/>.
    /// </summary>
    public static string SerializeTranscriptMessage(ChatMessage message)
    {
        var contents = new JsonArray();
        foreach (var c in message.Contents)
            contents.Add(SerializeContent(c));

        var root = new JsonObject
        {
            ["v"] = SchemaVersion,
            ["meai"] = true,
            ["role"] = message.Role.ToString(),
            ["messageId"] = message.MessageId,
            ["contents"] = contents
        };

        return root.ToJsonString(JsonWrite);
    }

    public static ChatMessage DeserializeTranscriptMessage(string content)
    {
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        if (!root.TryGetProperty("role", out var roleEl))
            throw new JsonException("Missing role.");
        var role = new ChatRole(roleEl.GetString() ?? "assistant");
        string? messageId = root.TryGetProperty("messageId", out var mid) && mid.ValueKind == JsonValueKind.String
            ? mid.GetString()
            : null;

        if (!root.TryGetProperty("contents", out var contentsEl) || contentsEl.ValueKind != JsonValueKind.Array)
            throw new JsonException("Missing contents array.");

        var list = new List<AIContent>();
        foreach (var item in contentsEl.EnumerateArray())
            list.Add(DeserializeContent(item));

        var msg = new ChatMessage(role, list);
        if (!string.IsNullOrEmpty(messageId))
            msg.MessageId = messageId;
        return msg;
    }

    private static JsonObject SerializeContent(AIContent content)
    {
        switch (content)
        {
            case TextContent tc:
                return new JsonObject
                {
                    ["kind"] = "text",
                    ["text"] = tc.Text
                };
            case FunctionCallContent fc:
                return new JsonObject
                {
                    ["kind"] = "functionCall",
                    ["callId"] = fc.CallId,
                    ["name"] = fc.Name,
                    ["informationalOnly"] = fc.InformationalOnly,
                    ["arguments"] = fc.Arguments is null
                        ? null
                        : JsonSerializer.SerializeToNode(fc.Arguments, JsonWrite)
                };
            case FunctionResultContent fr:
                return new JsonObject
                {
                    ["kind"] = "functionResult",
                    ["callId"] = fr.CallId,
                    ["result"] = JsonSerializer.SerializeToNode(fr.Result, JsonWrite)
                };
            default:
                return new JsonObject
                {
                    ["kind"] = "unsupported",
                    ["type"] = content.GetType().FullName ?? content.GetType().Name,
                    ["text"] = content.ToString()
                };
        }
    }

    private static AIContent DeserializeContent(JsonElement el)
    {
        if (!el.TryGetProperty("kind", out var kindEl))
            return new TextContent(el.GetRawText());
        var kind = kindEl.GetString();
        return kind switch
        {
            "text" => new TextContent(el.TryGetProperty("text", out var t) ? t.GetString() : ""),
            "functionCall" => new FunctionCallContent(
                el.GetProperty("callId").GetString() ?? "",
                el.GetProperty("name").GetString() ?? "",
                DeserializeArguments(el)),
            "functionResult" => new FunctionResultContent(
                el.GetProperty("callId").GetString() ?? "",
                DeserializeResult(el.GetProperty("result"))),
            "unsupported" => new TextContent(el.TryGetProperty("text", out var ut) ? ut.GetString() ?? "" : ""),
            _ => new TextContent(el.GetRawText())
        };
    }

    private static IDictionary<string, object?>? DeserializeArguments(JsonElement el)
    {
        if (!el.TryGetProperty("arguments", out var args) || args.ValueKind == JsonValueKind.Null)
            return null;
        if (args.ValueKind != JsonValueKind.Object)
            return null;

        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var prop in args.EnumerateObject())
            dict[prop.Name] = prop.Value.Clone();
        return dict;
    }

    private static object? DeserializeResult(JsonElement el) =>
        el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Object or JsonValueKind.Array => el.Clone(),
            _ => el.GetRawText()
        };

    /// <summary>
    /// Builds a <see cref="ChatMessage"/> for a persisted tool row (<see cref="Journeys.Core.Models.AgentMessage.Role"/> = tool).
    /// </summary>
    public static ChatMessage DeserializeToolRow(string? toolCallId, string? toolResultJson)
    {
        if (string.IsNullOrWhiteSpace(toolCallId))
            throw new InvalidOperationException("toolCallId is required for tool role replay.");
        object? result = string.IsNullOrWhiteSpace(toolResultJson)
            ? null
            : DeserializeToolResultPayload(toolResultJson);
        return new ChatMessage(ChatRole.Tool, [new FunctionResultContent(toolCallId, result)]);
    }

    private static object? DeserializeToolResultPayload(string json)
    {
        json = json.Trim();
        if (json.Length == 0)
            return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return DeserializeResult(doc.RootElement);
        }
        catch (JsonException)
        {
            return json;
        }
    }
}
