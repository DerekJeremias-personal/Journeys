using Journeys.API.CampaignAgent;
using Journeys.CampaignAgent.Remediation;
using Journeys.Core.Models;

using Journeys.Core.Utility;



namespace Journeys.API.CampaignAgent.Workflow;



/// <summary>

/// Applies user/tool outcomes, updates checklist state, and resolves coaching focus.

/// </summary>

public static class CampaignWorkflowStepManager

{

    private static readonly HashSet<string> ModelResolutionToolNames = new(StringComparer.OrdinalIgnoreCase)

    {

        "GetModel", "get_model", "GetManyModels", "get_many_models", "SaveModel", "save_model"

    };



    public static void ApplyUserMessage(CampaignWorkflowState state, string? userMessage, bool dataWarehouseEnabled = true)

    {
        var hadBrief = CampaignWorkflowChecklist.IsBriefCaptured(state);

        if (string.IsNullOrWhiteSpace(userMessage))

        {

            TryUpdateChecklist(state);

            state.Phase = CampaignWorkflowPhaseNormalizer.Normalize(CampaignWorkflowFocusResolver.CoachDefaultFocus(state));

            return;

        }



        var msg = userMessage.Trim();

        CampaignJourneyDeliveryGuard.ClearUserTurnGuards(state);

        TryCaptureDeferredIntent(state, msg);
        VerificationCoach.TryCaptureTestAccountFromUserMessage(state, msg);
        VerificationDebugContext.TryCaptureSteeringFromUserMessage(state, msg);
        LivePromotionGuard.ApplyUserMessage(state, msg);
        EventModelReuseHelper.TryConfirmReuseFromUserMessage(state, msg);

        var selectionAmbiguous = false;

        if (state.Artifacts.AwaitingApproval == CampaignWorkflowApprovalKind.EventModelSelection)
            selectionAmbiguous = TryResolveEventModelSelection(state, msg);

        if (!selectionAmbiguous)
        {
            if (WorkflowUserPhraseCatalog.ContainsAny(msg, WorkflowUserPhraseCatalog.RewindDataPhrases))

                RewindTo(state, CampaignWorkflowPhase.DataAnalysis);

            else if (WorkflowUserPhraseCatalog.ContainsAny(msg, WorkflowUserPhraseCatalog.RewindEventModelPhrases))

                RewindTo(state, CampaignWorkflowPhase.EventModels);

            else if (WorkflowUserPhraseCatalog.ContainsAny(msg, WorkflowUserPhraseCatalog.TagFirstPhrases))

            {

                state.CampaignKind = CampaignWorkflowKind.TagFirst;

                state.UserSkippedEventModels = true;

                state.ModelGatePassed = true;

            }

            if (WorkflowUserPhraseCatalog.ContainsAny(msg, WorkflowUserPhraseCatalog.AbandonPendingModelPhrases))

            {

                PendingEventModelSpecArtifact.Clear(state);

                TryUpdateChecklist(state);

            }

            if (WorkflowUserPhraseCatalog.ContainsAny(msg, WorkflowUserPhraseCatalog.ApprovalPhrases)

                && CanBuildBriefFromCurrentState(state, dataWarehouseEnabled))

            {

                if (string.IsNullOrEmpty(state.Artifacts.CampaignDesignBriefProposed))

                    state.Artifacts.CampaignDesignBriefProposed = CampaignWorkflowArtifactBuilder.BuildDesignBrief(state);

                state.Artifacts.DataAnalysisObjectiveCaptured = true;

                state.Artifacts.CampaignDesignBriefApproved = state.Artifacts.CampaignDesignBriefProposed;

                state.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.None;

            }
        }

        TryUpdateChecklist(state);

        state.Phase = CampaignWorkflowPhaseNormalizer.Normalize(CampaignWorkflowFocusResolver.Resolve(state, msg));

        if (!hadBrief && CampaignWorkflowChecklist.IsBriefCaptured(state))
            state.Artifacts.BriefGateJustCleared = true;

    }



