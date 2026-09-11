using Microsoft.Extensions.AI;

namespace Journeys.API.CampaignAgent;

/// <summary>
/// Normalizes MEAI/Anthropic chat transcripts so each tool <c>callId</c> has at most one
/// <see cref="FunctionResultContent"/>. Prefers <see cref="ChatRole.Tool"/> rows over assistant-embedded results.
/// </summary>
internal static class CampaignAgentTranscriptRules
{
    /// <summary>
    /// Removes duplicate tool results that break Anthropic's tool_use → tool_result pairing.
    /// </summary>
    public static List<ChatMessage> SanitizeChatMessages(IEnumerable<ChatMessage> messages)
    {
        var list = messages.ToList();
        if (list.Count == 0)
            return list;

        var toolResultCallIds = CollectFunctionResultCallIds(list, ChatRole.Tool);
        var output = new List<ChatMessage>(list.Count);
        var satisfiedResultCallIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var msg in list)
        {
            if (msg.Role == ChatRole.User)
            {
                satisfiedResultCallIds.Clear();
                output.Add(msg);
                continue;
            }

            if (msg.Role == ChatRole.Assistant || msg.Role == ChatRole.Tool)
            {
                var filtered = FilterMessageContents(
                    msg,
                    c =>
                    {
                        if (c is not FunctionResultContent fr || string.IsNullOrEmpty(fr.CallId))
                            return true;

                        if (msg.Role == ChatRole.Assistant && toolResultCallIds.Contains(fr.CallId))
                            return false;

                        return satisfiedResultCallIds.Add(fr.CallId);
                    });

                if (filtered.Contents is { Count: > 0 })
                    output.Add(filtered);
                continue;
            }

            output.Add(msg);
        }

        return output;
    }

    /// <summary>
    /// Returns true when a prior assistant or tool message already carries a result for this call id.
    /// </summary>
    public static bool CallIdAlreadyHasFunctionResult(IReadOnlyList<ChatMessage> priorMessages, string callId)
    {
        if (string.IsNullOrEmpty(callId))
            return false;

        foreach (var msg in priorMessages)
        {
            if ((msg.Role != ChatRole.Assistant && msg.Role != ChatRole.Tool) || msg.Contents is null)
                continue;

            foreach (var c in msg.Contents)
            {
                if (c is FunctionResultContent fr
                    && string.Equals(fr.CallId, callId, StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Obsolete name kept for callers migrating; use <see cref="CallIdAlreadyHasFunctionResult"/>.
    /// </summary>
    public static bool AssistantAlreadyHasFunctionResult(IReadOnlyList<ChatMessage> priorMessages, string callId) =>
        CallIdAlreadyHasFunctionResult(priorMessages, callId);

    public static void RegisterAssistantResultsFromMessage(ChatMessage msg, ISet<string> satisfiedCallIds)
    {
        if (msg.Role != ChatRole.Assistant || msg.Contents is null)
            return;

        foreach (var c in msg.Contents)
        {
            if (c is FunctionResultContent fr && !string.IsNullOrEmpty(fr.CallId))
                satisfiedCallIds.Add(fr.CallId);
        }
    }

    private static HashSet<string> CollectFunctionResultCallIds(IReadOnlyList<ChatMessage> messages, ChatRole role)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var msg in messages)
        {
            if (msg.Role != role || msg.Contents is null)
                continue;

            foreach (var c in msg.Contents)
            {
                if (c is FunctionResultContent fr && !string.IsNullOrEmpty(fr.CallId))
                    set.Add(fr.CallId);
            }
        }

        return set;
    }

    private static ChatMessage FilterMessageContents(ChatMessage msg, Func<AIContent, bool> keep)
    {
        if (msg.Contents is null || msg.Contents.Count == 0)
            return msg;

        var contents = new List<AIContent>();
        foreach (var c in msg.Contents)
        {
            if (keep(c))
                contents.Add(c);
        }

        if (contents.Count == msg.Contents.Count)
            return msg;

        return new ChatMessage(msg.Role, contents)
        {
            MessageId = msg.MessageId,
            AuthorName = msg.AuthorName,
            CreatedAt = msg.CreatedAt,
            RawRepresentation = msg.RawRepresentation,
            AdditionalProperties = msg.AdditionalProperties
        };
    }
}
