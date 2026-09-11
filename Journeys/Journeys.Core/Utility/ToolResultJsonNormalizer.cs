using System.Text;
using System.Text.Json;

namespace Journeys.Core.Utility;

/// <summary>
/// Normalizes tool-result JSON before object-shaped parsing. MCP/MEAI transports deliver results in a
/// few shapes that hide the real payload from the workflow gates:
/// <list type="bullet">
/// <item>a double-encoded JSON string (a JSON string whose content is itself JSON), and</item>
/// <item>a MEAI content envelope — an object <c>{"$type":"text","text":"&lt;inner JSON&gt;"}</c>
/// (or an array of such text blocks) produced when <c>FunctionResultContent.Result</c> is a
/// <c>TextContent</c>.</item>
/// </list>
/// This unwraps those layers so downstream deserialization (event-model contract, PAT manifest, etc.)
/// sees the real object/array. Never throws.
/// </summary>
public static class ToolResultJsonNormalizer
{
    private const int MaxUnwrapDepth = 5;

    /// <summary>
    /// Unwraps double-encoded JSON strings and MEAI text-content envelopes, returning the innermost
    /// JSON payload. Returns the input unchanged when no known wrapper is present.
    /// </summary>
    public static string? Unwrap(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return json;

        var current = json;
        for (var depth = 0; depth < MaxUnwrapDepth; depth++)
        {
            string? next;
            try
            {
                next = TryUnwrapOnce(current);
            }
            catch (JsonException)
            {
                return current;
            }

            if (next is null || string.Equals(next, current, StringComparison.Ordinal))
                return current;

            current = next;
        }

        return current;
    }

    private static string? TryUnwrapOnce(string current)
    {
        using var doc = JsonDocument.Parse(current);
        var root = doc.RootElement;
        return root.ValueKind switch
        {
            JsonValueKind.String => UnwrapEncodedString(root),
            JsonValueKind.Object => TryUnwrapTextEnvelope(root),
            JsonValueKind.Array => TryUnwrapTextEnvelopeArray(root),
            _ => null
        };
    }

    private static string? UnwrapEncodedString(JsonElement stringEl)
    {
        var inner = stringEl.GetString() ?? string.Empty;
        return LooksLikeJson(inner) ? inner : null;
    }

    private static string? TryUnwrapTextEnvelope(JsonElement obj) =>
        TryReadTextEnvelope(obj, out var text) ? text : null;

    private static string? TryUnwrapTextEnvelopeArray(JsonElement arr)
    {
        var sb = new StringBuilder();
        var any = false;
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !TryReadTextEnvelope(item, out var text))
                return null;
            sb.Append(text);
            any = true;
        }

        return any ? sb.ToString() : null;
    }

    /// <summary>
    /// Recognizes a MEAI <c>TextContent</c> envelope: an object with <c>$type == "text"</c> and a
    /// string <c>text</c> property.
    /// </summary>
    private static bool TryReadTextEnvelope(JsonElement obj, out string? text)
    {
        text = null;
        if (!obj.TryGetProperty("$type", out var typeEl)
            || typeEl.ValueKind != JsonValueKind.String
            || !string.Equals(typeEl.GetString(), "text", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!obj.TryGetProperty("text", out var textEl) || textEl.ValueKind != JsonValueKind.String)
            return false;

        text = textEl.GetString() ?? string.Empty;
        return true;
    }

    private static bool LooksLikeJson(string value)
    {
        var trimmed = value.TrimStart();
        return trimmed.Length > 0 && (trimmed[0] == '{' || trimmed[0] == '[');
    }
}