    public static CampaignWorkflowState ApplyToolResults(

        CampaignWorkflowState state,

        IReadOnlyList<(string ToolName, string ResultJson)> toolResults,

        bool dataWarehouseEnabled = true)

    {
        var hadBrief = CampaignWorkflowChecklist.IsBriefCaptured(state);
        var eventModelsGateWasClosed = CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(state);

        foreach (var (toolName, json) in toolResults)
        {
            ApplySingleToolOutcome(state, toolName, json, dataWarehouseEnabled, eventModelsGateWasClosed);

            if (eventModelsGateWasClosed
                && CampaignWorkflowChecklist.IsBriefCaptured(state)
                && !CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(state))
            {
                state.Artifacts.EventModelsGateJustCleared = true;
                TrySetPatUpsertPendingOnGateOpened(state);
            }
        }

        TryCompleteDataAnalysisPhase(state);

        TryUpdateChecklist(state);

        state.Phase = CampaignWorkflowPhaseNormalizer.Normalize(CampaignWorkflowFocusResolver.Resolve(state, null));

        if (CampaignWorkflowEngine.IsPatBoundaryActive(state))
            state.Phase = CampaignWorkflowPhase.CampaignBuild;

        if (CampaignWorkflowEngine.IsJourneyBoundaryActive(state))
            state.Phase = CampaignWorkflowPhase.CampaignJourney;

        if (!hadBrief && CampaignWorkflowChecklist.IsBriefCaptured(state))
            state.Artifacts.BriefGateJustCleared = true;

        if (eventModelsGateWasClosed
            && CampaignWorkflowChecklist.IsBriefCaptured(state)
            && !CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(state))
        {
            state.Artifacts.EventModelsGateJustCleared = true;
            TrySetPatUpsertPendingOnGateOpened(state);
        }

        return state;

    }



    /// <summary>
    /// Re-applies event-model selection from the current user message when the selection gate
    /// was raised after <see cref="ApplyUserMessage"/> (same orchestrator turn).
    /// </summary>
    public static bool TryApplyDeferredEventModelSelection(CampaignWorkflowState state, string? userMessage)
    {
        if (state.Artifacts.AwaitingApproval != CampaignWorkflowApprovalKind.EventModelSelection)
            return false;
        if (string.IsNullOrWhiteSpace(userMessage))
            return false;

        if (TryResolveEventModelSelection(state, userMessage.Trim()))
            return false;

        TryUpdateChecklist(state);
        state.Phase = CampaignWorkflowPhaseNormalizer.Normalize(CampaignWorkflowFocusResolver.Resolve(state, userMessage));
        return true;
    }

    public static void TryUpdateChecklist(CampaignWorkflowState state)
    {
        if (CampaignWorkflowChecklist.IsWorkflowComplete(state))
            state.Phase = CampaignWorkflowPhase.Done;

        if (CampaignWorkflowChecklist.IsItemComplete(state, CampaignWorkflowPhase.EventModels))
            state.ModelGatePassed = true;

        if (CreationSnapshotArtifact.IsCreationComplete(state))
            state.Artifacts.PatUpsertPending = false;

        JourneyPatternArtifacts.ClearIfJourneyPersisted(state);
        JourneyContractSummaryPinArtifacts.RefreshEpisodeState(state);
    }

