namespace Journeys.Core.Utility;

public static class MutatorRetryIntent
{
    private static readonly string[] ProceedPhrases =
    {
        "proceed to create the campaign",
        "proceed to create campaign",
        "go ahead and create the campaign",
        "continue to create the campaign",
        "create the campaign",
        "build the campaign",
        "start the campaign",
        "create the pat",
        "create point account",
        "create the point account types",
        "upsert_point_account_type",
        "create the four pat"
    };

    public static bool LooksLikeProceedToCreate(string? userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            return false;

        var msg = userMessage.Trim();
        foreach (var phrase in ProceedPhrases)
        {
            if (msg.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
