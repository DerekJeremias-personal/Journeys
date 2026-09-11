using System.Text.Json;
using Journeys.Core.Models;
using Journeys.Core.Utility;

namespace Journeys.API.CampaignAgent.Workflow;

public static class LivePromotionGuard
{
    public static bool Enabled { get; set; } = true;

    public static void ApplyUserMessage(CampaignWorkflowState state, string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (WorkflowUserPhraseCatalog.ContainsAny(message, WorkflowUserPhraseCatalog.LivePromotionPhrases))
        {
            state.Artifacts.LivePromotionApprovedThisSession = true;
            if (state.Artifacts.AwaitingApproval == CampaignWorkflowApprovalKind.LivePromotion)
                state.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.None;
        }
    }

    public static bool IsLivePromotionAttempt(CampaignWorkflowState state, string? campaignJson)
    {
        if (!TryReadStatus(campaignJson, out var status))
            return false;
        if (!string.Equals(status, CampaignStatusStrings.Live, StringComparison.OrdinalIgnoreCase))
            return false;

        var snap = CreationSnapshotArtifact.Read(state);
        var prior = snap?.CampaignStatus ?? CampaignStatusStrings.Draft;
        return !string.Equals(prior, CampaignStatusStrings.Live, StringComparison.OrdinalIgnoreCase);
    }

    public static bool ShouldBlockLivePromotion(CampaignWorkflowState state, string? campaignJson)
    {
        if (!Enabled)
            return false;
        if (!IsLivePromotionAttempt(state, campaignJson))
            return false;
        return !state.Artifacts.LivePromotionApprovedThisSession;
    }

    public static void ApplyBlock(CampaignWorkflowState state)
    {
        state.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.LivePromotion;
        state.Artifacts.LastToolRemediationSummary =
            "Live promotion requires explicit user approval. Verify Draft first via process_event(campaignId). "
            + "Reply 'promote to Live' when ready.";
    }

    public static string BuildBlockedToolResult() =>
        """{"errors":{"campaign.status.0":"[violation=LIVE_PROMOTION_REQUIRES_APPROVAL] Live promotion blocked — verify Draft with process_event(campaignId) first; user must explicitly approve promotion."}}""";

    private static bool TryReadStatus(string? campaignJson, out string? status)
    {
        status = null;
        if (string.IsNullOrWhiteSpace(campaignJson))
            return false;
        try
        {
            using var doc = JsonDocument.Parse(campaignJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("status", out var s) && s.ValueKind == JsonValueKind.String)
                status = s.GetString();
            else if (root.TryGetProperty("Status", out var p) && p.ValueKind == JsonValueKind.String)
                status = p.GetString();
            return !string.IsNullOrWhiteSpace(status);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
