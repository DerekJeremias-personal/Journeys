using System.Text.Json;
using Journeys.CampaignAgent.Remediation;
using Journeys.Core.Models;
using Journeys.DTO.Models;

namespace Journeys.Core.Utility;

/// <summary>
/// Updates workflow artifacts from full upsert JSON or mutation digest ack shapes.
/// </summary>
public static class CreationArtifactBridge
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public sealed class UpsertArtifactOutcome
    {
        public bool JourneyDigestApplied { get; init; }

        public string? JourneyDigestJson { get; init; }

        public string? ShellDigestJson { get; init; }
    }

    public static UpsertArtifactOutcome TryApplyUpsertOutcome(CampaignWorkflowState state, string upsertJson)
    {
        ArgumentNullException.ThrowIfNull(state);
        upsertJson = ToolResultJsonNormalizer.Unwrap(upsertJson) ?? upsertJson;

        if (!ToolResultSuccessEvaluator.LooksSuccessful(upsertJson))
            return new UpsertArtifactOutcome();

        if (CampaignJourneyArtifactDigestBuilder.TryBuildFromUpsertResult(
                upsertJson, state.Artifacts.PointAccountManifest, out var journeyDigest))
        {
            ApplyJourneyDigest(state, journeyDigest);
            return new UpsertArtifactOutcome { JourneyDigestApplied = true, JourneyDigestJson = journeyDigest };
        }

        if (TryApplyMutationAck(state, upsertJson, out var ackOutcome))
            return ackOutcome;

        var eligibility = (IReadOnlyDictionary<string, bool>?)EventModelContractsAccumulator.BuildProcessEventEligibilityMap(state);
        if (CampaignShellDigestBuilder.TryBuildFromUpsertResult(upsertJson, out var shell, eligibility))
        {
            state.Artifacts.CampaignShellRef = shell;
            TryMergeShellFromDigestJson(state, shell);
            return new UpsertArtifactOutcome { ShellDigestJson = shell };
        }

        state.Artifacts.CampaignShellRef = Truncate(upsertJson, 2000);
        return new UpsertArtifactOutcome();
    }

    private static bool TryApplyMutationAck(
        CampaignWorkflowState state,
        string upsertJson,
        out UpsertArtifactOutcome outcome)
    {
        outcome = new UpsertArtifactOutcome();
        try
        {
            using var doc = JsonDocument.Parse(upsertJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !LooksLikeMutationAck(root))
                return false;

            if (!root.TryGetProperty("campaign", out var campaignEl) || campaignEl.ValueKind != JsonValueKind.Object)
                return false;

            var campaignId = ReadString(campaignEl, "Id") ?? ReadString(campaignEl, "id");
            var status = ReadString(campaignEl, "Status") ?? ReadString(campaignEl, "status");

            var shell = BuildShellFromAckCampaign(campaignEl, campaignId, status);
            state.Artifacts.CampaignShellRef = JsonSerializer.Serialize(shell, JsonOpts);

            if (!root.TryGetProperty("journey", out var journeyEl) || journeyEl.ValueKind != JsonValueKind.Object)
            {
                CreationSnapshotArtifact.MergeShellOnly(state, campaignId, status,
                ReadString(campaignEl, "ExtCampaignId") ?? ReadString(campaignEl, "extCampaignId"));
                outcome = new UpsertArtifactOutcome { ShellDigestJson = state.Artifacts.CampaignShellRef };
                return true;
            }

            var (ruleSetCount, outcomeCount, nodeCount) = CountAckJourney(journeyEl);

            var journeyDigest = new CampaignJourneyArtifactDigest
            {
                CampaignId = campaignId ?? string.Empty,
                JourneyNodeCount = nodeCount,
                RuleSetCount = ruleSetCount,
                OutcomeKindCounts = outcomeCount > 0
                    ? new Dictionary<string, int> { ["DepositPointsOutcome"] = outcomeCount }
                    : new Dictionary<string, int>(),
                ReferencedPointAccountTypes = [],
                UnresolvedPatIds = []
            };

            var journeyJson = JsonSerializer.Serialize(journeyDigest, JsonOpts);
            ApplyJourneyDigest(state, journeyJson);
            CreationSnapshotArtifact.MergeFromJourneyUpsert(state, campaignId, status, ruleSetCount, outcomeCount,
                ReadString(campaignEl, "ExtCampaignId") ?? ReadString(campaignEl, "extCampaignId"));
            CreationSnapshotArtifact.MergePatsFromWorkflowManifest(state);

            outcome = new UpsertArtifactOutcome
            {
                JourneyDigestApplied = ruleSetCount > 0,
                JourneyDigestJson = journeyJson,
                ShellDigestJson = state.Artifacts.CampaignShellRef
            };
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool LooksLikeMutationAck(JsonElement root)
    {
        if (root.TryGetProperty("note", out var note)
            && note.ValueKind == JsonValueKind.String
            && note.GetString()?.Contains("Full campaign definition suppressed", StringComparison.OrdinalIgnoreCase) == true)
            return true;

        return root.TryGetProperty("campaign", out var c) && c.ValueKind == JsonValueKind.Object
               && root.TryGetProperty("journey", out var j) && j.ValueKind == JsonValueKind.Object
               && !root.TryGetProperty("Journey", out _);
    }

    private static CampaignShellDigest BuildShellFromAckCampaign(
        JsonElement campaignEl,
        string? campaignId,
        string? status)
    {
        var events = new List<string>();
        if (campaignEl.TryGetProperty("Events", out var eventsEl) && eventsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in eventsEl.EnumerateArray())
            {
                if (e.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(e.GetString()))
                    events.Add(e.GetString()!);
            }
        }

        return new CampaignShellDigest
        {
            CampaignId = campaignId ?? string.Empty,
            Name = ReadString(campaignEl, "Name") ?? ReadString(campaignEl, "name"),
            Status = status ?? string.Empty,
            ExtCampaignId = ReadString(campaignEl, "ExtCampaignId") ?? ReadString(campaignEl, "extCampaignId"),
            EventModelIds = events,
            StartDateUtc = ReadString(campaignEl, "StartDate") ?? ReadString(campaignEl, "startDate"),
            HasJourneyPayload = campaignEl.TryGetProperty("journey", out _)
        };
    }

    private static (int RuleSetCount, int OutcomeCount, int NodeCount) CountAckJourney(JsonElement journeyEl)
    {
        var ruleSetCount = 0;
        var outcomeCount = 0;
        var nodeCount = 0;

        if (!journeyEl.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array)
            return (0, 0, 0);

        foreach (var node in nodes.EnumerateArray())
            WalkAckNode(node, ref nodeCount, ref ruleSetCount, ref outcomeCount);

        return (ruleSetCount, outcomeCount, nodeCount);
    }

    private static void WalkAckNode(JsonElement node, ref int nodeCount, ref int ruleSetCount, ref int outcomeCount)
    {
        nodeCount++;
        if (node.TryGetProperty("ruleSets", out var ruleSets) && ruleSets.ValueKind == JsonValueKind.Array)
        {
            foreach (var rs in ruleSets.EnumerateArray())
            {
                ruleSetCount++;
                if (rs.TryGetProperty("outcomes", out var oc) && oc.ValueKind == JsonValueKind.Number)
                    outcomeCount += oc.GetInt32();
            }
        }

        if (node.TryGetProperty("nodes", out var nested) && nested.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in nested.EnumerateArray())
                WalkAckNode(child, ref nodeCount, ref ruleSetCount, ref outcomeCount);
        }
    }

    private static void ApplyJourneyDigest(CampaignWorkflowState state, string journeyDigest)
    {
        state.Artifacts.JourneyDigestProposed = journeyDigest;
        state.Artifacts.JourneyDigestApproved = journeyDigest;
        state.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.None;

        try
        {
            using var doc = JsonDocument.Parse(journeyDigest);
            var root = doc.RootElement;
            var campaignId = ReadString(root, "campaignId");
            var ruleSets = root.TryGetProperty("ruleSetCount", out var rs) && rs.ValueKind == JsonValueKind.Number
                ? rs.GetInt32()
                : 0;
            var outcomes = 0;
            if (root.TryGetProperty("outcomeKindCounts", out var okc) && okc.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in okc.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Number)
                        outcomes += prop.Value.GetInt32();
                }
            }

            string? status = null;
            if (!string.IsNullOrWhiteSpace(state.Artifacts.CampaignShellRef))
            {
                using var shellDoc = JsonDocument.Parse(state.Artifacts.CampaignShellRef);
                status = ReadString(shellDoc.RootElement, "status") ?? ReadString(shellDoc.RootElement, "Status");
            }

            CreationSnapshotArtifact.MergeFromJourneyUpsert(state, campaignId, status, ruleSets, outcomes);
            CreationSnapshotArtifact.MergePatsFromWorkflowManifest(state);
        }
        catch (JsonException)
        {
            // Journey digest JSON malformed; snapshot update is best-effort.
        }
    }

    private static void TryMergeShellFromDigestJson(CampaignWorkflowState state, string shellJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(shellJson);
            var root = doc.RootElement;
            CreationSnapshotArtifact.MergeShellOnly(
                state,
                ReadString(root, "campaignId") ?? ReadString(root, "CampaignId"),
                ReadString(root, "status") ?? ReadString(root, "Status"),
                ReadString(root, "extCampaignId") ?? ReadString(root, "ExtCampaignId"));
        }
        catch (JsonException)
        {
        }
    }

    private static string? ReadString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
