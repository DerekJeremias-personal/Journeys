using System.Text.Json;
using Journeys.CampaignAgent.Remediation;
using Journeys.Core.Models;
using Journeys.Core.Utility;

namespace Journeys.API.CampaignAgent.Workflow;

internal static class JourneyContractSummaryPinArtifacts
{
    private const int MaxCriticalRowIdsChars = 400;

    public static void RefreshEpisodeState(CampaignWorkflowState state)
    {
        var manifestCount = PointAccountManifestBuilder.Parse(state.Artifacts.PointAccountManifest).Items.Count;
        var ruleSetCount = JourneyPatternArtifacts.GetJourneyRuleSetCount(state);

        if (manifestCount > 0 && ruleSetCount == 0)
        {
            state.Artifacts.JourneyContractSummaryPinActive = true;
            return;
        }

        if (ruleSetCount > 0)
            Clear(state);
    }

    public static void Clear(CampaignWorkflowState state)
    {
        state.Artifacts.JourneyContractSummaryPinActive = false;
        state.Artifacts.JourneyContractSummaryMatrixVersion = null;
        state.Artifacts.JourneyContractSummaryFetchedThisEpisode = false;
        state.Artifacts.JourneyContractCriticalRowIds = null;
    }

    public static void OnContractSummaryFetched(CampaignWorkflowState state, string json)
    {
        if (!CampaignWorkflowToolSuccess.LooksSuccessful(json))
            return;

        RefreshEpisodeState(state);
        if (!state.Artifacts.JourneyContractSummaryPinActive)
            return;

        state.Artifacts.JourneyContractSummaryFetchedThisEpisode = true;

        if (TryParseMetadata(json, out var matrixVersion, out var criticalRowIds))
        {
            if (!string.IsNullOrWhiteSpace(matrixVersion))
                state.Artifacts.JourneyContractSummaryMatrixVersion = matrixVersion;

            if (criticalRowIds.Count > 0)
                state.Artifacts.JourneyContractCriticalRowIds = FormatCriticalRowIds(criticalRowIds);
        }
    }

    private static string FormatCriticalRowIds(IReadOnlyList<string> ids)
    {
        var joined = string.Join(", ", ids);
        if (joined.Length <= MaxCriticalRowIdsChars)
            return joined;

        var truncated = joined[..MaxCriticalRowIdsChars];
        var lastComma = truncated.LastIndexOf(", ", StringComparison.Ordinal);
        return lastComma > 0 ? truncated[..lastComma] + "…" : truncated + "…";
    }

    private static bool TryParseMetadata(string json, out string? matrixVersion, out List<string> criticalRowIds)
    {
        matrixVersion = null;
        criticalRowIds = new List<string>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("$type", out var typeProp)
                && typeProp.ValueKind == JsonValueKind.String
                && string.Equals(typeProp.GetString(), "text", StringComparison.Ordinal)
                && root.TryGetProperty("text", out var textProp)
                && textProp.ValueKind == JsonValueKind.String)
            {
                var inner = textProp.GetString();
                if (string.IsNullOrWhiteSpace(inner))
                    return false;
                return TryParseMetadata(inner, out matrixVersion, out criticalRowIds);
            }

            if (root.ValueKind != JsonValueKind.Object)
                return false;

            matrixVersion = ReadStringProperty(root, "matrixVersion");
            if (!root.TryGetProperty("criticalRows", out var rows) || rows.ValueKind != JsonValueKind.Array)
                return !string.IsNullOrWhiteSpace(matrixVersion);

            foreach (var row in rows.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object)
                    continue;
                var id = ReadStringProperty(row, "id");
                if (!string.IsNullOrWhiteSpace(id))
                    criticalRowIds.Add(id);
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ReadStringProperty(JsonElement el, string name)
    {
        foreach (var prop in el.EnumerateObject())
        {
            if (!string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                continue;
            return prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : null;
        }

        return null;
    }
}
