namespace CampaignContextAudit.Analysis;

public sealed record WorkflowSnapshot(
    bool? CreationComplete,
    int? JourneyRuleSetCount,
    string? WorkflowPhase,
    int PatManifestCount,
    int VerificationPatCount,
    bool? UpsertFailedSinceValidate,
    string? LastRemediationPreview,
    bool? UserRequestedNewEventModel,
    bool? FetchFailed);
