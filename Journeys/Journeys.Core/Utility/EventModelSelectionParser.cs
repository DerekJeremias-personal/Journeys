using Journeys.DTO.Models;

namespace Journeys.Core.Utility;

public enum EventModelSelectionKind
{
    Ambiguous = 0,
    PickExisting = 1,
    CreateNew = 2
}

public readonly record struct EventModelSelectionResult(EventModelSelectionKind Kind, string? SelectedEventModelId);

/// <summary>
/// Resolves a user's reply while the EventModelSelection gate is active into pick-existing,
/// create-new, or ambiguous. Create-new intent takes precedence; then explicit id/name match;
/// then an affirmative confirmation of the recommended default.
/// </summary>
public static class EventModelSelectionParser
{
    private static readonly string[] ConfirmPhrases =
    {
        "yes", "yep", "yeah", "use it", "use that", "use the", "that one", "sounds good",
        "looks good", "ok", "okay", "sure", "confirm", "proceed", "continue", "approve", "lgtm"
    };

    public static EventModelSelectionResult Resolve(string? userMessage, EventModelCandidateSet? set)
    {
        if (string.IsNullOrWhiteSpace(userMessage) || set == null)
            return new EventModelSelectionResult(EventModelSelectionKind.Ambiguous, null);

        var msg = userMessage.Trim();

        if (EventModelCreateNewIntent.LooksLikeRequest(msg))
            return new EventModelSelectionResult(EventModelSelectionKind.CreateNew, null);

        foreach (var c in set.Candidates)
        {
            if (!string.IsNullOrWhiteSpace(c.EventModelId)
                && msg.Equals(c.EventModelId, StringComparison.OrdinalIgnoreCase))
                return new EventModelSelectionResult(EventModelSelectionKind.PickExisting, c.EventModelId);
        }

        var exact = set.Candidates.FirstOrDefault(c =>
            NameEquals(c.Name, msg) || NameEquals(c.DisplayName, msg));
        if (exact != null)
            return new EventModelSelectionResult(EventModelSelectionKind.PickExisting, exact.EventModelId);

        var contains = set.Candidates.FirstOrDefault(c =>
            NameContained(c.Name, msg) || NameContained(c.DisplayName, msg));
        if (contains != null)
            return new EventModelSelectionResult(EventModelSelectionKind.PickExisting, contains.EventModelId);

        if (!string.IsNullOrWhiteSpace(set.RecommendedDefaultId) && ContainsAny(msg, ConfirmPhrases))
            return new EventModelSelectionResult(EventModelSelectionKind.PickExisting, set.RecommendedDefaultId);

        if (!string.IsNullOrWhiteSpace(set.RecommendedDefaultId)
            && set.Candidates.Any(c =>
                string.Equals(c.EventModelId, set.RecommendedDefaultId, StringComparison.OrdinalIgnoreCase)
                && c.IsStrongMatch)
            && MutatorRetryIntent.LooksLikeProceedToCreate(msg))
        {
            return new EventModelSelectionResult(EventModelSelectionKind.PickExisting, set.RecommendedDefaultId);
        }

        return new EventModelSelectionResult(EventModelSelectionKind.Ambiguous, null);
    }

    private static bool NameEquals(string? name, string msg) =>
        !string.IsNullOrWhiteSpace(name) && msg.Equals(name, StringComparison.OrdinalIgnoreCase);

    private static bool NameContained(string? name, string msg) =>
        !string.IsNullOrWhiteSpace(name)
        && name!.Length >= 3
        && msg.Contains(name, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAny(string msg, IEnumerable<string> phrases)
    {
        foreach (var p in phrases)
        {
            if (msg.Contains(p, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
