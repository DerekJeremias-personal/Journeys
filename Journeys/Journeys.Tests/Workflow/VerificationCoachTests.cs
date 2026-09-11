using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Xunit;

namespace Journeys.Tests.Workflow;

public class VerificationCoachTests
{
    [Theory]
    [InlineData("Please test with test_exp_01", "test_exp_01")]
    [InlineData("Awesome! test with a sample payload, you can use the test_001 user", "test_001")]
    [InlineData("verify with test_exp_01", "test_exp_01")]
    public void TryExtractTestAccountId_finds_account_token(string message, string expected)
    {
        Assert.Equal(expected, VerificationCoach.TryExtractTestAccountId(message));
    }

    [Fact]
    public void TryCaptureTestAccountFromUserMessage_sets_artifact_and_resets_confirmed()
    {
        var s = InVerification();
        s.Artifacts.VerificationTestAccountId = "old_id";
        s.Artifacts.VerificationAccountConfirmed = true;

        VerificationCoach.TryCaptureTestAccountFromUserMessage(s, "test with test_exp_01");

        Assert.Equal("test_exp_01", s.Artifacts.VerificationTestAccountId);
        Assert.False(s.Artifacts.VerificationAccountConfirmed);
    }

    [Fact]
    public void GetCoachHint_blocked_when_allowlist_empty()
    {
        var s = InVerification();
        s.Artifacts.TenantTestAccountAllowlistJson = "[]";
        s.Artifacts.VerificationBlockedNoAllowlist = true;
        var hint = VerificationCoach.GetCoachHint(s);
        Assert.Contains("cannot complete", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetCoachHint_offers_allowlisted_ids()
    {
        var s = InVerification();
        s.Artifacts.TenantTestAccountAllowlistJson = """["test_exp_01","test_exp_02"]""";
        var hint = VerificationCoach.GetCoachHint(s);
        Assert.Contains("test_exp_01", hint!);
        Assert.Contains("test_exp_02", hint!);
    }

    [Fact]
    public void TryExtractTestAccountId_external_id_phrase()
    {
        Assert.Equal("cust_99", VerificationCoach.TryExtractTestAccountId("use external id cust_99 for the test"));
    }

    [Fact]
    public void GetCoachHint_asks_for_account_when_missing()
    {
        var s = InVerification();
        var hint = VerificationCoach.GetCoachHint(s);
        Assert.NotNull(hint);
        Assert.Contains("Coach:", hint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("test account", hint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetCoachHint_prompts_get_account_when_named_not_confirmed()
    {
        var s = InVerification();
        s.Artifacts.VerificationTestAccountId = "test_exp_01";
        var hint = VerificationCoach.GetCoachHint(s);
        Assert.Contains("get_account", hint!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("test_exp_01", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyGetAccountOutcome_confirms_when_response_matches_test_id()
    {
        var s = InVerification();
        s.Artifacts.VerificationTestAccountId = "test_exp_01";
        var json = """{"id":"guid-1","extAccountId":"test_exp_01","tenantId":"primo"}""";

        VerificationCoach.ApplyGetAccountOutcome(s, json, success: true);

        Assert.True(s.Artifacts.VerificationAccountConfirmed);
        Assert.NotNull(s.Artifacts.VerificationAccountDigest);
    }

    [Fact]
    public void GetCoachHint_anti_rebuild_when_creation_complete()
    {
        var s = InVerification();
        s.Artifacts.CreationSnapshot = """
            {
              "schemaVersion": 1,
              "creationComplete": true,
              "campaignId": "camp-1",
              "journeyRuleSetCount": 2,
              "pointAccountTypes": [{ "id": "pat-1" }]
            }
            """;
        s.Artifacts.VerificationTestAccountId = "test_exp_01";

        var hint = VerificationCoach.GetCoachHint(s);

        Assert.NotNull(hint);
        Assert.Contains("Do not call upsert_point_account_type", hint!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("get_account", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetCoachHint_surfaces_allowlist_error()
    {
        var s = InVerification();
        s.Artifacts.VerificationTestAccountId = "test_exp_01";
        s.Artifacts.VerificationAccountConfirmed = true;
        s.Artifacts.LastToolRemediationSummary = """{"draftTestingNotPermitted":"Account not on tenant campaign test allowlist."}""";

        var hint = VerificationCoach.GetCoachHint(s);

        Assert.NotNull(hint);
        Assert.Contains("allowlist", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetCoachHint_includes_campaignId_when_account_confirmed()
    {
        var s = InVerification();
        s.Artifacts.VerificationTestAccountId = "test_exp_01";
        s.Artifacts.VerificationAccountConfirmed = true;
        s.Artifacts.CreationSnapshot = """
            {
              "schemaVersion": 1,
              "creationComplete": true,
              "campaignId": "camp-draft-1",
              "journeyRuleSetCount": 1,
              "pointAccountTypes": [{ "id": "pat-1" }]
            }
            """;

        var hint = VerificationCoach.GetCoachHint(s);

        Assert.NotNull(hint);
        Assert.Contains("campaignId=camp-draft-1", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetCoachHint_no_anti_rebuild_when_incomplete()
    {
        var s = InVerification();
        s.Artifacts.VerificationTestAccountId = "test_exp_01";

        var hint = VerificationCoach.GetCoachHint(s);

        Assert.NotNull(hint);
        Assert.DoesNotContain("CreationSnapshot", hint!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("get_account", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildAntiRediscoveryRemediation_mentions_discovery_tools()
    {
        var s = InVerification();
        s.Artifacts.CreationSnapshot = """
            {
              "schemaVersion": 1,
              "creationComplete": true,
              "campaignId": "camp-1",
              "journeyRuleSetCount": 2,
              "pointAccountTypes": [{ "id": "pat-1" }]
            }
            """;

        var text = VerificationCoach.BuildAntiRediscoveryRemediation(s);

        Assert.Contains("get_model_attributes_for_rules", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("resolvedEventModelIds", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetCoachHint_anti_rediscovery_when_creation_complete_and_confirmed()
    {
        var s = InVerification();
        s.Artifacts.CreationSnapshot = """
            {
              "schemaVersion": 1,
              "creationComplete": true,
              "campaignId": "camp-1",
              "journeyRuleSetCount": 2,
              "pointAccountTypes": [{ "id": "pat-1" }]
            }
            """;
        s.Artifacts.VerificationTestAccountId = "test_exp_01";
        s.Artifacts.VerificationAccountConfirmed = true;

        var hint = VerificationCoach.GetCoachHint(s);

        Assert.NotNull(hint);
        Assert.Contains("get_model_attributes_for_rules", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IsRedundantDiscoveryTool_matches_model_attribute_tool()
    {
        Assert.True(VerificationCoach.IsRedundantDiscoveryTool("get_model_attributes_for_rules"));
        Assert.True(VerificationCoach.IsRedundantDiscoveryTool("get_model"));
        Assert.False(VerificationCoach.IsRedundantDiscoveryTool("process_event"));
    }

    [Fact]
    public void GetCoachHint_returns_verify_playbook_when_active_debug()
    {
        var s = InVerification();
        s.Artifacts.CreationSnapshot = """
            {
              "schemaVersion": 1,
              "creationComplete": true,
              "campaignId": "camp-1",
              "journeyRuleSetCount": 2,
              "pointAccountTypes": [{ "id": "pat-1" }]
            }
            """;
        s.Artifacts.VerificationRecord = """{"isError":true,"errors":["rule path mismatch"]}""";

        var hint = VerificationCoach.GetCoachHint(s);

        Assert.NotNull(hint);
        Assert.Contains("upsert_campaign", hint!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sampleScaffold", hint!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not save_model", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShouldWarnRedundantDiscovery_suppressed_when_active_debug()
    {
        var s = InVerification();
        s.Artifacts.CreationSnapshot = """
            {
              "schemaVersion": 1,
              "creationComplete": true,
              "campaignId": "camp-1",
              "journeyRuleSetCount": 2,
              "pointAccountTypes": [{ "id": "pat-1" }]
            }
            """;
        s.Artifacts.VerificationRecord = """{"isError":true,"errors":["rule path mismatch"]}""";

        Assert.False(VerificationCoach.ShouldWarnRedundantDiscovery(s));
    }

    [Fact]
    public void GetCoachHint_post_verify_success_stops_redundant_discovery()
    {
        var s = InVerification();
        s.Artifacts.CreationSnapshot = """
            {
              "schemaVersion": 1,
              "creationComplete": true,
              "campaignId": "camp-1",
              "journeyRuleSetCount": 2,
              "pointAccountTypes": [{ "id": "pat-1" }]
            }
            """;
        s.Artifacts.VerificationRecord = """
            {"AppliedRuleSetIds":["rs1"],"OutcomeStates":[{"PointsDeposited":3}]}
            """;

        var hint = VerificationCoach.GetCoachHint(s);

        Assert.NotNull(hint);
        Assert.Contains("points were deposited", hint!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("list_example_campaigns", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IsRedundantPostVerifyTool_matches_example_campaign_reads()
    {
        Assert.True(VerificationCoach.IsRedundantPostVerifyTool("list_example_campaigns"));
        Assert.True(VerificationCoach.IsRedundantPostVerifyTool("get_rules_engine_contract_summary"));
        Assert.False(VerificationCoach.IsRedundantPostVerifyTool("process_event"));
    }

    [Fact]
    public void BuildPostVerifySuccessRemediation_mentions_campaign_id()
    {
        var s = InVerification();
        s.Artifacts.CreationSnapshot = """
            {"schemaVersion":1,"creationComplete":true,"campaignId":"ee9aaad5","journeyRuleSetCount":3}
            """;
        s.Artifacts.VerificationRecord = """
            {"AppliedRuleSetIds":["rs1"],"OutcomeStates":[{"PointsDeposited":3}]}
            """;

        var text = VerificationCoach.BuildPostVerifySuccessRemediation(s);

        Assert.Contains("ee9aaad5", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildPostVerifySuccessRemediation_multi_tier_hint_when_three_rule_sets()
    {
        var s = InVerification();
        s.Artifacts.CreationSnapshot = """
            {"schemaVersion":1,"creationComplete":true,"campaignId":"c1","journeyRuleSetCount":3}
            """;

        var text = VerificationCoach.BuildPostVerifySuccessRemediation(s);

        Assert.Contains("Silver/Gold", text, StringComparison.OrdinalIgnoreCase);
    }

    private static CampaignWorkflowState InVerification()
    {
        var s = CampaignWorkflowState.CreateDefault("primo", "u", "c");
        s.Phase = CampaignWorkflowPhase.Verification;
        s.CampaignKind = CampaignWorkflowKind.EventDriven;
        s.Artifacts.CampaignDesignBriefProposed = """{"objective":"x"}""";
        s.Artifacts.CampaignDesignBriefApproved = s.Artifacts.CampaignDesignBriefProposed;
        return s;
    }
}
