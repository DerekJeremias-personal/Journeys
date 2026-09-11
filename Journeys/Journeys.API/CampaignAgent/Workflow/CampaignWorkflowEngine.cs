using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.Core.Workflow;

namespace Journeys.API.CampaignAgent.Workflow;

/// <summary>
/// Hybrid transitions: user overrides, approval gates, and successful tool outcomes.
/// </summary>
public static class CampaignWorkflowEngine
{
    public static CampaignWorkflowState ApplyUserMessage(CampaignWorkflowState state, string? userMessage, bool dataWarehouseEnabled = true)
    {
        CampaignWorkflowStepManager.ApplyUserMessage(state, userMessage, dataWarehouseEnabled);
        return state;
    }

    public static CampaignWorkflowState ApplyToolResults(
        CampaignWorkflowState state,
        IReadOnlyList<(string ToolName, string ResultJson)> toolResults,
        bool dataWarehouseEnabled = true) =>
        CampaignWorkflowStepManager.ApplyToolResults(state, toolResults, dataWarehouseEnabled);

    /// <summary>
    /// Decides whether the orchestrator should run one more in-turn "continuation hop" after a
    /// streamed segment. Continues when the brief gate or EventModels readiness gate just opened
    /// (so campaign mutators are re-offered on a fresh tool surface), the workflow is not terminal,
    /// and we have not already used a continuation hop this turn.
    /// </summary>
    public static bool ShouldContinueAfterSegment(
        CampaignWorkflowState state,
        int continuationHopsUsed)
    {
        if (CampaignJourneyDeliveryGuard.ShouldBlockContinuation(state))
            return false;

        if (state.Phase == CampaignWorkflowPhase.Done
            && VerificationDeliveryGuard.IsVerificationSatisfiedForWorkflowComplete(state))
            return false;
        if (continuationHopsUsed >= 2)
            return false;

        if (continuationHopsUsed >= 1)
        {
            if (state.Artifacts.EventModelsGateJustCleared
                && CampaignWorkflowChecklist.IsBriefCaptured(state))
            {
                if (!(IsPatBoundaryActive(state)
                      && PointAccountManifestBuilder.Parse(state.Artifacts.PointAccountManifest).Items.Count == 0))
                    return true;
            }

            return state.Artifacts.DeferredMutatorRetry
                   && !CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(state)
                   && CampaignWorkflowChecklist.IsBriefCaptured(state)
                   || state.Artifacts.PatUpsertPending
                      && !CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(state)
                      && PointAccountManifestBuilder.Parse(state.Artifacts.PointAccountManifest).Items.Count == 0
                      && CampaignWorkflowChecklist.IsBriefCaptured(state);
        }

        if (state.Artifacts.EventModelsGateJustCleared
            && CampaignWorkflowChecklist.IsBriefCaptured(state))
            return true;

        if (state.Artifacts.DeferredMutatorRetry
            && !CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(state)
            && CampaignWorkflowChecklist.IsBriefCaptured(state))
            return true;

        if (state.Artifacts.PatUpsertPending
            && !CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(state)
            && PointAccountManifestBuilder.Parse(state.Artifacts.PointAccountManifest).Items.Count == 0
            && CampaignWorkflowChecklist.IsBriefCaptured(state))
            return true;

        if (state.Artifacts.BriefGateJustCleared
            && CampaignWorkflowChecklist.IsBriefCaptured(state)
            && NeedsEventModelWork(state))
        {
            if (IsPatBoundaryActive(state))
                return false;

            return true;
        }

        if (VerificationDeliveryGuard.ShouldContinueForVerification(state, continuationHopsUsed))
            return true;

        if (!state.Artifacts.BriefGateJustCleared)
            return false;

        if (IsPatBoundaryActive(state))
            return false;

        return PendingEventModelSpecArtifact.Read(state) != null
               || state.Artifacts.UserRequestedNewEventModel;
    }

    /// <summary>
    /// Whether the host should rank event-model candidates (catalog fetch + selection gate) before
    /// the next model segment — including mid-turn after the design brief is first captured.
    /// </summary>
    public static bool ShouldRunEventModelPrep(CampaignWorkflowState state)
    {
        if (IsPatBoundaryActive(state))
            return false;

        return CampaignWorkflowChecklist.IsBriefCaptured(state)
               && NeedsEventModelWork(state)
               && !state.ModelGatePassed
               && state.Artifacts.AwaitingApproval == CampaignWorkflowApprovalKind.None
               && string.IsNullOrWhiteSpace(state.Artifacts.SelectedEventModelId)
               && EventModelCandidatesCatalogFallback.ShouldAttemptMcpCatalogFetch(state);
    }

    internal static bool IsPatBoundaryActive(CampaignWorkflowState state) =>
        WorkflowSkillRegistry.ResolveBuildSubStep(state) == CampaignBuildSubStep.PatRequired
        && !CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(state);

