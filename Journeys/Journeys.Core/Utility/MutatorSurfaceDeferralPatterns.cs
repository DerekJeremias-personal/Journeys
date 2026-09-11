namespace Journeys.Core.Utility;

public static class MutatorSurfaceDeferralPatterns
{
    private static readonly string[] Patterns =
    [
        "not in my tool surface",
        "not surfaced",
        "not on my tool surface",
        "doesn't appear to be enabled",
        "does not appear to be enabled",
        "not enabled in this session",
        "no upsert_",
        "post /api",
        "pointaccounttype/upsert",
        "paste back",
        "paste the",
        "paste your",
        "4 guids",
        "four guids"
    ];

    public static bool LooksLikeDeferral(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var lower = text.ToLowerInvariant();
        return Patterns.Any(p => lower.Contains(p, StringComparison.Ordinal));
    }
}