    public static string? GetCoachHint(CampaignWorkflowState state, IReadOnlyList<string>? toolsThisTurn = null)
    {
        if (!CampaignWorkflowChecklist.IsBriefCaptured(state))

        {

            var pending = PendingEventModelSpecArtifact.Read(state);

            return pending != null
                ? $"Coach: capture the campaign brief first; then create pending event model '{pending.Name}'."
                : "Coach: establish campaign objective and persist the design brief before mutations.";

        }

        if (EventModelsGateClosedCoach.ShouldApply(state))
        {
            var drift = EventModelWorkflowDriftCoach.TryGetHint(state);
            if (!string.IsNullOrWhiteSpace(drift))
                return drift;

            var gateClosed = EventModelsGateClosedCoach.TryGetHint(state);
            if (!string.IsNullOrWhiteSpace(gateClosed))
                return gateClosed;

            var readiness = EventModelsReadiness.Evaluate(state);
            if (!readiness.IsReady && !string.IsNullOrWhiteSpace(readiness.CoachMessage))
                return readiness.CoachMessage;
        }

        if (CampaignBuildCoach.ShouldApply(state))
        {
            var buildCoach = CampaignBuildCoach.TryGetHint(state, toolsThisTurn);
            if (!string.IsNullOrWhiteSpace(buildCoach))
                return buildCoach;
        }

        if (VerificationDeliveryGuard.ShouldSurfaceVerificationCoach(state))
        {
            var verificationCoach = VerificationCoach.GetCoachHint(state);
            if (!string.IsNullOrWhiteSpace(verificationCoach))
                return verificationCoach;
        }

        var stallHint = CampaignValidationCoach.GetStallCoachHint(state);
        if (!string.IsNullOrWhiteSpace(stallHint))
            return stallHint;

        var journeyCoach = JourneyCoach.GetCoachHint(state);
        if (!string.IsNullOrWhiteSpace(journeyCoach))
            return journeyCoach;

        var validationCoach = CampaignValidationCoach.GetCoachHint(state);
        if (!string.IsNullOrWhiteSpace(validationCoach))
            return validationCoach;

        if (!CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(state)
            && state.Artifacts.DeferredMutatorRetry
            && PointAccountManifestBuilder.Parse(state.Artifacts.PointAccountManifest).Items.Count > 0
            && LooksLikePhaseBlockedMutatorSummary(state.Artifacts.LastToolRemediationSummary))
        {
            return "Coach: Event model gate is open. A prior upsert_campaign or upsert_point_account_type "
                   + "failed because mutators were filtered from the tool surface — not because MCP is missing. "
                   + "Retry shell/PAT creation now with upsert tools; do not pivot to read-only discovery.";
        }

        if (!string.IsNullOrWhiteSpace(state.Artifacts.LastToolRemediationSummary))
            return "Last remediation: " + state.Artifacts.LastToolRemediationSummary;

        return null;

    }

    [Obsolete("Use GetCoachHint instead.")]
    public static string? GetBlockingReason(CampaignWorkflowState state) => GetCoachHint(state);



    private static void TryCaptureDeferredIntent(CampaignWorkflowState state, string msg)

    {

        PendingEventModelSpecArtifact.TryCaptureFromUserMessage(state, msg);

        if (EventModelCreateNewIntent.LooksLikeRequest(msg))

            state.Artifacts.UserRequestedNewEventModel = true;

    }



    private static bool TryResolveEventModelSelection(CampaignWorkflowState state, string msg)

    {

        var set = EventModelCandidatesArtifact.Read(state);

        var result = EventModelSelectionParser.Resolve(msg, set);

        switch (result.Kind)

        {

            case EventModelSelectionKind.PickExisting:

                state.Artifacts.SelectedEventModelId = result.SelectedEventModelId;

                state.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.None;

                if (!string.IsNullOrWhiteSpace(result.SelectedEventModelId))

                    EventModelContractsAccumulator.PromoteDiscoveredToResolved(state, result.SelectedEventModelId);

                return false;

            case EventModelSelectionKind.CreateNew:

                state.Artifacts.SelectedEventModelId = null;

                state.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.None;

                return false;

            case EventModelSelectionKind.Ambiguous:

            default:

                return true;

        }

        return true;

    }



    private static void RewindTo(CampaignWorkflowState state, CampaignWorkflowPhase phase)

