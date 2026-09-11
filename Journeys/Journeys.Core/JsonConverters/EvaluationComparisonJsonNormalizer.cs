using System.Text.Json;

namespace Journeys.Core.JsonConverters;

/// <summary>
/// Rewrites legacy evaluator JSON (<c>evalType</c> / <c>EvalType</c>) to canonical <c>comparison</c>
/// before rule-tree deserialization.
/// </summary>
internal static class EvaluationComparisonJsonNormalizer
{
    internal static JsonElement Normalize(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => NormalizeObject(element),
            JsonValueKind.Array => NormalizeArray(element),
            _ => element
        };
    }

    private static JsonElement NormalizeObject(JsonElement element)
    {
        var properties = new List<(string Name, JsonElement Value)>();
        foreach (var prop in element.EnumerateObject())
            properties.Add((prop.Name, Normalize(prop.Value)));

        if (!HasComparison(properties) && TryGetEvalTypeAlias(properties, out var aliasValue))
            properties.Add(("comparison", aliasValue));

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var (name, value) in properties)
            {
                writer.WritePropertyName(name);
                value.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        using var doc = JsonDocument.Parse(stream.ToArray());
        return doc.RootElement.Clone();
    }

    private static JsonElement NormalizeArray(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var item in element.EnumerateArray())
                Normalize(item).WriteTo(writer);
            writer.WriteEndArray();
        }

        using var doc = JsonDocument.Parse(stream.ToArray());
        return doc.RootElement.Clone();
    }

    private static bool HasComparison(IEnumerable<(string Name, JsonElement Value)> properties) =>
        properties.Any(p =>
            p.Name.Equals("comparison", StringComparison.OrdinalIgnoreCase));

    private static bool TryGetEvalTypeAlias(
        IEnumerable<(string Name, JsonElement Value)> properties,
        out JsonElement aliasValue)
    {
        foreach (var prop in properties)
        {
            if (prop.Name.Equals("evalType", StringComparison.OrdinalIgnoreCase))
            {
                aliasValue = prop.Value;
                return true;
            }
        }

        aliasValue = default;
        return false;
    }
}
