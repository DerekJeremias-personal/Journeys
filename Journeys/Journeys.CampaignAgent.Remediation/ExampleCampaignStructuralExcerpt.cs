using System.Text.Json;

namespace Journeys.CampaignAgent.Remediation;

/// <summary>Extracts navigation + first tier child structure from example campaigns for journey prep carve-out.</summary>
public static class ExampleCampaignStructuralExcerpt
{
    public const string Note =
        "Structural excerpt for journey authoring — full example suppressed after first fetch.";

    private const int MaxChars = 4000;

    private static readonly JsonSerializerOptions CompactOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static string? TryBuild(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return null;

        JsonDocument doc;
        try { doc = JsonDocument.Parse(rawJson); }
        catch (JsonException) { return null; }

        using (doc)
        {
            var root = doc.RootElement;
            if (!TryGetProperty(root, "journey", out var journey)
                && !TryGetProperty(root, "Journey", out journey))
            {
                return null;
            }

            if (journey.ValueKind != JsonValueKind.Object)
                return null;

            var excerpt = new Dictionary<string, object?> { ["note"] = Note };

            if (TryGetProperty(journey, "navigation", out var nav) || TryGetProperty(journey, "Navigation", out nav))
                excerpt["navigation"] = JsonSerializer.Deserialize<object>(nav.GetRawText());

            if (TryGetProperty(journey, "children", out var children) || TryGetProperty(journey, "Children", out children))
            {
                if (children.ValueKind == JsonValueKind.Array)
                {
                    var first = children.EnumerateArray().FirstOrDefault();
                    if (first.ValueKind == JsonValueKind.Object)
                        excerpt["firstChild"] = JsonSerializer.Deserialize<object>(first.GetRawText());
                }
            }

            var json = JsonSerializer.Serialize(excerpt, CompactOptions);
            return json.Length <= MaxChars ? json : json[..MaxChars] + "…";
        }
    }

    private static bool TryGetProperty(JsonElement el, string name, out JsonElement value)
    {
        if (el.TryGetProperty(name, out value))
            return true;

        foreach (var prop in el.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
