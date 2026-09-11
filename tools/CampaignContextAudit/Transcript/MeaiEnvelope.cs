using System.Text.Json;

namespace CampaignContextAudit.Transcript;

public sealed record MeaiFunctionCall(string CallId, string Name, string? ArgumentsRaw);
public sealed record MeaiFunctionResult(string CallId, string? ResultRaw);

public static class MeaiEnvelope
{
    public static bool IsEnvelope(string? content)
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
        catch (JsonException) { return false; }
    }

    public static IReadOnlyList<MeaiFunctionCall> ExtractFunctionCalls(string? content)
    {
        var result = new List<MeaiFunctionCall>();
        if (!IsEnvelope(content)) return result;
        using var doc = JsonDocument.Parse(content!);
        if (!doc.RootElement.TryGetProperty("contents", out var contents) || contents.ValueKind != JsonValueKind.Array)
            return result;
        foreach (var item in contents.EnumerateArray())
        {
            if (!item.TryGetProperty("kind", out var k) || k.GetString() != "functionCall") continue;
            var callId = item.TryGetProperty("callId", out var c) ? c.GetString() ?? "" : "";
            var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            string? argsRaw = item.TryGetProperty("arguments", out var a) && a.ValueKind != JsonValueKind.Null
                ? a.GetRawText() : null;
            result.Add(new MeaiFunctionCall(callId, name, argsRaw));
        }
        return result;
    }

    public static string ExtractPlainText(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return "";
        if (!IsEnvelope(content)) return content;

        using var doc = JsonDocument.Parse(content!);
        if (!doc.RootElement.TryGetProperty("contents", out var contents) || contents.ValueKind != JsonValueKind.Array)
            return "";

        var parts = new List<string>();
        foreach (var item in contents.EnumerateArray())
        {
            if (!item.TryGetProperty("kind", out var k) || k.GetString() != "text") continue;
            if (item.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
            {
                var text = t.GetString();
                if (!string.IsNullOrEmpty(text))
                    parts.Add(text);
            }
        }

        return string.Join("\n", parts);
    }

    public static IReadOnlyList<MeaiFunctionResult> ExtractFunctionResults(string? content)
    {
        var result = new List<MeaiFunctionResult>();
        if (!IsEnvelope(content)) return result;
        using var doc = JsonDocument.Parse(content!);
        if (!doc.RootElement.TryGetProperty("contents", out var contents) || contents.ValueKind != JsonValueKind.Array)
            return result;
        foreach (var item in contents.EnumerateArray())
        {
            if (!item.TryGetProperty("kind", out var k) || k.GetString() != "functionResult") continue;
            var callId = item.TryGetProperty("callId", out var c) ? c.GetString() ?? "" : "";
            string? resRaw = item.TryGetProperty("result", out var r) && r.ValueKind != JsonValueKind.Null
                ? r.GetRawText() : null;
            result.Add(new MeaiFunctionResult(callId, resRaw));
        }
        return result;
    }
}
