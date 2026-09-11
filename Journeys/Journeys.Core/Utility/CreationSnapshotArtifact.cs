using System.Text.Json;
using Journeys.CampaignAgent.Remediation;
using Journeys.Core.Models;
using Journeys.DTO.Models;

namespace Journeys.Core.Utility;

/// <summary>Read/write compact creation-complete snapshot on workflow artifacts.</summary>
public static class CreationSnapshotArtifact
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static CampaignCreationSnapshotDto? Read(CampaignWorkflowState state) =>
        Read(state.Artifacts.CreationSnapshot);

    public static CampaignCreationSnapshotDto? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<CampaignCreationSnapshotDto>(json, JsonOpts);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool IsCreationComplete(CampaignWorkflowState state)
    {
        var snap = Read(state);
        return snap is { CreationComplete: true };
    }

    public static void Clear(CampaignWorkflowArtifactsDocument artifacts)
    {
        artifacts.CreationSnapshot = null;
        artifacts.DeferredMutatorRetry = false;
    }

    public static void MergePatFromUpsertResult(CampaignWorkflowState state, string patJson)
    {
        patJson = ToolResultJsonNormalizer.Unwrap(patJson) ?? patJson;
        if (!ToolResultSuccessEvaluator.LooksSuccessful(patJson))
            return;

        if (!TryReadPatFields(patJson, out var id, out var name, out var ledger))
            return;

        var snap = Read(state) ?? new CampaignCreationSnapshotDto();
        if (!snap.PointAccountTypes.Any(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)))
        {
            snap.PointAccountTypes.Add(new PointAccountSnapshotItemDto
            {
                Id = id,
                Name = name,
                LedgerType = ledger
            });
        }

        RecomputeCreationComplete(snap);
        Write(state, snap);
    }

    public static void MergeFromJourneyUpsert(
        CampaignWorkflowState state,
        string? campaignId,
        string? campaignStatus,
        int ruleSetCount,
        int outcomeCount,
        string? externalCampaignId = null)
    {
        var snap = Read(state) ?? new CampaignCreationSnapshotDto();
        if (!string.IsNullOrWhiteSpace(campaignId))
            snap.CampaignId = campaignId;
        if (!string.IsNullOrWhiteSpace(externalCampaignId))
            snap.CampaignExternalId = externalCampaignId;
        if (!string.IsNullOrWhiteSpace(campaignStatus))
            snap.CampaignStatus = campaignStatus;
        snap.JourneyRuleSetCount = ruleSetCount;
        snap.JourneyOutcomeCount = outcomeCount;
        RecomputeCreationComplete(snap);
        Write(state, snap);
    }

    public static void MergePatsFromWorkflowManifest(CampaignWorkflowState state)
    {
        var manifest = PointAccountManifestBuilder.Parse(state.Artifacts.PointAccountManifest);
        if (manifest.Items.Count == 0)
            return;

        var snap = Read(state) ?? new CampaignCreationSnapshotDto();
        foreach (var item in manifest.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
                continue;
            if (snap.PointAccountTypes.Any(p => string.Equals(p.Id, item.Id, StringComparison.OrdinalIgnoreCase)))
                continue;

            snap.PointAccountTypes.Add(new PointAccountSnapshotItemDto
            {
                Id = item.Id,
                Name = item.DisplayLabel,
                LedgerType = item.LedgerType
            });
        }

        RecomputeCreationComplete(snap);
        Write(state, snap);
    }

    public static void MergeShellOnly(
        CampaignWorkflowState state,
        string? campaignId,
        string? campaignStatus,
        string? externalCampaignId = null)
    {
        if (string.IsNullOrWhiteSpace(campaignId)
            && string.IsNullOrWhiteSpace(campaignStatus)
            && string.IsNullOrWhiteSpace(externalCampaignId))
            return;

        var snap = Read(state) ?? new CampaignCreationSnapshotDto();
        if (!string.IsNullOrWhiteSpace(campaignId))
            snap.CampaignId = campaignId;
        if (!string.IsNullOrWhiteSpace(externalCampaignId))
            snap.CampaignExternalId = externalCampaignId;
        if (!string.IsNullOrWhiteSpace(campaignStatus))
            snap.CampaignStatus = campaignStatus;
        RecomputeCreationComplete(snap);
        Write(state, snap);
    }

    private static void RecomputeCreationComplete(CampaignCreationSnapshotDto snap)
    {
        snap.CreationComplete = !string.IsNullOrWhiteSpace(snap.CampaignId)
                                && snap.JourneyRuleSetCount > 0
                                && snap.PointAccountTypes.Count > 0;
    }

    private static void Write(CampaignWorkflowState state, CampaignCreationSnapshotDto snap)
    {
        var wasComplete = Read(state) is { CreationComplete: true };
        state.Artifacts.CreationSnapshot = JsonSerializer.Serialize(snap, JsonOpts);
        if (snap.CreationComplete && !wasComplete)
            VerificationRecordManifestBridge.SeedOnCreationComplete(state);
    }

    private static bool TryReadPatFields(string patJson, out string id, out string? name, out string? ledger)
    {
        id = string.Empty;
        name = null;
        ledger = null;
        try
        {
            using var doc = JsonDocument.Parse(patJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            id = ReadString(root, "Id") ?? ReadString(root, "id") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(id))
                return false;

            name = ReadString(root, "Name") ?? ReadString(root, "name");
            ledger = ReadString(root, "LedgerType") ?? ReadString(root, "ledgerType");
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ReadString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;
}
