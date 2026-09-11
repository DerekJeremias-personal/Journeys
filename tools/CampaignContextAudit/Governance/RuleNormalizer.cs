using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CampaignContextAudit.Governance;

public static partial class RuleNormalizer
{
    private static readonly Regex ImperativeRegex = ImperativePattern();
    private static readonly Regex BulletRegex = BulletPattern();

    public static IReadOnlyList<NormalizedRule> ExtractRules(string content, GuidanceLayer layer, string source)
    {
        var rules = new List<NormalizedRule>();
        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            var isBullet = BulletRegex.IsMatch(trimmed);
            var isProse = trimmed.Length > 20 && !trimmed.StartsWith("//", StringComparison.Ordinal);
            if (!isBullet && !isProse)
                continue;

            var normalized = Normalize(trimmed);
            if (normalized.Length < 12)
                continue;

            rules.Add(new NormalizedRule(Hash(normalized), Excerpt(trimmed), layer, source));
        }

        return rules;
    }

    public static string Normalize(string line)
    {
        var s = line.Trim();
        s = BulletRegex.Replace(s, "");
        s = Regex.Replace(s, @"^\d+[\.\)]\s*", "");
        s = s.ToLowerInvariant();
        s = Regex.Replace(s, @"\s+", " ").Trim();
        return s;
    }

    public static HashSet<string> Tokenize(string normalized) =>
        normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length >= 4)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static double Jaccard(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 && b.Count == 0)
            return 1.0;
        if (a.Count == 0 || b.Count == 0)
            return 0.0;

        var intersection = a.Intersect(b, StringComparer.OrdinalIgnoreCase).Count();
        var union = a.Union(b, StringComparer.OrdinalIgnoreCase).Count();
        return union == 0 ? 0.0 : (double)intersection / union;
    }

    public static string Hash(string normalized)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes)[..8].ToLowerInvariant();
    }

    public static bool IsImperative(string line) => ImperativeRegex.IsMatch(line);

    public static string Excerpt(string line, int max = 120) =>
        line.Length <= max ? line : line[..max] + "…";

    [GeneratedRegex(@"\b(call|use|do not|must|set|upsert|validate|prefer|require)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ImperativePattern();

    [GeneratedRegex(@"^[\-*•]\s*")]
    private static partial Regex BulletPattern();
}
