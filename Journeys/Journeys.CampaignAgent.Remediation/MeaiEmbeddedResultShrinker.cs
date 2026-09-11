using System.Text.Json;
using System.Text.Json.Nodes;

namespace Journeys.CampaignAgent.Remediation;

/// <summary>Read-time shrink for oversized <c>functionResult</c> entries inside persisted MEAI assistant envelopes.</summary>
public static class MeaiEmbeddedResultShrinker
{
    private static readonly JsonSerializerOptions CompactWrite = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static bool IsMeaiEnvelope(string? content)
    {
        if (string.IsNullOrWhiteSpace(content) || !content.TrimStart().StartsWith('{'))
            return false;
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            return root.TryGetProperty("v", out var v) && v.TryGetInt32(out var ver) && ver == 1
                   && root.TryGetProperty("meai", out var meai) && meai.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Stub one embedded result over <paramref name="threshold"/> chars; lowest shrink priority first.</summary>
    public static bool TryShrinkOneResult(
        string content,
        int threshold,
        IReadOnlyList<string> shrinkPriorityTools,
        out string updated,
        IReadOnlySet<string>? skipCallIds = null)
    {
        updated = content;
        if (!IsMeaiEnvelope(content) || threshold <= 0)
            return false;

        JsonNode? rootNode;
        try
        {
            rootNode = JsonNode.Parse(content);
        }
        catch (JsonException)
        {
            return false;
        }

        if (rootNode is not JsonObject root
            || root["contents"] is not JsonArray contents)
            return false;

        var callIdToToolName = BuildCallIdToToolName(contents);
        var candidates = new List<(int Index, int Priority, int Size, string CallId, string? ToolName)>();

        for (var i = 0; i < contents.Count; i++)
        {
            if (contents[i] is not JsonObject item)
                continue;
            if (!string.Equals(item["kind"]?.GetValue<string>(), "functionResult", StringComparison.Ordinal))
                continue;

            var callId = item["callId"]?.GetValue<string>() ?? "";
            if (!string.IsNullOrEmpty(callId) && skipCallIds?.Contains(callId) == true)
                continue;

            var resultNode = item["result"];
            if (resultNode is null)
                continue;

            var raw = resultNode.ToJsonString(CompactWrite);
            if (raw.Length <= threshold || raw.Contains("\"historyStub\":true", StringComparison.Ordinal))
                continue;

            callIdToToolName.TryGetValue(callId, out var toolName);
            candidates.Add((i, ShrinkPriority(toolName, shrinkPriorityTools), raw.Length, callId, toolName));
        }

        if (candidates.Count == 0)
            return false;

        var pick = candidates.OrderBy(c => c.Priority).ThenByDescending(c => c.Size).First();
        var stubJson = ToolResultHistoryStub.Build(pick.ToolName, pick.Size);
        try
        {
            contents[pick.Index]!["result"] = JsonNode.Parse(stubJson);
        }
        catch (JsonException)
        {
            return false;
        }

        updated = root.ToJsonString(CompactWrite);
        return true;
    }

    /// <summary>
    /// Effective char count for assistant MEAI content — skips embedded results duplicated on tool rows.
    /// </summary>
    public static int CountEffectiveAssistantContentChars(string? content, IReadOnlySet<string> toolCallIdsOnRows)
    {
        if (string.IsNullOrEmpty(content))
            return 0;

        if (!IsMeaiEnvelope(content))
            return content.Length;

        JsonNode? rootNode;
        try
        {
            rootNode = JsonNode.Parse(content);
        }
        catch (JsonException)
        {
            return content.Length;
        }

        if (rootNode is not JsonObject root
            || root["contents"] is not JsonArray contents)
            return content.Length;

        var total = 0;
        foreach (var node in contents)
        {
            if (node is not JsonObject item)
                continue;

            var kind = item["kind"]?.GetValue<string>();
            switch (kind)
            {
                case "text":
                    total += item["text"]?.GetValue<string>()?.Length ?? 0;
                    break;
                case "functionCall":
                    total += item["arguments"]?.ToJsonString(CompactWrite).Length ?? 0;
                    break;
                case "functionResult":
                    var callId = item["callId"]?.GetValue<string>() ?? "";
                    if (!string.IsNullOrEmpty(callId) && toolCallIdsOnRows.Contains(callId))
                        break;
                    total += item["result"]?.ToJsonString(CompactWrite).Length ?? 0;
                    break;
                default:
                    total += item.ToJsonString(CompactWrite).Length;
                    break;
            }
        }

        return total;
    }

    public static bool EnvelopeContainsFunctionResultKind(string? content)
    {
        if (!IsMeaiEnvelope(content))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(content!);
            if (!doc.RootElement.TryGetProperty("contents", out var contents)
                || contents.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var item in contents.EnumerateArray())
            {
                if (item.TryGetProperty("kind", out var k)
                    && string.Equals(k.GetString(), "functionResult", StringComparison.Ordinal))
                    return true;
            }
        }
        catch (JsonException)
        {
            // ignore
        }

        return false;
    }