    {

        state.Phase = CampaignWorkflowPhaseNormalizer.Normalize(phase);

        state.Artifacts.ArtifactVersion++;

        state.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.None;

        CampaignValidationCoach.ClearValidationStall(state);
        CampaignValidationCoach.ClearLastValidateDeliveryArtifacts(state);

        switch (phase)

        {

            case CampaignWorkflowPhase.DataAnalysis:

                EventModelContractsAccumulator.Clear(state);

                EventModelCandidatesArtifact.Clear(state);

                PendingEventModelSpecArtifact.Clear(state);

                state.Artifacts.DataAnalysisWarehouseSucceeded = false;

                state.Artifacts.DataAnalysisObjectiveCaptured = false;

                state.Artifacts.CampaignDesignBriefProposed = null;

                state.Artifacts.CampaignDesignBriefApproved = null;

                state.Artifacts.EventModelSaveFailureCount = 0;

                state.Artifacts.CampaignShellRef = null;

                state.Artifacts.PointAccountManifest = null;

                state.Artifacts.JourneyDigestProposed = null;

                state.Artifacts.JourneyDigestApproved = null;

                VerificationCoach.ClearVerificationArtifacts(state.Artifacts);
                CreationSnapshotArtifact.Clear(state.Artifacts);

                state.ModelGatePassed = false;
                state.Artifacts.EventModelsGateJustCleared = false;

                break;

            case CampaignWorkflowPhase.EventModels:

                EventModelContractsAccumulator.Clear(state);

                EventModelCandidatesArtifact.Clear(state);

                state.Artifacts.EventModelSaveFailureCount = 0;

                state.Artifacts.CampaignShellRef = null;

                state.Artifacts.PointAccountManifest = null;

                state.Artifacts.JourneyDigestProposed = null;

                state.Artifacts.JourneyDigestApproved = null;

                VerificationCoach.ClearVerificationArtifacts(state.Artifacts);
                CreationSnapshotArtifact.Clear(state.Artifacts);

                state.ModelGatePassed = false;
                state.Artifacts.EventModelsGateJustCleared = false;

                break;

        }

    }



    private static void ApplySingleToolOutcome(

        CampaignWorkflowState state,

        string toolName,

        string json,

        bool dataWarehouseEnabled,

        bool eventModelsGateWasClosedAtTurnStart)

