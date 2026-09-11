namespace Journeys.Core.Utility;

/// <summary>Detects when the user wants a brand-new event model (not reuse an existing candidate).</summary>
public static class EventModelCreateNewIntent
{
    private static readonly string[] ExplicitPhrases =
    [
        "create the model",
        "create a model",
        "create the event",
        "create an event",
        "new event model",
        "make the model",
        "build the model",
        "define the model",
        "add a new event",
        "save the model"
    ];

    private static readonly string[] BroadPhrases =
    [
        "create new",
        "create a new",
        "new event",
        "new model",
        "make a new",
        "build a new",
        "define a new"
    ];

    private static readonly string[] NonEventCreateTargets =
    [
        "campaign",
        "journey",
        "point account type",
        "point account",
        "loyalty program"
    ];

    public static bool LooksLikeRequest(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        var msg = message.Trim();

        foreach (var phrase in ExplicitPhrases)
        {
            if (msg.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (TargetsNonEventArtifactWithoutEventAnchor(msg))
            return false;

        foreach (var phrase in BroadPhrases)
        {
            if (!msg.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                continue;
            if (HasEventModelAnchor(msg))
                return true;
        }

        return false;
    }

    private static bool TargetsNonEventArtifactWithoutEventAnchor(string msg)
    {
        if (!NonEventCreateTargets.Any(t => msg.Contains(t, StringComparison.OrdinalIgnoreCase)))
            return false;

        return !HasEventModelAnchor(msg);
    }

    private static bool HasEventModelAnchor(string msg) =>
        msg.Contains("event model", StringComparison.OrdinalIgnoreCase)
        || msg.Contains("event payload", StringComparison.OrdinalIgnoreCase)
        || msg.Contains("eventable", StringComparison.OrdinalIgnoreCase)
        || msg.Contains("wrapper model", StringComparison.OrdinalIgnoreCase)
        || msg.Contains("save_model", StringComparison.OrdinalIgnoreCase)
        || msg.Contains("savemodel", StringComparison.OrdinalIgnoreCase)
        || ContainsWholeWord(msg, "event");

    private static bool ContainsWholeWord(string msg, string word)
    {
        var index = 0;
        while ((index = msg.IndexOf(word, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var beforeOk = index == 0 || !char.IsLetterOrDigit(msg[index - 1]);
            var afterIndex = index + word.Length;
            var afterOk = afterIndex >= msg.Length || !char.IsLetterOrDigit(msg[afterIndex]);
            if (beforeOk && afterOk)
                return true;
            index += word.Length;
        }

        return false;
    }
}