    internal static bool IsJourneyBoundaryActive(CampaignWorkflowState state) =>
        WorkflowSkillRegistry.ResolveBuildSubStep(state) == CampaignBuildSubStep.JourneyRequired
        && PointAccountManifestBuilder.Parse(state.Artifacts.PointAccountManifest).Items.Count > 0
        && JourneyPatternArtifacts.GetJourneyRuleSetCount(state) == 0
        && !CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(state);

    private static bool NeedsEventModelWork(CampaignWorkflowState state) =>
        !state.UserSkippedEventModels
        && state.CampaignKind != CampaignWorkflowKind.TagFirst
        && !CampaignWorkflowChecklist.IsItemComplete(state, CampaignWorkflowPhase.EventModels);

    /// <summary>
    /// Turn-start hybrid continuation: when gate is open, deferred retry is pending, and the user
    /// expresses proceed-to-create intent, returns an ephemeral directive for the first segment.
    /// </summary>
    public static string? TryPrepareTurnStartMutatorRetry(
        CampaignWorkflowState state,
        string? userMessage)
    {
        if (!CampaignWorkflowChecklist.IsBriefCaptured(state))
            return null;
        if (CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(state))
            return null;
        if (!state.Artifacts.DeferredMutatorRetry)
            return null;
        if (!MutatorRetryIntent.LooksLikeProceedToCreate(userMessage))
            return null;

        state.Artifacts.EventModelsGateJustCleared = true;
        ClearStalePhaseBlockedRemediation(state);

        var directive = BuildContinuationUserDirective(state);
        state.Artifacts.DeferredMutatorRetry = false;
        return directive;
    }

    /// <summary>
    /// Turn-start PAT handoff: when gate is open and PAT upserts are pending, inject directive without requiring user "proceed".
    /// </summary>
    public static string? TryPrepareTurnStartPatCreation(CampaignWorkflowState state)
    {
        if (!CampaignWorkflowChecklist.IsBriefCaptured(state))
            return null;
        if (CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(state))
            return null;
        if (!IsPatBoundaryActive(state))
            return null;
        if (PointAccountManifestBuilder.Parse(state.Artifacts.PointAccountManifest).Items.Count > 0)
            return null;
        if (state.Artifacts.AwaitingApproval != CampaignWorkflowApprovalKind.None)
            return null;

        state.Artifacts.PatUpsertPending = true;
        ClearStalePhaseBlockedRemediation(state);
        return BuildPatFirstDirective();
    }

    /// <summary>
    /// Turn-start journey authoring: when pattern skeleton is pinned, direct first validate from structure.
    /// </summary>
    public static string? TryPrepareTurnStartJourneyAuthoring(CampaignWorkflowState state)
    {
        if (!CampaignWorkflowChecklist.IsBriefCaptured(state))
            return null;
        if (!state.Artifacts.JourneyPatternPrepComplete)
            return null;
        if (string.IsNullOrWhiteSpace(state.Artifacts.JourneyPatternId))
            return null;
        if (PointAccountManifestBuilder.Parse(state.Artifacts.PointAccountManifest).Items.Count == 0)
            return null;
        if (JourneyPatternArtifacts.GetJourneyRuleSetCount(state) > 0)
            return null;
        if (state.Artifacts.AwaitingApproval == CampaignWorkflowApprovalKind.Journey)
            return null;
        if (state.Artifacts.ValidationStalled)
            return null;

        return $"Journey pattern '{state.Artifacts.JourneyPatternId}' is pinned in WORKFLOW ARTIFACTS. "
               + (string.IsNullOrWhiteSpace(state.Artifacts.JourneyAuthoringTemplateJson)
                   ? string.Empty
                   : "Author from JourneyAuthoringTemplate — journey.children[] ONLY. ")
               + "Use navigation.Entry with SimpleNavigationCriteria $type; "
               + "journey.children[] not nodes[]; PointBalanceProvider on tier-qual PAT. "
               + "Call validate_campaign once with the complete tree before upsert_campaign — do not list_example_campaigns. "
               + "DepositPointsOutcome: PathValueProvider event.ordertotal + PointsPerDollar — not AggregateValueProvider.";
    }

    private static string BuildPatFirstDirective() =>
        "Event model gate is open. Call upsert_point_account_type now (expired sink PATs first, "
        + "then earning PATs that reference them). Do not wait for user confirmation. "
        + "Do not defer to HTTP or pasted GUIDs.";

