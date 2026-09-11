namespace Journeys.Core.Utility;

public static class WorkflowUserPhraseCatalog
{
    public static readonly string[] ApprovalPhrases =
    [
        "yes", "yep", "yeah", "sure", "ok", "okay", "confirm",
        "approve", "approved", "looks good", "continue", "proceed", "go ahead", "lgtm", "ok to continue"
    ];

    public static readonly string[] RewindDataPhrases = ["redo analysis", "redo data", "restart analysis"];

    public static readonly string[] RewindEventModelPhrases = ["redo event", "redo models", "restart event model"];

    public static readonly string[] TagFirstPhrases =
    [
        "tag-first", "tag first", "skip event model", "skip event setup", "no event model", "tag-first campaign", "tag first campaign"
    ];

    public static readonly string[] CampaignFixPhrases =
    [
        "fix campaign", "change the journey", "edit campaign", "update campaign", "modify campaign"
    ];

    public static readonly string[] AbandonPendingModelPhrases =
    [
        "use loyaltyaccountdetails instead", "skip review model", "use existing model", "use the existing", "don't create", "do not create"
    ];

    /// <summary>User confirms reusing an eligible discovered event model for the pending spec.</summary>
    public static readonly string[] EventModelReusePhrases =
    [
        "reuse existing", "reuse the", "confirm reuse", "reuse review", "use existing review",
        "yes reuse", "use that model", "use that one", "keep the existing"
    ];

    /// <summary>
    /// User wants to run ProcessEvent / sample payload testing — treats as journey approval and advances to Verification.
    /// </summary>
    public static readonly string[] JourneyVerificationIntentPhrases =
    [
        "test with", "sample payload", "test payload", "process event", "process_event",
        "process the event", "run a test", "test the campaign", "test event", "verify with"
    ];

    public static readonly string[] FocusEventModelPhrases =
    [
        "event model", "save_model", "create a model", "create model", "wrapper model", "andrulestate"
    ];

    public static readonly string[] FocusPatPhrases =
    [
        "point account", "pat ", "upsert_point_account", "pointaccounttype", "earn account", "spend account"
    ];

    public static readonly string[] FocusJourneyPhrases =
    [
        "journey", "rule set", "ruleset", "navigation", "outcome", "depositpoints", "spendpoint"
    ];

    public static readonly string[] FocusVerificationPhrases =
    [
        "process event", "process_event", "sample payload", "test payload", "verify the campaign"
    ];

    public static readonly string[] LivePromotionPhrases =
    [
        "promote to live", "go live", "set status live", "make it live", "make live",
        "deploy", "activate campaign", "publish campaign", "launch campaign", "promote the campaign"
    ];

    public static bool ContainsAny(string message, IEnumerable<string> phrases)
    {
        foreach (var p in phrases)
        {
            if (message.Contains(p, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