    {

        if (IsEventModelsSaveModelFailure(state, toolName, json))

        {

            state.Artifacts.EventModelSaveFailureCount++;

            return;

        }



        if (IsGetAccountTool(toolName))
            VerificationCoach.ApplyGetAccountOutcome(state, json, CampaignWorkflowToolSuccess.LooksSuccessful(json));

        if (VerificationDeliveryGuard.IsVerificationTool(toolName))
            state.Artifacts.VerificationToolsInvokedThisSegment = true;

        if (IsValidateCampaignTool(toolName))
            CampaignValidationCoach.ApplyValidateOutcome(state, toolName, json, CampaignWorkflowToolSuccess.LooksSuccessful(json));

        if (IsExampleCampaignDiscoveryTool(toolName))
            JourneyCoach.ApplyExampleDiscoveryTool(state, toolName);

        if (!CampaignWorkflowToolSuccess.LooksSuccessful(json))
        {
            TryCaptureDeferredMutatorRetry(state, toolName, json, eventModelsGateWasClosedAtTurnStart);
            if (IsUpsertCampaignTool(toolName) || IsValidateCampaignTool(toolName))
            {
                if (McpHostFailureParser.TryParse(toolName, json, out _))
                {
                    JourneyCoach.ApplyInvocationFailure(state, toolName);
                    CampaignValidationCoach.RecordFailureCycle(state, toolName, json);
                }
                else if (IsUpsertCampaignTool(toolName))
                    CampaignValidationCoach.ApplyUpsertFailure(state, json);
                else if (IsValidateCampaignTool(toolName))
                    CampaignValidationCoach.RecordFailureCycle(state, toolName, json);
            }
            else if (IsMutatingTool(toolName) || IsGetAccountTool(toolName))
                state.Artifacts.LastToolRemediationSummary = TruncateJson(json, 500);
            return;
        }

        if (IsCampaignMutatorTool(toolName) && CampaignWorkflowToolSuccess.LooksSuccessful(json))
        {
            state.Artifacts.DeferredMutatorRetry = false;
            state.Artifacts.PatHttpDeferralShown = false;
            if (IsUpsertPatTool(toolName))
                state.Artifacts.PatUpsertPending = false;
        }

        if (ModelCatalogToolResultParser.IsCatalogTool(toolName))
            EventModelCandidatesCatalogFallback.TryApplyFromCatalogToolResult(state, json);

        if (CampaignWorkflowPhaseNames.IsDataWarehouseTool(toolName))

        {

            state.Artifacts.DataAnalysisWarehouseSucceeded = true;

            CampaignWorkflowArtifactBuilder.AppendWarehouseInsight(state, toolName, json);

        }



        if (!dataWarehouseEnabled

            && !CampaignWorkflowChecklist.IsBriefCaptured(state)

            && CampaignWorkflowPhaseNames.IsObjectiveProposalTool(toolName))

        {

            var campaignClass = CampaignWorkflowArtifactBuilder.BuildDesignBriefFromObjective(state, json);

            state.Artifacts.DataAnalysisObjectiveCaptured = true;
            state.Artifacts.CampaignDesignBriefApproved = state.Artifacts.CampaignDesignBriefProposed;
            state.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.None;

            if (string.Equals(campaignClass, "tag-first", StringComparison.OrdinalIgnoreCase))

            {

                state.CampaignKind = CampaignWorkflowKind.TagFirst;

                state.UserSkippedEventModels = true;

                state.ModelGatePassed = true;

            }

        }



        if (state.CampaignKind == CampaignWorkflowKind.EventDriven

            && !state.UserSkippedEventModels

            && ModelResolutionToolNames.Contains(toolName))

        {

            if (IsSaveModelTool(toolName))

                state.Artifacts.EventModelSaveFailureCount = 0;



            var merged = EventModelContractsAccumulator.TryMergeFromToolResult(state, toolName, json);

            if (merged
                && (string.Equals(toolName, "GetModel", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(toolName, "get_model", StringComparison.OrdinalIgnoreCase))
                && CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(state))
            {
                TryApplyWrongModelRemediation(state);
            }

            if (IsSaveModelTool(toolName))
            {
                if (EventModelSaveGuard.LooksLikePatFabricationFromJson(json))
                {
                    state.Artifacts.LastToolRemediationSummary =
                        EventModelSaveGuard.GetPatFabricationRemediation();
                }
                else if (CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(state)
                         && !merged)
                {
                    var bypass = EventModelSaveGuard.GetBypassRemediation(json);
                    if (!string.IsNullOrWhiteSpace(bypass))
                        state.Artifacts.LastToolRemediationSummary = bypass;
                }
            }
        }



        if (IsUpsertCampaignTool(toolName))
        {
            MaybeApplyAntiRebuildRemediation(state, toolName);
            CreationArtifactBridge.TryApplyUpsertOutcome(state, json);
            JourneyCoach.ApplyUpsertOutcome(state, json, CampaignWorkflowToolSuccess.LooksSuccessful(json));
            MaybeApplyLivePromotionSuccess(state);
        }



        if (IsUpsertPatTool(toolName))
        {
            MaybeApplyAntiRebuildRemediation(state, toolName);
            CampaignWorkflowArtifactBuilder.AppendPointAccountType(state, json);
            CreationSnapshotArtifact.MergePatFromUpsertResult(state, json);
            JourneyPatternPrepResolver.TryPrepare(state);
            JourneyContractSummaryPinArtifacts.RefreshEpisodeState(state);
        }
        else if (IsContractSummaryTool(toolName))
        {
            JourneyContractSummaryPinArtifacts.OnContractSummaryFetched(state, json);
        }
        else if (VerificationCoach.IsRedundantDiscoveryTool(toolName))
        {
            MaybeApplyAntiRediscoveryRemediation(state, toolName, json);
        }
        else if (VerificationCoach.IsRedundantPostVerifyTool(toolName))
        {
            MaybeApplyPostVerifyRedundancyRemediation(state, toolName, json);
        }

        if (string.Equals(toolName, "GetPointAccountType", StringComparison.OrdinalIgnoreCase)
            || string.Equals(toolName, "get_point_account_type", StringComparison.OrdinalIgnoreCase))
        {
            CampaignWorkflowArtifactBuilder.AppendPointAccountType(state, json);
        }

        if (string.Equals(toolName, "ListPointAccountTypes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(toolName, "list_point_account_types", StringComparison.OrdinalIgnoreCase))
        {
            PointAccountManifestBuilder.AppendFromListResult(state, json);
        }



        if (string.Equals(toolName, "ProcessEvent", StringComparison.OrdinalIgnoreCase)

            || string.Equals(toolName, "process_event", StringComparison.OrdinalIgnoreCase))

        {

            state.Artifacts.VerificationRecord = VerificationRecordManifestBridge.MergeIntoProcessEventResult(state, json);
            CaptureProcessEventViolationCode(state, json);
            NavigationCoach.ApplyProcessEventOutcome(state, json, CampaignWorkflowToolSuccess.LooksSuccessful(json));

        }

        if (string.Equals(toolName, "preview_tier_move", StringComparison.OrdinalIgnoreCase))
        {
            CaptureProcessEventViolationCode(state, json);
        }



        if (string.Equals(toolName, "GetCampaignAssistantContext", StringComparison.OrdinalIgnoreCase)

            || string.Equals(toolName, "get_campaign_assistant_context", StringComparison.OrdinalIgnoreCase))

        {
            JourneyCoach.ApplyAssistantContextOutcome(state, json, CampaignWorkflowToolSuccess.LooksSuccessful(json));
            if (string.IsNullOrEmpty(state.Artifacts.VerificationRecord))
                state.Artifacts.VerificationRecord = json;
        }

    }



    private static bool IsEventModelsSaveModelFailure(CampaignWorkflowState state, string toolName, string json) =>

        state.CampaignKind == CampaignWorkflowKind.EventDriven

        && !state.UserSkippedEventModels

        && IsSaveModelTool(toolName)

        && !CampaignWorkflowToolSuccess.LooksSuccessful(json);



    private static bool IsSaveModelTool(string toolName) =>

        string.Equals(toolName, "save_model", StringComparison.OrdinalIgnoreCase)

        || string.Equals(toolName, "SaveModel", StringComparison.OrdinalIgnoreCase);

    private static bool IsGetAccountTool(string toolName) =>
        string.Equals(toolName, "GetAccount", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "get_account", StringComparison.OrdinalIgnoreCase);



    private static void TryCompleteDataAnalysisPhase(CampaignWorkflowState state)

    {

        if (state.Phase != CampaignWorkflowPhase.DataAnalysis)

            return;

        if (!state.Artifacts.DataAnalysisWarehouseSucceeded && !state.Artifacts.DataAnalysisObjectiveCaptured)

            return;

        if (string.IsNullOrEmpty(state.Artifacts.CampaignDesignBriefProposed))

            state.Artifacts.CampaignDesignBriefProposed =

                CampaignWorkflowArtifactBuilder.BuildDesignBrief(state);

        state.Artifacts.CampaignDesignBriefApproved = state.Artifacts.CampaignDesignBriefProposed;
        state.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.None;

    }

    private static bool CanBuildBriefFromCurrentState(CampaignWorkflowState state, bool dataWarehouseEnabled) =>
        !CampaignWorkflowChecklist.IsBriefCaptured(state)
        && (!dataWarehouseEnabled || state.Artifacts.DataAnalysisWarehouseSucceeded || state.Artifacts.DataAnalysisObjectiveCaptured);

    private static void TryCaptureDeferredMutatorRetry(
        CampaignWorkflowState state,
        string toolName,
        string json,
        bool eventModelsGateWasClosedAtTurnStart)
    {
        if (!IsCampaignMutatorTool(toolName))
            return;
        if (!json.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return;

        var gateBlocks = CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(state);
        var shouldCapture = eventModelsGateWasClosedAtTurnStart
                            || gateBlocks
                            || ShouldCapturePostOpenMutatorFailure(state, toolName);

        if (!shouldCapture)
            return;

        state.Artifacts.DeferredMutatorRetry = true;
        if (MutatorSurfaceDeferralPatterns.LooksLikeDeferral(state.Artifacts.LastToolRemediationSummary)
            || MutatorSurfaceDeferralPatterns.LooksLikeDeferral(json))
        {
            state.Artifacts.PatHttpDeferralShown = true;
        }
    }

    private static bool ShouldCapturePostOpenMutatorFailure(CampaignWorkflowState state, string toolName)
    {
        if (CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(state))
            return false;

        if (IsUpsertPatTool(toolName))
            return PointAccountManifestBuilder.Parse(state.Artifacts.PointAccountManifest).Items.Count == 0;

        if (IsUpsertCampaignTool(toolName))
            return !CampaignWorkflowChecklist.IsItemComplete(state, CampaignWorkflowPhase.CampaignSetup);

        return false;
    }

    private static void MaybeApplyAntiRebuildRemediation(CampaignWorkflowState state, string toolName)
    {
        if (!IsCampaignMutatorTool(toolName))
            return;
        if (!CreationSnapshotArtifact.IsCreationComplete(state))
            return;
        if (state.Phase != CampaignWorkflowPhase.Verification
            && !state.Artifacts.VerificationUserTestIntentThisTurn)
            return;

        state.Artifacts.LastToolRemediationSummary = VerificationCoach.BuildAntiRebuildRemediation(state);
    }

    private static void MaybeApplyAntiRediscoveryRemediation(
        CampaignWorkflowState state,
        string toolName,
        string json)
    {
        if (!VerificationCoach.ShouldWarnRedundantDiscovery(state))
            return;
        if (VerificationDebugContext.ShouldSuppressAntiRediscovery(state))
            return;
        if (!CampaignWorkflowToolSuccess.LooksSuccessful(json))
            return;

        state.Artifacts.LastToolRemediationSummary = VerificationCoach.BuildAntiRediscoveryRemediation(state);
    }

    private static void MaybeApplyPostVerifyRedundancyRemediation(
        CampaignWorkflowState state,
        string toolName,
        string json)
    {
        if (!VerificationCoach.ShouldWarnPostVerifyRedundancy(state))
            return;
        if (!CampaignWorkflowToolSuccess.LooksSuccessful(json))
            return;

        state.Artifacts.LastToolRemediationSummary = VerificationCoach.BuildPostVerifySuccessRemediation(state);
    }

    private static void MaybeApplyLivePromotionSuccess(CampaignWorkflowState state)
    {
        var snap = CreationSnapshotArtifact.Read(state);
        if (snap == null || !string.Equals(snap.CampaignStatus, CampaignStatusStrings.Live, StringComparison.OrdinalIgnoreCase))
            return;

        state.Artifacts.PostJourneyVerifyHandoffShown = false;
        state.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.None;

        var summary = state.Artifacts.LastToolRemediationSummary;
        if (!string.IsNullOrWhiteSpace(summary)
            && (summary.Contains("draft verification", StringComparison.OrdinalIgnoreCase)
                || summary.Contains("Do not promote", StringComparison.OrdinalIgnoreCase)))
        {
            state.Artifacts.LastToolRemediationSummary = null;
        }
    }

    private static void TrySetPatUpsertPendingOnGateOpened(CampaignWorkflowState state)
    {
        if (!CampaignWorkflowChecklist.IsBriefCaptured(state))
            return;
        if (CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(state))
            return;
        if (PointAccountManifestBuilder.Parse(state.Artifacts.PointAccountManifest).Items.Count > 0)
            return;

        state.Artifacts.PatUpsertPending = true;
    }

    private static void TryApplyWrongModelRemediation(CampaignWorkflowState state)
    {
        if (EventModelContractsAccumulator.ReadDiscovered(state).Count == 0)
            return;

        var hint = EventModelResolutionCoach.TryGetHint(state, ["no_resolved_event_model"]);
        if (string.IsNullOrWhiteSpace(hint)
            || !hint.Contains("but campaign needs", StringComparison.Ordinal))
        {
            return;
        }

        state.Artifacts.LastToolRemediationSummary = hint.StartsWith("Coach: ", StringComparison.Ordinal)
            ? hint["Coach: ".Length..]
            : hint;
    }

    private static void CaptureProcessEventViolationCode(CampaignWorkflowState state, string json)
    {
        var code = ValidateLoopCoach.MapViolationCode(json);
        if (!string.IsNullOrWhiteSpace(code))
            state.Artifacts.LastValidateViolationCode = code;
        else if (!CampaignWorkflowToolSuccess.LooksSuccessful(json))
            state.Artifacts.LastValidateViolationCode = ValidateLoopCoach.MapViolationCode(json);
    }

    private static bool LooksLikePhaseBlockedMutatorSummary(string? summary) =>
        !string.IsNullOrWhiteSpace(summary)
        && summary.Contains("not found", StringComparison.OrdinalIgnoreCase)
        && (summary.Contains("Workflow hint", StringComparison.OrdinalIgnoreCase)
            || summary.Contains("upsert_campaign", StringComparison.OrdinalIgnoreCase)
            || summary.Contains("upsert_point_account_type", StringComparison.OrdinalIgnoreCase));

    private static bool IsUpsertCampaignTool(string toolName) =>
        string.Equals(toolName, "UpsertCampaign", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "upsert_campaign", StringComparison.OrdinalIgnoreCase);

    private static bool IsUpsertPatTool(string toolName) =>
        string.Equals(toolName, "UpsertPointAccountType", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "upsert_point_account_type", StringComparison.OrdinalIgnoreCase);

    private static bool IsValidateCampaignTool(string toolName) =>
        string.Equals(toolName, "ValidateCampaign", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "validate_campaign", StringComparison.OrdinalIgnoreCase);

    private static bool IsContractSummaryTool(string toolName) =>
        string.Equals(toolName, "GetRulesEngineContractSummary", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "get_rules_engine_contract_summary", StringComparison.OrdinalIgnoreCase);

    private static bool IsCampaignMutatorTool(string toolName) =>
        IsUpsertCampaignTool(toolName) || IsUpsertPatTool(toolName);

    private static bool IsMutatingTool(string toolName) =>
        IsSaveModelTool(toolName)
        || string.Equals(toolName, "UpsertCampaign", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "upsert_campaign", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "UpsertPointAccountType", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "upsert_point_account_type", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "ProcessEvent", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "process_event", StringComparison.OrdinalIgnoreCase);

    private static string TruncateJson(string json, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(json))
            return string.Empty;

        var text = json.Trim();
        return text.Length <= maxChars
            ? text
            : text.Substring(0, maxChars) + "...";
    }



    private static Dictionary<string, bool> BuildProcessEventEligibilityMap(CampaignWorkflowState state)

    {

        var map = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        foreach (var contract in EventModelContractsAccumulator.Read(state))

        {

            if (string.IsNullOrWhiteSpace(contract.EventModelId))

                continue;

            map[contract.EventModelId] = contract.IsProcessEventEligible;

        }



        return map;

    }

    private static bool IsExampleCampaignDiscoveryTool(string toolName) =>
        string.Equals(toolName, "GetExampleCampaign", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "get_example_campaign", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "ListExampleCampaigns", StringComparison.OrdinalIgnoreCase)
        || string.Equals(toolName, "list_example_campaigns", StringComparison.OrdinalIgnoreCase);

}


