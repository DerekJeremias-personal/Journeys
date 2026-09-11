using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Xunit;

namespace Journeys.Tests.Workflow;

public class CampaignValidationCoachTests
{
    private const string CleanAck = """
    {
      "validateAck": true,
      "isValid": true,
      "payloadFingerprint": "abc12345",
      "summary": { "errorCount": 0, "warningCount": 0, "ruleSetCount": 1, "hasJourney": true }
    }
    """;

    private const string WarningsAck = """
    {
      "validateAck": true,
      "isValid": true,
      "summary": { "errorCount": 0, "warningCount": 2, "ruleSetCount": 1, "hasJourney": true },
      "topWarnings": [ "WARN_JOURNEY_RULESET_NO_OUTCOMES" ]
    }
    """;

    private const string InvalidFull = """
    {
      "isValid": false,
      "validation": {
        "errors": [ { "field": "status", "message": "required" } ],
        "warnings": []
      },
      "summary": { "errorCount": 1, "warningCount": 0 }
    }
    """;

    [Fact]
    public void ApplyValidateOutcome_clean_stores_artifact_without_coach_hint()
    {
        var state = InCampaignSetup();
        CampaignValidationCoach.ApplyValidateOutcome(state, "validate_campaign", CleanAck, toolLooksSuccessful: true);

        var artifact = CampaignValidationCoach.Read(state);
        Assert.NotNull(artifact);
        Assert.True(artifact!.IsValid);
        Assert.Equal(0, artifact.WarningCount);
        Assert.Equal("abc12345", artifact.PayloadFingerprint);
        Assert.Null(CampaignValidationCoach.GetCoachHint(state));
    }

