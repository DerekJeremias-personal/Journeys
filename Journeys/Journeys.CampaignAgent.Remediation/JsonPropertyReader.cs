using System.Text.Json;

namespace Journeys.CampaignAgent.Remediation;

internal static class JsonPropertyReader
{
    public static bool TryGetProperty(JsonElement obj, string camelName, out JsonElement value)
    {
        if (obj.TryGetProperty(camelName, out value))
            return true;

        if (string.IsNullOrEmpty(camelName))
        {
            value = default;
            return false;
        }

        var pascal = char.ToUpperInvariant(camelName[0]) + camelName[1..];
        if (!string.Equals(pascal, camelName, StringComparison.Ordinal)
            && obj.TryGetProperty(pascal, out value))
            return true;

        var lower = char.ToLowerInvariant(camelName[0]) + camelName[1..];
        if (!string.Equals(lower, camelName, StringComparison.Ordinal)
            && obj.TryGetProperty(lower, out value))
            return true;

        value = default;
        return false;
    }
}
