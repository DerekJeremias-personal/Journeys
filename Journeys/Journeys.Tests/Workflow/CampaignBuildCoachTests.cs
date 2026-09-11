using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Workflow;

public class CampaignBuildCoachTests
{
    [Fact]
    public void TryGetHint_manifest_empty_steers_to_pat()
    {
        var s = GateOpenBriefCaptured();
        var hint = CampaignBuildCoach.TryGetHint(s);
        Assert.NotNull(hint);
        Assert.Contains("upsert_point_account_type", hint!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CampaignBuild", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryGetHint_build_gate_remediation_steers_to_pat()
    {
        var s = GateOpenBriefCaptured();
        s.Artifacts.LastToolRemediationSummary = "BUILD_GATE_PAT_MANIFEST_REQUIRED";
        var hint = CampaignBuildCoach.TryGetHint(s);
        Assert.NotNull(hint);
        Assert.Contains("upsert_point_account_type", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetCoachHint_build_coach_beats_journey_when_manifest_empty()
    {
        var s = GateOpenBriefCaptured();
        s.Phase = CampaignWorkflowPhase.CampaignJourney;
        s.Artifacts.CampaignShellRef = """{"campaignId":"c-1"}""";
        s.Artifacts.LastToolRemediationSummary = "BUILD_GATE_PAT_MANIFEST_REQUIRED";

        var hint = CampaignWorkflowStepManager.GetCoachHint(s);

        Assert.NotNull(hint);
        Assert.Contains("upsert_point_account_type", hint!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("journey.ruleSetCount is 0", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryGetHint_manifest_populated_zero_validate_rules_steers_to_journey()
    {
        var s = GateOpenBriefCaptured();
        s.Artifacts.PointAccountManifest =
            """{"items":[{"id":"11111111-1111-1111-1111-111111111111","displayLabel":"Spend","ledgerType":"Spendable"}]}""";
        s.Artifacts.LastValidateRuleSetCount = 0;
        s.Artifacts.CampaignShellRef = """{"campaignId":"c-1"}""";

        var hint = CampaignBuildCoach.TryGetHint(s);

        Assert.NotNull(hint);
        Assert.Contains("RuleSetCount", hint!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("children[]", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryGetHint_pat_on_surface_steers_to_invoke_now()
    {
        var s = GateOpenBriefCaptured();
        var hint = CampaignBuildCoach.TryGetHint(s, ["upsert_point_account_type", "validate_campaign"]);
        Assert.NotNull(hint);
        Assert.Contains("toolsThisTurn", hint!, StringComparison.OrdinalIgnoreCase);
    }

    private static CampaignWorkflowState GateOpenBriefCaptured()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.CampaignKind = CampaignWorkflowKind.EventDriven;
        state.Artifacts.CampaignDesignBriefProposed = """{"objective":"tier loyalty"}""";
        state.Artifacts.CampaignDesignBriefApproved = state.Artifacts.CampaignDesignBriefProposed;
        state.Artifacts.ResolvedEventModelContracts =
            """[{"schemaVersion":1,"eventModelId":"evt-1","eventModelName":"Order","isProcessEventEligible":true}]""";
        return state;
    }
}
