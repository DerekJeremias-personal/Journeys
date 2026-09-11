using System.Text;
using System.Text.Json;
using Journeys.Core.Models;

namespace Journeys.Core.Utility;

/// <summary>
/// Keeps workflow pointAccountManifest mirrored on verificationRecord for audit grounding.
/// </summary>
public static class VerificationRecordManifestBridge
{
    public static void SeedOnCreationComplete(CampaignWorkflowState state)
    {
        if (string.IsNullOrWhiteSpace(state.Artifacts.PointAccountManifest))
            return;

        if (PointAccountManifestBuilder.Parse(state.Artifacts.PointAccountManifest).Items.Count == 0)
            return;

        if (VerificationRecordHasManifestItems(state.Artifacts.VerificationRecord))
            return;

        state.Artifacts.VerificationRecord = MergeManifestIntoRecord(
            state.Artifacts.VerificationRecord,
            state.Artifacts.PointAccountManifest);
    }

    public static string MergeIntoProcessEventResult(CampaignWorkflowState state, string processEventJson)
    {
        if (string.IsNullOrWhiteSpace(state.Artifacts.PointAccountManifest))
            return processEventJson;

        return MergeManifestIntoRecord(processEventJson, state.Artifacts.PointAccountManifest);
    }

    public static bool IsManifestSeedOnly(string? recordJson)
    {
        if (string.IsNullOrWhiteSpace(recordJson) || !VerificationRecordHasManifestItems(recordJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(recordJson);
            var root = doc.RootElement;
            if (HasNonEmptyArray(root, "appliedRuleSetIds") || HasNonEmptyArray(root, "AppliedRuleSetIds"))
                return false;
            if (root.TryGetProperty("status", out _) || root.TryGetProperty("pointsAwarded", out _))
                return false;
            if (root.TryGetProperty("OutcomeStates", out var outcomes)
                && outcomes.ValueKind == JsonValueKind.Array
                && outcomes.GetArrayLength() > 0)
                return false;

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool HasNonEmptyArray(JsonElement root, string name) =>
        root.TryGetProperty(name, out var arr)
        && arr.ValueKind == JsonValueKind.Array
        && arr.GetArrayLength() > 0;

    internal static bool VerificationRecordHasManifestItems(string? recordJson)
    {
        if (string.IsNullOrWhiteSpace(recordJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(recordJson);
            if (!doc.RootElement.TryGetProperty("pointAccountManifest", out var manifest))
                return false;

            return manifest.TryGetProperty("items", out var items)
                   && items.ValueKind == JsonValueKind.Array
                   && items.GetArrayLength() > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    internal static string MergeManifestIntoRecord(string? recordJson, string manifestJson)
    {
        using var manifestDoc = JsonDocument.Parse(manifestJson);
        var manifestRoot = manifestDoc.RootElement;
        if (manifestRoot.TryGetProperty("items", out var manifestItems)
            && manifestItems.ValueKind == JsonValueKind.Array
            && manifestItems.GetArrayLength() == 0)
        {
            return recordJson ?? "{}";
        }

        using var recordDoc = JsonDocument.Parse(string.IsNullOrWhiteSpace(recordJson) ? "{}" : recordJson);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var prop in recordDoc.RootElement.EnumerateObject())
            {
                if (prop.NameEquals("pointAccountManifest"))
                    continue;

                prop.WriteTo(writer);
            }

            writer.WritePropertyName("pointAccountManifest");
            manifestRoot.WriteTo(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
