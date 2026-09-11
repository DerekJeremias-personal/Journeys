namespace Journeys.Core.Utility;

/// <summary>
/// Shared PAT name heuristics for manifest role inference and save_model fabrication guards.
/// </summary>
public static class PatNamingHeuristics
{
    public static bool LooksTierQualLike(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        return name.Contains("tier", StringComparison.OrdinalIgnoreCase)
               || name.Contains("qual", StringComparison.OrdinalIgnoreCase)
               || name.Contains("qualification", StringComparison.OrdinalIgnoreCase);
    }
}
