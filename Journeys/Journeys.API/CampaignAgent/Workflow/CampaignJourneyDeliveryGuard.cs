using Journeys.Core.Models;
using Journeys.Core.Utility;
using System.Text.Json;

namespace Journeys.API.CampaignAgent.Workflow;

public static class CampaignJourneyDeliveryGuard
{
    /// <summary>Set from orchestrator each turn from CampaignAgent:JourneyDeliveryGuard:Enabled.</summary>
    public static bool Enabled { get; set; } = true;

    public static bool ShouldBlockContinuation(CampaignWorkflowState state) =>
        Enabled && BlocksJourneyMutators(state);

    public static bool BlocksJourneyMutators(CampaignWorkflowState state) =>
        Enabled && (
            state.Artifacts.AwaitingApproval == CampaignWorkflowApprovalKind.Journey
            || state.Artifacts.ValidationStalled);

    public static void TrySetJourneyEntryCheckpoint(CampaignWorkflowState state, string? userMessageThisTurn)
    {
        if (!Enabled)
            return;
        if (state.Artifacts.AwaitingApproval != CampaignWorkflowApprovalKind.None)
            return;
        if (state.Artifacts.ValidationStalled)
            return;
        if (!HasMinimumCreationProgress(state))
            return;
        if (GetJourneyRuleSetCount(state) > 0)
            return;
        if (JourneyProceedIntent.LooksLikeProceedToJourney(userMessageThisTurn))
            return;

        var focus = CampaignWorkflowFocusResolver.CoachDefaultFocus(state);
        if (focus != CampaignWorkflowPhase.CampaignJourney
            && state.Phase != CampaignWorkflowPhase.CampaignJourney)
            return;

        state.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.Journey;
    }

    public static void ClearUserTurnGuards(CampaignWorkflowState state)
    {
        if (state.Artifacts.AwaitingApproval == CampaignWorkflowApprovalKind.Journey)
            state.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.None;

        state.Artifacts.ValidationStalled = false;
        state.Artifacts.ValidationStallCycleCount = 0;
        state.Artifacts.ValidationStallTrippedAtUtc = null;
    }

    public static string BuildCheckpointPromptLine(CampaignWorkflowState state)
    {
        var snap = CreationSnapshotArtifact.Read(state);
        var campaignId = snap?.CampaignId
            ?? TryReadShellCampaignId(state.Artifacts.CampaignShellRef)
            ?? "campaign";
        var patCount = CountPatManifest(state);
        var ruleSetCount = GetJourneyRuleSetCount(state);
        return $"Journey checkpoint — campaign shell and point account types are saved ({campaignId}, {patCount} PATs). "
               + $"Journey rules are not persisted yet (ruleSetCount: {ruleSetCount}). "
               + "Reply with tier names/thresholds or say \"continue\" to author the journey in the next turn.";
    }

    private static bool HasMinimumCreationProgress(CampaignWorkflowState state) =>
        !string.IsNullOrWhiteSpace(state.Artifacts.CampaignShellRef)
        && CountPatManifest(state) > 0;

    private static int CountPatManifest(CampaignWorkflowState state)
    {
        var snap = CreationSnapshotArtifact.Read(state);
        if (snap?.PointAccountTypes.Count > 0)
            return snap.PointAccountTypes.Count;

        return string.IsNullOrWhiteSpace(state.Artifacts.PointAccountManifest) ? 0 : 1;
    }

    private static int GetJourneyRuleSetCount(CampaignWorkflowState state) =>
        CreationSnapshotArtifact.Read(state)?.JourneyRuleSetCount ?? 0;

    private static string? TryReadShellCampaignId(string? shellRefJson)
    {
        if (string.IsNullOrWhiteSpace(shellRefJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(shellRefJson);
            if (doc.RootElement.TryGetProperty("campaignId", out var id))
                return id.GetString();
        }
        catch (JsonException)
        {
            // ignore malformed shell ref
        }

        return null;
    }
}
