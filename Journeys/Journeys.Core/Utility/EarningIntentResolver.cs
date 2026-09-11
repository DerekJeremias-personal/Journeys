using System.Text.Json;
using Journeys.Core.Models;

namespace Journeys.Core.Utility;

/// <summary>
/// Resolves campaign earning intent from the design brief: a structured "earningIntent" field
/// wins; otherwise a lightweight keyword scan of the brief text; otherwise Unknown.
/// </summary>
public static class EarningIntentResolver
{
    private static readonly string[] PointEarningKeywords =
    {
        "earn points", "earn point", "points for", "point accrual", "accrual",
        "reward per", "rewards per", "points per", "earning", "spend-based", "per dollar", "per purchase"
    };

    public static EarningIntent Resolve(string? briefJsonOrText)
    {
        if (string.IsNullOrWhiteSpace(briefJsonOrText))
            return EarningIntent.Unknown;

        var structured = TryReadStructuredIntent(briefJsonOrText);
        if (structured != EarningIntent.Unknown)
            return structured;

        foreach (var kw in PointEarningKeywords)
        {
            if (briefJsonOrText.Contains(kw, StringComparison.OrdinalIgnoreCase))
                return EarningIntent.PointEarning;
        }

        return EarningIntent.Unknown;
    }

    private static EarningIntent TryReadStructuredIntent(string brief)
    {
        try
        {
            using var doc = JsonDocument.Parse(brief);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return EarningIntent.Unknown;

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (!string.Equals(prop.Name, "earningIntent", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (prop.Value.ValueKind != JsonValueKind.String)
                    return EarningIntent.Unknown;
                var raw = prop.Value.GetString();
                return Enum.TryParse<EarningIntent>(raw, ignoreCase: true, out var parsed)
                    ? parsed
                    : EarningIntent.Unknown;
            }
        }
        catch (JsonException)
        {
            // Brief may be plain prose, not JSON — fall through to keyword scan.
        }

        return EarningIntent.Unknown;
    }
}
