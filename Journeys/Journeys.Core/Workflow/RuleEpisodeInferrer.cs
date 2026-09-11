using System.Text.Json;
using Journeys.Core.Models;

namespace Journeys.Core.Workflow;

public static class RuleEpisodeInferrer
{
    public static RuleEpisode Infer(string? briefJson, string? openingUser, string? lastViolationCode)
    {
        if (!string.IsNullOrWhiteSpace(lastViolationCode))
        {
            if (lastViolationCode.Contains("PAT", StringComparison.OrdinalIgnoreCase)
                || lastViolationCode.Contains("EXPIR", StringComparison.OrdinalIgnoreCase))
                return RuleEpisode.Expiration;
        }

        if (MentionsTierLadderIntent(briefJson) || MentionsTierLadderIntent(openingUser))
            return RuleEpisode.TierLadder;

        if (MentionsExpirationIntent(briefJson) || MentionsExpirationIntent(openingUser))
            return RuleEpisode.Expiration;

        if (MentionsSimpleEarnIntent(briefJson) || MentionsSimpleEarnIntent(openingUser))
            return RuleEpisode.SimpleEarn;

        return RuleEpisode.Generic;
    }

    private static bool MentionsTierLadderIntent(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var lower = text.ToLowerInvariant();
        return lower.Contains("tier", StringComparison.Ordinal)
               || lower.Contains("bronze", StringComparison.Ordinal)
               || lower.Contains("silver", StringComparison.Ordinal)
               || lower.Contains("gold", StringComparison.Ordinal)
               || lower.Contains("platinum", StringComparison.Ordinal)
               || lower.Contains("tqp", StringComparison.Ordinal)
               || lower.Contains("tier-qual", StringComparison.Ordinal);
    }

    private static bool MentionsExpirationIntent(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var lower = text.ToLowerInvariant();
        return lower.Contains("expir", StringComparison.Ordinal)
               || lower.Contains("lifespan", StringComparison.Ordinal)
               || lower.Contains("12 month", StringComparison.Ordinal)
               || lower.Contains("365", StringComparison.Ordinal);
    }

    private static bool MentionsSimpleEarnIntent(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (MentionsTierLadderIntent(text))
            return false;

        var lower = text.ToLowerInvariant();
        return lower.Contains("earn", StringComparison.Ordinal)
               || lower.Contains("points per", StringComparison.Ordinal)
               || lower.Contains("pts/$", StringComparison.Ordinal)
               || lower.Contains("pts per", StringComparison.Ordinal)
               || lower.Contains("ordertotal", StringComparison.Ordinal);
    }

    public static string? ReadBriefField(string? briefJson, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(briefJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(briefJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;
            if (!doc.RootElement.TryGetProperty(fieldName, out var el) || el.ValueKind != JsonValueKind.String)
                return null;
            return el.GetString();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
