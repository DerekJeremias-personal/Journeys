using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Journeys.API.CampaignAgent;

internal static class CampaignAgentToolAliasExpander
{
    public static IReadOnlyList<AITool> Expand(IEnumerable<AITool> tools, bool enabled, ILogger? logger)
    {
        var list = tools.ToList();
        if (!enabled)
            return list;

        var existing = new HashSet<string>(list.Select(t => t.Name), StringComparer.OrdinalIgnoreCase);
        var additions = new List<AITool>();

        foreach (var tool in list)
        {
            if (tool is not AIFunction fn || tool is ToolAliasAIFunction)
                continue;

            foreach (var (canonical, alias) in CampaignAgentToolAliasCatalog.Pairs)
            {
                TryAddAlias(fn, canonical, alias, existing, additions);
                TryAddAlias(fn, alias, canonical, existing, additions);
            }
        }

        if (additions.Count > 0)
        {
            logger?.LogDebug(
                "Campaign agent tool aliases: added {Count} alias tool(s): {Names}",
                additions.Count,
                string.Join(", ", additions.Select(t => t.Name)));
            list.AddRange(additions);
        }

        return list;
    }

    private static void TryAddAlias(
        AIFunction fn,
        string presentName,
        string aliasName,
        HashSet<string> existing,
        List<AITool> additions)
    {
        if (!string.Equals(fn.Name, presentName, StringComparison.OrdinalIgnoreCase))
            return;
        if (existing.Contains(aliasName))
            return;

        additions.Add(new ToolAliasAIFunction(fn, aliasName));
        existing.Add(aliasName);
    }
}