    private static void ClearStalePhaseBlockedRemediation(CampaignWorkflowState state)
    {
        var summary = state.Artifacts.LastToolRemediationSummary;
        if (string.IsNullOrWhiteSpace(summary))
            return;
        if (!summary.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return;

        state.Artifacts.LastToolRemediationSummary = null;
    }

    /// <summary>Ephemeral user directive for the in-turn continuation segment (not persisted).</summary>
    public static string BuildContinuationUserDirective(CampaignWorkflowState state)
    {
        if (state.Artifacts.VerificationUserTestIntentThisTurn
            && VerificationDeliveryGuard.ShouldSurfaceVerificationCoach(state)
            && !new Steps.VerificationExitCriteria().IsMet(state))
        {
            var snap = CreationSnapshotArtifact.Read(state);
            var campaignId = snap?.CampaignId;
            var campaignHint = !string.IsNullOrWhiteSpace(campaignId)
                ? $" Pass campaignId={campaignId} on process_event."
                : " Pass campaignId from THREAD CONTEXT on process_event.";
            return "User asked to test — call get_account then process_event with campaignId now; "
                   + "do not summarize or ask which account when draftTestAccounts are in SESSION."
                   + campaignHint;
        }

        if (IsPatBoundaryActive(state))
            return BuildPatFirstDirective();

        if (state.Artifacts.EventModelsGateJustCleared || state.Artifacts.PatUpsertPending)
        {
            var manifestEmpty = PointAccountManifestBuilder.Parse(state.Artifacts.PointAccountManifest).Items.Count == 0;
            var gateOpen = !CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(state);

            if (manifestEmpty && gateOpen)
            {
                if (state.Artifacts.DeferredMutatorRetry)
                {
                    return "Event model readiness gate just opened. Retry upsert_point_account_type immediately "
                           + "(expired sink PATs first). Do not defer PAT creation to HTTP or pasted GUIDs.";
                }

                return BuildPatFirstDirective();
            }

            if (state.Artifacts.DeferredMutatorRetry)
            {
                return "Event model readiness gate just opened. A prior upsert_campaign or "
                       + "upsert_point_account_type failed because mutators were not on the tool surface. "
                       + "Retry that deferred work immediately with the now-available mutators.";
            }

            if (state.Artifacts.EventModelsGateJustCleared)
            {
                return "Event model readiness gate just opened. Campaign mutators (upsert_campaign, "
                       + "upsert_point_account_type) are now available. Continue immediately with deferred "
                       + "shell, point-account, or journey work from this turn without waiting for user confirmation.";
            }
        }

        if (state.Artifacts.BriefGateJustCleared)
            return BuildBriefGateContinuationDirective(state);

        if (WorkflowSkillRegistry.ResolveActiveSkill(state) == CampaignWorkflowSkill.CampaignBuild
            && CreationSnapshotArtifact.Read(state) is not { CreationComplete: true })
        {
            return "CampaignBuild continuation: " + WorkflowSkillRegistry.BuildNextStepLine(state);
        }

        return "The design brief was just captured and mutating tools are now available. Continue immediately "
               + "with any deferred work (e.g. pending event model) without waiting for user confirmation.";
    }

    private static string BuildBriefGateContinuationDirective(CampaignWorkflowState state)
    {
        var set = EventModelCandidatesArtifact.Read(state);
        if (set?.FetchFailed == true)
        {
            return "The design brief was just captured. Event model catalog was unavailable — resolve the "
                   + "event model with get_model (or save_model if missing), then continue to campaign shell "
                   + "and journey work. Do not pivot to read-only list_campaigns discovery.";
        }

        if (set?.Candidates.Count > 0 && !string.IsNullOrWhiteSpace(set.RecommendedDefaultId))
        {
            var rec = set.Candidates.FirstOrDefault(c =>
                string.Equals(c.EventModelId, set.RecommendedDefaultId, StringComparison.OrdinalIgnoreCase));
            var name = rec?.Name ?? rec?.DisplayName ?? "recommended event model";
            var id = set.RecommendedDefaultId;
            if (rec?.IsStrongMatch == true)
            {
                return $"The design brief was just captured. Recommended event model is '{name}' ({id}). "
                       + "Call get_model to confirm tag eventable and metadata. Ask one-line confirmation only "
                       + "if needed, then continue to campaign shell and journey — do not pivot to read-only "
                       + "list_campaigns/get_campaign discovery.";
            }

            return $"The design brief was just captured. Recommended event model is '{name}' ({id}). "
                   + "Call get_model to confirm, present a brief choice if needed, then continue to campaign "
                   + "shell work.";
        }

        if (PendingEventModelSpecArtifact.Read(state) != null)
        {
            return "The design brief was just captured. Continue immediately with the pending event model "
                   + "(get_model or save_model) before campaign shell work.";
        }

        if (state.Artifacts.UserRequestedNewEventModel)
        {
            return "The design brief was just captured. Continue immediately with save_model for the new event "
                   + "model before campaign shell work.";
        }

        return "The design brief was just captured. Resolve the event model (get_model existing or save_model "
               + "new) before campaign shell and journey work — do not pivot to read-only discovery.";
    }
}
