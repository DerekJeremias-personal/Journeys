namespace Journeys.Core.Utility;

public static class JourneyProceedIntent
{
    private static readonly string[] JourneyProceedPhrases =
    {
        "continue to the journey",
        "continue to journey",
        "author the journey",
        "build the journey",
        "build the tiers",
        "create the journey",
        "start the journey",
        "proceed to journey"
    };

    public static bool LooksLikeProceedToJourney(string? userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            return false;

        if (MutatorRetryIntent.LooksLikeProceedToCreate(userMessage))
            return true;

        var msg = userMessage.Trim();
        foreach (var phrase in JourneyProceedPhrases)
        {
            if (msg.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
