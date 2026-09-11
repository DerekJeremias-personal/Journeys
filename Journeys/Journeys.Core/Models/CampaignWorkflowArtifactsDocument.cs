namespace Journeys.Core.Models;

/// <summary>
/// Serialized into <see cref="AgentMessage.Content"/> on workflow orchestration rows.
/// </summary>
public sealed class CampaignWorkflowArtifactsDocument
{
    public int ArtifactVersion { get; set; }

    public CampaignWorkflowApprovalKind AwaitingApproval { get; set; }

    /// <summary>At least one successful data-warehouse MCP tool in the current analysis pass.</summary>
    public bool DataAnalysisWarehouseSucceeded { get; set; }

    /// <summary>Objective captured conversationally (warehouse tools disabled) in the current analysis pass.</summary>
    public bool DataAnalysisObjectiveCaptured { get; set; }

    public string? CampaignDesignBriefProposed { get; set; }

    /// <summary>Set true when brief gate clears this segment; cleared end of orchestrator turn.</summary>
    public bool BriefGateJustCleared { get; set; }

    /// <summary>Set true when EventModels readiness opens this segment; cleared end of orchestrator turn.</summary>
    public bool EventModelsGateJustCleared { get; set; }

    public string? CampaignDesignBriefApproved { get; set; }

    public string? EventModelContract { get; set; }

    /// <summary>JSON array of <see cref="Journeys.DTO.Models.EventProcessingContractDigest"/>.</summary>
    public string? EventModelContracts { get; set; }

    public string? CampaignShellRef { get; set; }

    public string? PointAccountManifest { get; set; }

    public string? JourneyDigestProposed { get; set; }

    public string? JourneyDigestApproved { get; set; }

    public string? VerificationRecord { get; set; }

    /// <summary>External or internal test account id named by the user for verification.</summary>
    public string? VerificationTestAccountId { get; set; }

    /// <summary>True after successful get_account for <see cref="VerificationTestAccountId"/>.</summary>
    public bool VerificationAccountConfirmed { get; set; }

    /// <summary>Truncated get_account JSON for SESSION coaching.</summary>
    public string? VerificationAccountDigest { get; set; }

    /// <summary>JSON of <see cref="Journeys.DTO.Models.EventModelCandidateSet"/> computed on EventModels entry.</summary>
    public string? EventModelCandidates { get; set; }

    /// <summary>Resolved earning intent for the EventModels order-event default.</summary>
    public EarningIntent EarningIntent { get; set; }

    /// <summary>Event model id the user selected to reuse (null when creating a new event model).</summary>
    public string? SelectedEventModelId { get; set; }

    /// <summary>User explicitly asked to create a new event model — skip the reuse selection gate.</summary>
    public bool UserRequestedNewEventModel { get; set; }

    /// <summary>JSON: pending event model user asked to create (deferred through DataAnalysis).</summary>
    public string? PendingEventModelSpec { get; set; }

    /// <summary>Consecutive save_model failures in EventModels (reset on successful save).</summary>
    public int EventModelSaveFailureCount { get; set; }

    /// <summary>JSON array of contracts from successful save_model only.</summary>
    public string? ResolvedEventModelContracts { get; set; }

    /// <summary>JSON array of contracts from get_model / list_models (discovery only).</summary>
    public string? DiscoveredEventModelContracts { get; set; }

    /// <summary>JSON array of <see cref="Utility.WrapperContractValidationEntry"/> for *AndRuleState wrappers.</summary>
    public string? WrapperContractValidation { get; set; }

    /// <summary>Concise remediation from the last failed mutating tool (coach SESSION).</summary>
    public string? LastToolRemediationSummary { get; set; }

    /// <summary>JSON <see cref="Journeys.DTO.Models.CampaignCreationSnapshotDto"/> — compact post-creation facts for SESSION.</summary>
    public string? CreationSnapshot { get; set; }

    /// <summary>Set when upsert_campaign/upsert_point_account_type failed with "not found" while gate was closed; cleared after continuation hop.</summary>
    public bool DeferredMutatorRetry { get; set; }

    /// <summary>Agent deferred PAT creation to HTTP/paste-GUID instructions while mutators were available.</summary>
    public bool PatHttpDeferralShown { get; set; }

    /// <summary>PAT upserts still required after gate opens or narrative stall; persists across turns until first successful upsert_point_account_type.</summary>
    public bool PatUpsertPending { get; set; }

    /// <summary>Resolved rules-engine pattern id for journey authoring (e.g. tier-navigation-point-balance).</summary>
    public string? JourneyPatternId { get; set; }

    /// <summary>PAT-substituted minimalSkeleton JSON pinned for the active journey episode.</summary>
    public string? JourneyPatternSkeletonJson { get; set; }

    /// <summary>Host pinned journey pattern skeleton for this episode.</summary>
    public bool JourneyPatternPrepComplete { get; set; }

