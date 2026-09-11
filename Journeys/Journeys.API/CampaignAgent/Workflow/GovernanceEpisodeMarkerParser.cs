using Journeys.Core.Models;

namespace Journeys.API.CampaignAgent.Workflow;

public static class GovernanceEpisodeMarkerParser
{
    public static string? Extract(string content, RuleEpisode episode)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var tag = episode.ToString();
        var startMarker = $"<!-- episode:{tag} -->";
        var endMarker = "<!-- /episode -->";

        var startIdx = content.IndexOf(startMarker, StringComparison.Ordinal);
        if (startIdx < 0)
            return null;

        var contentStart = startIdx + startMarker.Length;
        var endIdx = content.IndexOf(endMarker, contentStart, StringComparison.Ordinal);
        if (endIdx < 0)
            return null;

        return content[contentStart..endIdx].Trim();
    }
}
