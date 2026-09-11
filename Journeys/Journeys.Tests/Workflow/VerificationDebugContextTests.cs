using Journeys.Core.Models;
using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Workflow;

public class VerificationDebugContextTests
{
    private static CampaignWorkflowState StateWithFailedProcessEvent()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.CampaignKind = CampaignWorkflowKind.EventDriven;
        s.Phase = CampaignWorkflowPhase.Verification;
        s.Artifacts.CreationSnapshot = """
            {"schemaVersion":1,"creationComplete":true,"campaignId":"c1","journeyRuleSetCount":3}
            """;
        s.Artifacts.VerificationRecord = """{"isError":true,"errors":["rule mismatch"]}""";
        return s;
    }

    [Fact]
    public void ShouldSuppressAntiRediscovery_when_process_event_failed()
    {
        var s = StateWithFailedProcessEvent();
        Assert.True(VerificationDebugContext.ShouldSuppressAntiRediscovery(s, null));
    }

    [Fact]
    public void ShouldSuppressAntiRediscovery_when_user_steers_rule_path()
    {
        var s = StateWithFailedProcessEvent();
        Assert.True(VerificationDebugContext.ShouldSuppressAntiRediscovery(
            s, "use the item level Price value, not qty"));
    }

    [Fact]
    public void ShouldNotSuppress_when_idle_and_no_failures()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Phase = CampaignWorkflowPhase.Verification;
        s.Artifacts.CreationSnapshot = """
            {"schemaVersion":1,"creationComplete":true,"campaignId":"c1","journeyRuleSetCount":3}
            """;
        s.Artifacts.VerificationRecord = """{"appliedRuleSetIds":["r1"]}""";
        Assert.False(VerificationDebugContext.ShouldSuppressAntiRediscovery(s, null));
    }

    [Fact]
    public void LooksLikeCatalogReloadIntent_detects_reload_phrase()
    {
        Assert.True(VerificationDebugContext.LooksLikeCatalogReloadIntent("please reload the model"));
    }

    [Fact]
    public void ShouldNotSuppress_when_catalog_reload_intent()
    {
        var s = StateWithFailedProcessEvent();
        Assert.False(VerificationDebugContext.ShouldSuppressAntiRediscovery(s, "please reload the model"));
    }

    [Fact]
    public void TryCaptureSteering_sets_ephemeral_flag()
    {
        var s = StateWithFailedProcessEvent();
        VerificationDebugContext.TryCaptureSteeringFromUserMessage(s, "use Price not qty");
        Assert.True(s.Artifacts.VerificationDebugSteeringThisTurn);
    }

    [Fact]
    public void IsActiveDebug_when_rules_fire_but_zero_points()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Phase = CampaignWorkflowPhase.Verification;
        s.Artifacts.CreationSnapshot = """
            {"schemaVersion":1,"creationComplete":true,"campaignId":"c1","journeyRuleSetCount":3}
            """;
        s.Artifacts.VerificationRecord = """
            {"AppliedRuleSetIds":["rs1"],"OutcomeStates":[{"PointsDeposited":0}]}
            """;
        s.Artifacts.VerificationProcessEventRulesApplied = true;

        Assert.True(VerificationDebugContext.IsActiveDebug(s));
    }

    [Fact]
    public void HasSuccessfulPointDeposit_when_outcome_states_have_points()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.VerificationRecord = """
            {"AppliedRuleSetIds":["rs1"],"OutcomeStates":[{"PointsDeposited":3}]}
            """;

        Assert.True(VerificationDebugContext.HasSuccessfulPointDeposit(s));
    }

    [Fact]
    public void BuildVerifyPlaybookHint_includes_deposit_outcome_guidance()
    {
        var s = StateWithFailedProcessEvent();
        var hint = VerificationDebugContext.BuildVerifyPlaybookHint(s);

        Assert.Contains("PointsPerDollar", hint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("EarnRateMultiplier", hint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("discounts: []", hint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("latest upsert_campaign digest", hint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildDepositPointsOutcomeSection_zero_points_adds_triage()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.VerificationRecord = """
            {"AppliedRuleSetIds":["rs1"],"OutcomeStates":[{"PointsDeposited":0}]}
            """;

        var section = VerificationDebugContext.BuildDepositPointsOutcomeSection(s);

        Assert.Contains("0 points", section, StringComparison.OrdinalIgnoreCase);
    }

    private static CampaignWorkflowState JourneyRequiredState()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.CampaignKind = CampaignWorkflowKind.EventDriven;
        s.Artifacts.CampaignDesignBriefApproved = "tier program";
        s.UserSkippedEventModels = true;
        s.ModelGatePassed = true;
        s.Artifacts.PointAccountManifest =
            """{"schemaVersion":1,"items":[{"id":"pat-1","displayLabel":"Spend","ledgerType":"Spendable"}]}""";
        s.Artifacts.CreationSnapshot = """
            {"schemaVersion":1,"creationComplete":false,"campaignId":"c1","journeyRuleSetCount":0}
            """;
        return s;
    }

    [Fact]
    public void IsEarlyVerifyAttempt_true_when_journeyRequired_and_shape_error()
    {
        var s = JourneyRequiredState();
        s.Artifacts.VerificationRecord = """{"errors":{"x":"Unsupported type"}}""";
        Assert.True(VerificationDebugContext.IsEarlyVerifyAttempt(s));
    }

    [Fact]
    public void IsActiveDebug_true_before_creation_complete_when_early_verify()
    {
        var s = JourneyRequiredState();
        s.Artifacts.VerificationRecord = """{"errors":{"x":"Unsupported type"}}""";
        Assert.True(VerificationDebugContext.IsActiveDebug(s));
    }

    [Fact]
    public void ShouldSuppressAntiRediscovery_false_when_not_creation_complete()
    {
        var s = JourneyRequiredState();
        s.Artifacts.VerificationRecord = """{"errors":{"x":"Unsupported type"}}""";
        Assert.False(VerificationDebugContext.ShouldSuppressAntiRediscovery(s));
    }
}