    /// <summary>First get_rule_pattern_recipes returned full skeleton for pinned pattern.</summary>
    public bool JourneyPatternRecipesFullResultShown { get; set; }

    /// <summary>First get_example_campaign returned structural excerpt for pinned pattern.</summary>
    public bool JourneyPatternExampleFullResultShown { get; set; }

    /// <summary>Example campaign discovery tool calls while pattern prep is active.</summary>
    public int JourneyExampleCampaignFetchCount { get; set; }

    /// <summary>True when the current user turn expressed verification test intent (cleared end of orchestrator turn).</summary>
    public bool VerificationUserTestIntentThisTurn { get; set; }

    /// <summary>Ephemeral — user steered rule-path / keep-model debug this turn; cleared end of orchestrator turn.</summary>
    public bool VerificationDebugSteeringThisTurn { get; set; }

    /// <summary>Last successful process_event applied the creation campaign id.</summary>
    public bool VerificationProcessEventCampaignApplied { get; set; }

    /// <summary>Last successful process_event applied at least one rule set.</summary>
    public bool VerificationProcessEventRulesApplied { get; set; }

    /// <summary>JSON of <see cref="CampaignValidationArtifact"/> — last validate_campaign outcome.</summary>
    public string? CampaignValidationSummary { get; set; }

    /// <summary>Circuit breaker tripped — host blocks validate/upsert until user re-engages.</summary>
    public bool ValidationStalled { get; set; }

    /// <summary>Failed validate/upsert cycles in the current stall episode.</summary>
    public int ValidationStallCycleCount { get; set; }

    /// <summary>ISO-8601 UTC when ValidationStalled was set.</summary>
    public string? ValidationStallTrippedAtUtc { get; set; }

    /// <summary>Ephemeral — checkpoint card already emitted this turn; cleared at orchestrator turn end.</summary>
    public bool JourneyEntryCheckpointShownThisTurn { get; set; }

    /// <summary>JSON array of tenant campaignTestAccountExtIds snapshot for SESSION (eviction-proof).</summary>
    public string? TenantTestAccountAllowlistJson { get; set; }

    /// <summary>ISO-8601 UTC when allowlist was last loaded from tenant registry.</summary>
    public string? TenantTestAccountsLoadedAtUtc { get; set; }

    /// <summary>True when tenant registry loaded successfully but allowlist is empty — draft verification blocked.</summary>
    public bool VerificationBlockedNoAllowlist { get; set; }

    /// <summary>True when tenant allowlist load failed this session (coach degrades).</summary>
    public bool TenantTestAccountsLoadFailed { get; set; }

    /// <summary>Ephemeral — set when get_account / assistant context / process_event runs this segment; cleared between orchestrator segments.</summary>
    public bool VerificationToolsInvokedThisSegment { get; set; }

    /// <summary>One-shot post-journey handoff coach already shown after first ruleSetCount &gt; 0.</summary>
    public bool PostJourneyVerifyHandoffShown { get; set; }

    /// <summary>User explicitly requested Live promotion this session — clears hard gate.</summary>
    public bool LivePromotionApprovedThisSession { get; set; }

    /// <summary>RuleSetCount from the most recent successful validate_campaign summary.</summary>
    public int? LastValidateRuleSetCount { get; set; }

    public bool? LastValidateIsValid { get; set; }

    public bool? LastValidateHadJourneyIntent { get; set; }

    /// <summary>Journey episode active — protect contract summary from history shrink until rules persist.</summary>
    public bool JourneyContractSummaryPinActive { get; set; }

    /// <summary>Matrix version from the contract summary fetched this journey episode.</summary>
    public string? JourneyContractSummaryMatrixVersion { get; set; }

    /// <summary>Contract summary tool succeeded while journey episode pin is active.</summary>
    public bool JourneyContractSummaryFetchedThisEpisode { get; set; }

    /// <summary>Compact criticalRows ids from contract summary (SESSION pin companion).</summary>
    public string? JourneyContractCriticalRowIds { get; set; }

    /// <summary>Normalized violation code from last validate/upsert/process_event failure (BCP).</summary>
    public string? LastValidateViolationCode { get; set; }

    /// <summary>Human-readable journey scaffold for WORKFLOW ARTIFACTS when JourneyRequired.</summary>
    public string? JourneyScaffold { get; set; }

    /// <summary>Structured journey.children[] template for validate-first authoring.</summary>
    public string? JourneyAuthoringTemplateJson { get; set; }

    /// <summary>Last validate failed and upsert was attempted before a successful re-validate.</summary>
    public bool LastValidateFailedBeforeUpsert { get; set; }

    /// <summary>Compact process_event payload shape hint for verification handoff.</summary>
    public string? EventPayloadScaffold { get; set; }
}
