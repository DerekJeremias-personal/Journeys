using Journeys.Core.Models;
using Journeys.Core.Utility;

namespace Journeys.API.CampaignAgent.Workflow;

/// <summary>Maps brief + manifest signals to rules-engine pattern recipe ids.</summary>
public static class JourneyPatternClassifier
{
    public const string TierNavigationPointBalance = "tier-navigation-point-balance";

    private static readonly string[] TierLadderTerms =
    [
        "tier", "bronze", "silver", "gold", "platinum", "qualification point", "tqp"
    ];

    public static string? Resolve(CampaignWorkflowState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (ManifestHasTierQualificationPat(state))
            return TierNavigationPointBalance;

        if (BriefMentionsTierLadder(state))
            return TierNavigationPointBalance;

        return null;
    }

    public static bool BriefMentionsTierLadder(CampaignWorkflowState state)
    {
        var brief = state.Artifacts.CampaignDesignBriefApproved
                    ?? state.Artifacts.CampaignDesignBriefProposed;
        return TextMentionsTierLadder(brief);
    }

    public static bool TextMentionsTierLadder(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var lower = text.ToLowerInvariant();
        return TierLadderTerms.Any(term => lower.Contains(term, StringComparison.Ordinal));
    }

    private static bool ManifestHasTierQualificationPat(CampaignWorkflowState state)
    {
        var manifest = PointAccountManifestBuilder.Parse(state.Artifacts.PointAccountManifest);
        return manifest.Items.Any(item =>
            string.Equals(item.Role, "tierQualification", StringComparison.OrdinalIgnoreCase));
    }
}