    [Fact]
    public void ApplyValidateOutcome_warnings_emits_improve_first_hint()
    {
        var state = InCampaignJourney();
        CampaignValidationCoach.ApplyValidateOutcome(state, "validate_campaign", WarningsAck, toolLooksSuccessful: true);

        var hint = CampaignValidationCoach.GetCoachHint(state);
        Assert.NotNull(hint);
        Assert.Contains("2 warning", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyUpsertFailure_nudges_validate_first()
    {
        var state = InCampaignJourney();
        CampaignValidationCoach.ApplyValidateOutcome(state, "validate_campaign", CleanAck, toolLooksSuccessful: true);

        CampaignValidationCoach.ApplyUpsertFailure(state, """{"errors":{"journey.shape.0":"bad"}}""");

        var hint = CampaignValidationCoach.GetCoachHint(state);
        Assert.NotNull(hint);
        Assert.Contains("validate_campaign", hint!, StringComparison.OrdinalIgnoreCase);

        var artifact = CampaignValidationCoach.Read(state);
        Assert.False(artifact!.IsValid);
        Assert.True(artifact.UpsertFailedSinceValidate);
    }

    [Fact]
    public void GetCoachHint_no_validate_yet_nudges_validate_before_upsert()
    {
        var state = InCampaignSetup();
        var hint = CampaignValidationCoach.GetCoachHint(state);
        Assert.NotNull(hint);
        Assert.Contains("validate_campaign", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyValidateOutcome_invalid_sets_remediation_summary()
    {
        var state = InCampaignSetup();
        CampaignValidationCoach.ApplyValidateOutcome(state, "validate_campaign", InvalidFull, toolLooksSuccessful: true);

        Assert.Contains("hard errors", state.Artifacts.LastToolRemediationSummary!, StringComparison.OrdinalIgnoreCase);
        var hint = CampaignValidationCoach.GetCoachHint(state);
        Assert.Contains("validation.errors", hint!, StringComparison.OrdinalIgnoreCase);
    }

    private static CampaignWorkflowState InCampaignSetup()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Phase = CampaignWorkflowPhase.CampaignSetup;
        s.CampaignKind = CampaignWorkflowKind.EventDriven;
        s.Artifacts.CampaignDesignBriefApproved = """{"objective":"x"}""";
        s.ModelGatePassed = true;
        return s;
    }

    private static CampaignWorkflowState InCampaignJourney()
    {
        var s = InCampaignSetup();
        s.Phase = CampaignWorkflowPhase.CampaignJourney;
        return s;
    }

    private const string InvalidJourney = """
    {
      "validateAck": true,
      "isValid": false,
      "payloadFingerprint": "fp1",
      "validation": {
        "errors": [
          { "field": "journey.rules[0].ruleJsonElement", "message": "bad shape", "code": "TIER_A_SIMPLE_RULE_MISSING_EVALUATOR" }
        ]
      },
      "summary": { "errorCount": 1, "warningCount": 0, "ruleSetCount": 0 }
    }
    """;

    [Fact]
    public void RecordFailureCycle_two_invalid_validate_trips_stall()
    {
        CampaignJourneyDeliveryGuard.Enabled = true;
        var state = InCampaignJourney();
        state.Artifacts.CampaignShellRef = """{"campaignId":"c1"}""";

        CampaignValidationCoach.RecordFailureCycle(state, "validate_campaign", InvalidJourney);
        Assert.Equal(1, state.Artifacts.ValidationStallCycleCount);
        Assert.False(state.Artifacts.ValidationStalled);

        CampaignValidationCoach.RecordFailureCycle(state, "validate_campaign", InvalidJourney);
        Assert.Equal(2, state.Artifacts.ValidationStallCycleCount);
        Assert.True(state.Artifacts.ValidationStalled);
        Assert.NotNull(state.Artifacts.ValidationStallTrippedAtUtc);
    }

    [Fact]
    public void GetStallCoachHint_when_stalled_beats_generic_validation_hint()
    {
        var state = InCampaignJourney();
        state.Artifacts.ValidationStalled = true;
        state.Artifacts.ValidationStallCycleCount = 2;
        var hint = CampaignValidationCoach.GetStallCoachHint(state);
        Assert.NotNull(hint);
        Assert.Contains("paused autonomous retries", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClearValidationStall_resets_counters()
    {
        var state = InCampaignJourney();
        state.Artifacts.ValidationStalled = true;
        state.Artifacts.ValidationStallCycleCount = 2;
        CampaignValidationCoach.ClearValidationStall(state);
        Assert.False(state.Artifacts.ValidationStalled);
        Assert.Equal(0, state.Artifacts.ValidationStallCycleCount);
    }

    private const string LegacyPatAliasErrors = """
    {
      "$type": "text",
      "text": "{\"errors\":{\"journey.validation.3\":\"[violation=TIER_A_OUTCOME_PAT_ALIAS_MISUSED] kind=DepositPointsOutcome field=pointAccountTypeId — use affectedPointAccountTypeIds[].\",\"journey.validation.0\":\"[violation=TIER_A_MISSING_TYPE_DISCRIMINATOR] field=navigation — navigation criteria object requires $type.\"}}"
    }
    """;

    private const string NavigationEntryError = """
    {
      "errors": {
        "journey.navigation.0": "[violation=JOURNEY_NAV_ROOT_ENTRY_REQUIRED] path=journey — root rule sets require journey.navigation.Entry."
      }
    }
    """;

    [Fact]
    public void ApplyValidateOutcome_legacy_errors_extracts_violation_codes_and_pat_remediation()
    {
        var state = InCampaignJourney();
        CampaignValidationCoach.ApplyValidateOutcome(
            state, "validate_campaign", LegacyPatAliasErrors, toolLooksSuccessful: true);

        var artifact = CampaignValidationCoach.Read(state);
        Assert.NotNull(artifact);
        Assert.Contains("TIER_A_OUTCOME_PAT_ALIAS_MISUSED", artifact!.LastErrorCodes);
        Assert.Contains("TIER_A_MISSING_TYPE_DISCRIMINATOR", artifact.LastErrorCodes);
        Assert.Contains("AffectedPointAccountTypeIds", state.Artifacts.LastToolRemediationSummary!, StringComparison.Ordinal);
    }

    [Fact]
    public void GetStallCoachHint_navigation_error_includes_entry_remediation()
    {
        CampaignJourneyDeliveryGuard.Enabled = true;
        var state = InCampaignJourney();
        state.Artifacts.CampaignShellRef = """{"campaignId":"c1"}""";

        CampaignValidationCoach.RecordFailureCycle(state, "validate_campaign", NavigationEntryError);
        CampaignValidationCoach.RecordFailureCycle(state, "validate_campaign", NavigationEntryError);

        var hint = CampaignValidationCoach.GetStallCoachHint(state);
        Assert.NotNull(hint);
        Assert.Contains("JOURNEY_NAV_ROOT_ENTRY_REQUIRED", hint!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("root Entry", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetCoachHint_invalid_navigation_error_surfaces_entry_remediation()
    {
        var state = InCampaignJourney();
        CampaignValidationCoach.ApplyValidateOutcome(
            state, "validate_campaign", NavigationEntryError, toolLooksSuccessful: true);

        var hint = CampaignValidationCoach.GetCoachHint(state);
        Assert.NotNull(hint);
        Assert.Contains("root Entry", hint!, StringComparison.OrdinalIgnoreCase);
    }

    private const string JourneyShapeError = """
    {
      "validateAck": true,
      "isValid": false,
      "validation": {
        "errors": [
          { "code": "JOURNEY_SHAPE_NODES_NOT_CHILDREN", "message": "use children[] not nodes[]" }
        ]
      },
      "summary": { "errorCount": 1, "warningCount": 0, "ruleSetCount": 0, "hasJourney": true }
    }
    """;

    [Fact]
    public void RecordFailureCycle_upsert_after_failed_validate_sets_revalidate_remediation()
    {
        CampaignJourneyDeliveryGuard.Enabled = true;
        var state = InCampaignJourney();
        state.Artifacts.CampaignShellRef = """{"campaignId":"c1"}""";

        CampaignValidationCoach.ApplyValidateOutcome(
            state, "validate_campaign", JourneyShapeError, toolLooksSuccessful: true);

        Assert.True(state.Artifacts.LastValidateFailedBeforeUpsert);

        CampaignValidationCoach.RecordFailureCycle(state, "upsert_campaign", """{"errors":{"shape":"bad"}}""");

        Assert.Equal("REVALIDATE_BEFORE_UPSERT", state.Artifacts.LastValidateViolationCode);
        Assert.Contains("Re-validate before upsert", state.Artifacts.LastToolRemediationSummary!, StringComparison.OrdinalIgnoreCase);
    }
}
