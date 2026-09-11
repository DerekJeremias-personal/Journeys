using System.Text.Json;
using Journeys.Core.Models;
using Journeys.DTO.Models;

namespace Journeys.Core.Utility;

/// <summary>
/// Decides whether <see cref="CampaignWorkflowPhase.PointAccountTypes"/> can advance to
/// <see cref="CampaignWorkflowPhase.CampaignJourney"/> based on populated manifest items.
/// </summary>
public static class PointAccountManifestGate
{
    public static bool IsSatisfied(CampaignWorkflowState state, out IReadOnlyList<string> missingRoles)
    {
        ArgumentNullException.ThrowIfNull(state);

        var manifest = PointAccountManifestBuilder.Parse(state.Artifacts.PointAccountManifest);
        if (manifest.Items.Count == 0)
        {
            missingRoles = ["any"];
            return false;
        }

        if (!RequiresDualBucket(state))
        {
            missingRoles = [];
            return true;
        }

        var hasSpendable = manifest.Items.Any(i =>
            string.Equals(i.Role, "spendable", StringComparison.OrdinalIgnoreCase));
        var hasTier = manifest.Items.Any(i =>
            string.Equals(i.Role, "tierQualification", StringComparison.OrdinalIgnoreCase));

        var missing = new List<string>(2);
        if (!hasSpendable) missing.Add("spendable");
        if (!hasTier) missing.Add("tierQualification");
        missingRoles = missing;
        return missing.Count == 0;
    }

    /// <summary>
    /// Tier / milestone / dual-bucket programs need both spendable and tier-qualification PAT roles.
    /// </summary>
    public static bool RequiresDualBucket(CampaignWorkflowState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        foreach (var text in BriefTexts(state))
        {
            if (string.IsNullOrWhiteSpace(text))
                continue;

            if (text.Contains("tier", StringComparison.OrdinalIgnoreCase)
                || text.Contains("milestone", StringComparison.OrdinalIgnoreCase)
                || text.Contains("qualification", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static IEnumerable<string?> BriefTexts(CampaignWorkflowState state)
    {
        var texts = new List<string?>();
        foreach (var json in new[] { state.Artifacts.CampaignDesignBriefApproved, state.Artifacts.CampaignDesignBriefProposed })
        {
            if (string.IsNullOrWhiteSpace(json))
                continue;

            texts.Add(json);
            texts.AddRange(ParseBriefFields(json));
        }

        return texts;
    }

    private static IEnumerable<string?> ParseBriefFields(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return [];

            return
            [
                ReadString(root, "objective"),
                ReadString(root, "mechanic"),
                ReadString(root, "successCriteria"),
                ReadString(root, "audience")
            ];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? ReadString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;
}
