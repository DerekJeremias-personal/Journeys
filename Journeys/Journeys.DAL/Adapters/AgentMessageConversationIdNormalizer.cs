namespace Journeys.DAL.Adapters;

public static class AgentMessageConversationIdNormalizer
{
    public static string Normalize(string raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);
        var normalized = raw.Trim().Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();
        if (string.IsNullOrEmpty(normalized))
            throw new ArgumentException("Conversation id is empty after normalization.", nameof(raw));
        return normalized;
    }
}
