using System.Text.Json;

namespace Journeys.Core.Utility;

/// <summary>Parse rule-set counts from journey digest JSON on workflow artifacts.</summary>
public static class JourneyDigestUtility
{
    public static int TryReadRuleSetCountFromDigest(string? digestJson)
    {
        if (string.IsNullOrWhiteSpace(digestJson))
            return 0;

        try
        {
            using var doc = JsonDocument.Parse(digestJson);
            return ReadRuleSetCount(doc.RootElement);
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    public static int ReadRuleSetCount(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return 0;

        foreach (var name in new[] { "ruleSetCount", "RuleSetCount" })
        {
            if (element.TryGetProperty(name, out var countEl)
                && countEl.ValueKind == JsonValueKind.Number
                && countEl.TryGetInt32(out var count))
                return count;
        }

        return 0;
    }
}
