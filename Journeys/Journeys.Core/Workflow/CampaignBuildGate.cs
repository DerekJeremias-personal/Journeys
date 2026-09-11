using System.Text.Json;
using Journeys.Core.Models;
using Journeys.Core.Utility;

namespace Journeys.Core.Workflow;

/// <summary>
/// Blocks validate/upsert campaign calls that include journey content or inline PAT ids
/// before PointAccountManifest is populated from upsert_point_account_type.
/// </summary>
public static class CampaignBuildGate
{
    public static bool ShouldBlockJourneyPayload(CampaignWorkflowState state, string? campaignJson)
    {
        if (string.IsNullOrWhiteSpace(campaignJson))
            return false;
        if (CreationSnapshotArtifact.Read(state) is { CreationComplete: true })
            return false;

        var manifestItems = PointAccountManifestBuilder.Parse(state.Artifacts.PointAccountManifest).Items;
        if (manifestItems.Count > 0
            && ContainsJourneyContent(campaignJson)
            && !ContainsInlineUnregisteredPatIds(state, campaignJson))
            return false;

        return ContainsJourneyContent(campaignJson)
               || ContainsInlineUnregisteredPatIds(state, campaignJson);
    }

    internal static bool ContainsJourneyContentForGate(string json) => ContainsJourneyContent(json);

    public static string BuildBlockedToolResult() =>
        """
        {"errors":{"buildGate":["[violation=BUILD_GATE_PAT_MANIFEST_REQUIRED] Call upsert_point_account_type first. PointAccountManifest.items must be populated with real PAT ids before validate_campaign or upsert_campaign with journey content. Do not inline fake manifest ids like pat-spendable or SPENDABLE_PAT_ID."]},"_agentRemediation":"Upsert expired sink PAT, then spendable PAT, then retry journey validate/upsert."}
        """;

    public static void ApplyBlock(CampaignWorkflowState state) =>
        state.Artifacts.LastToolRemediationSummary = "BUILD_GATE_PAT_MANIFEST_REQUIRED";

    private static bool ContainsJourneyContent(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("journey", out var journey))
                return false;
            if (journey.TryGetProperty("rules", out var rules)
                && rules.ValueKind == JsonValueKind.Array
                && rules.GetArrayLength() > 0)
                return true;
            if (journey.TryGetProperty("navigation", out var nav)
                && nav.ValueKind == JsonValueKind.Object
                && nav.EnumerateObject().Any())
                return true;
            if (journey.TryGetProperty("nodes", out var nodes)
                && nodes.ValueKind == JsonValueKind.Array
                && nodes.GetArrayLength() > 0)
                return true;
        }
        catch (JsonException)
        {
            // ignore malformed JSON — let downstream validation handle it
        }

        return false;
    }

    private static bool ContainsInlineUnregisteredPatIds(CampaignWorkflowState state, string json)
    {
        var registered = PointAccountManifestBuilder.Parse(state.Artifacts.PointAccountManifest)
            .Items.Select(i => i.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (registered.Count > 0 && !ContainsJourneyContent(json))
            return false;

        if (json.Contains("SPENDABLE_PAT_ID", StringComparison.OrdinalIgnoreCase))
            return true;
        if (json.Contains("pointAccountManifest", StringComparison.OrdinalIgnoreCase)
            && registered.Count == 0)
            return true;
        if (json.Contains("pointAccountTypeId", StringComparison.OrdinalIgnoreCase)
            && registered.Count == 0)
            return true;

        return registered.Count == 0
               && json.Contains("pat-", StringComparison.OrdinalIgnoreCase);
    }
}
