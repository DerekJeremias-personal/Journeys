using Journeys.CampaignAgent.Remediation;
using Journeys.Core.Models;

namespace Journeys.API.CampaignAgent;

/// <summary>Effective history char count — avoids double-counting MEAI embedded results when tool rows exist.</summary>
public static class CampaignAgentHistoryCharCounter
{
    public static int CountEffective(IReadOnlyList<AgentMessage> messages)
    {
        var toolCallIds = CollectToolCallIdsOnRows(messages);
        var total = 0;
        foreach (var m in messages)
            total += CountMessage(m, toolCallIds);
        return total;
    }

    private static HashSet<string> CollectToolCallIdsOnRows(IReadOnlyList<AgentMessage> messages)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var m in messages)
        {
            if (string.Equals(m.Role, "tool", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(m.ToolCallId))
                set.Add(m.ToolCallId);
        }

        return set;
    }

    private static int CountMessage(AgentMessage m, IReadOnlySet<string> toolCallIdsOnRows)
    {
        var toolChars = m.ToolResultJson?.Length ?? 0;
        if (string.Equals(m.Role, "tool", StringComparison.OrdinalIgnoreCase))
            return toolChars;

        if (string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase)
            && MeaiEmbeddedResultShrinker.IsMeaiEnvelope(m.Content))
            return MeaiEmbeddedResultShrinker.CountEffectiveAssistantContentChars(m.Content, toolCallIdsOnRows) + toolChars;

        return (m.Content?.Length ?? 0) + toolChars;
    }
}
