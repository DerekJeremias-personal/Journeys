using System;
using System.Collections.Generic;
using System.Linq;

namespace Journeys.Core.Utility;

public static class CampaignTestAccountAllowlist
{
    public static bool IsAllowlisted(IEnumerable<string>? allowlist, string? accountExtId)
    {
        if (string.IsNullOrWhiteSpace(accountExtId))
            return false;
        if (allowlist == null)
            return false;

        return allowlist.Any(id =>
            !string.IsNullOrWhiteSpace(id)
            && string.Equals(id.Trim(), accountExtId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<string> Normalize(IEnumerable<string>? ids) =>
        ids?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList()
        ?? [];
}