    public static bool TryGetLargestEmbeddedResultSize(
        string content,
        int threshold,
        bool requireOverThreshold,
        out int size,
        out string? toolName,
        IReadOnlySet<string>? skipCallIds = null)
    {
        size = 0;
        toolName = null;
        if (!IsMeaiEnvelope(content))
            return false;

        JsonNode? rootNode;
        try
        {
            rootNode = JsonNode.Parse(content);
        }
        catch (JsonException)
        {
            return false;
        }

        if (rootNode is not JsonObject root || root["contents"] is not JsonArray contents)
            return false;

        var callIdToToolName = BuildCallIdToToolName(contents);
        var found = false;
        foreach (var node in contents)
        {
            if (node is not JsonObject item)
                continue;
            if (!string.Equals(item["kind"]?.GetValue<string>(), "functionResult", StringComparison.Ordinal))
                continue;

            var callId = item["callId"]?.GetValue<string>() ?? "";
            if (!string.IsNullOrEmpty(callId) && skipCallIds?.Contains(callId) == true)
                continue;

            var resultNode = item["result"];
            if (resultNode is null)
                continue;

            var raw = resultNode.ToJsonString(CompactWrite);
            if (raw.Contains("\"historyStub\":true", StringComparison.Ordinal))
                continue;
            if (requireOverThreshold && raw.Length <= threshold)
                continue;

            if (!found || raw.Length > size)
            {
                found = true;
                size = raw.Length;
                callIdToToolName.TryGetValue(callId, out toolName);
            }
        }

        return found;
    }

    /// <summary>Truncate the largest MEAI text or functionCall block when history remains over budget.</summary>
    public static bool TryTruncateOneContentBlock(string content, int maxTextChars, out string updated, int minCharsToTruncate = 2048)
    {
        updated = content;
        if (!IsMeaiEnvelope(content) || maxTextChars <= 0)
            return false;

        JsonNode? rootNode;
        try
        {
            rootNode = JsonNode.Parse(content);
        }
        catch (JsonException)
        {
            return false;
        }

        if (rootNode is not JsonObject root || root["contents"] is not JsonArray contents)
            return false;

        var pickIndex = -1;
        var pickLen = 0;
        var pickIsCall = false;
        for (var i = 0; i < contents.Count; i++)
        {
            if (contents[i] is not JsonObject item)
                continue;

            var kind = item["kind"]?.GetValue<string>();
            int len;
            switch (kind)
            {
                case "text":
                    len = item["text"]?.GetValue<string>()?.Length ?? 0;
                    break;
                case "functionCall":
                    len = item["arguments"]?.ToJsonString(CompactWrite).Length ?? 0;
                    break;
                default:
                    continue;
            }

            if (len <= minCharsToTruncate || len <= pickLen)
                continue;

            pickIndex = i;
            pickLen = len;
            pickIsCall = kind == "functionCall";
        }

        if (pickIndex < 0)
            return false;

        if (pickIsCall)
        {
            var args = contents[pickIndex]!["arguments"];
            var raw = args?.ToJsonString(CompactWrite) ?? "{}";
            if (raw.Length <= minCharsToTruncate)
                return false;
            contents[pickIndex]!["arguments"] = JsonNode.Parse(
                "{\"_historyTrimmed\":true,\"_originalChars\":" + raw.Length + "}");
        }
        else
        {
            var text = contents[pickIndex]!["text"]?.GetValue<string>() ?? "";
            contents[pickIndex]!["text"] = TrimTextBlock(text, maxTextChars, minCharsToTruncate);
        }

        updated = root.ToJsonString(CompactWrite);
        return true;
    }

    /// <inheritdoc cref="TryTruncateOneContentBlock"/>
    public static bool TryTruncateOneTextBlock(string content, int maxTextChars, out string updated, int minTextCharsToTruncate = 2048) =>
        TryTruncateOneContentBlock(content, maxTextChars, out updated, minTextCharsToTruncate);

    private static string TrimTextBlock(string text, int maxTextChars, int minFloor = 256)
    {
        if (text.Length <= minFloor)
            return text;

        var target = text.Length > maxTextChars ? maxTextChars : Math.Max(minFloor, text.Length / 2);
        if (target >= text.Length)
            return text;

        return text[..target] + "\n\n[historyTextTrimmed: narrative shortened for history budget]";
    }

    internal static Dictionary<string, string> BuildCallIdToToolName(JsonArray contents)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var node in contents)
        {
            if (node is not JsonObject item)
                continue;
            if (!string.Equals(item["kind"]?.GetValue<string>(), "functionCall", StringComparison.Ordinal))
                continue;

            var callId = item["callId"]?.GetValue<string>();
            var name = item["name"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(callId) && !string.IsNullOrEmpty(name))
                map[callId] = name;
        }

        return map;
    }

    internal static int ShrinkPriority(string? toolName, IReadOnlyList<string> shrinkPriorityTools)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            return shrinkPriorityTools.Count;

        for (var i = 0; i < shrinkPriorityTools.Count; i++)
        {
            if (string.Equals(toolName, shrinkPriorityTools[i], StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return shrinkPriorityTools.Count;
    }
}
