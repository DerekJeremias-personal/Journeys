using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.Core.Workflow;
using Xunit;

namespace Journeys.Tests.Workflow;

public class WorkflowSkillRegistryTests
{
    [Fact]
    public void ResolveActiveSkill_event_driven_before_brief_is_Brief()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        Assert.Equal(CampaignWorkflowSkill.Brief, WorkflowSkillRegistry.ResolveActiveSkill(s));
    }

    [Fact]
    public void ResolveActiveSkill_gate_open_brief_captured_is_CampaignBuild()
    {
        var s = GateOpenBriefCaptured();
        Assert.Equal(CampaignWorkflowSkill.CampaignBuild, WorkflowSkillRegistry.ResolveActiveSkill(s));
    }

    [Fact]
    public void ResolveBuildSubStep_manifest_empty_is_PatRequired()
    {
        var s = GateOpenBriefCaptured();
        Assert.Equal(CampaignBuildSubStep.PatRequired, WorkflowSkillRegistry.ResolveBuildSubStep(s));
    }

    [Fact]
    public void ResolveBuildSubStep_manifest_populated_no_journey_is_JourneyRequired()
    {
        var s = GateOpenBriefCaptured();
        s.Artifacts.PointAccountManifest =
            """{"items":[{"id":"11111111-1111-1111-1111-111111111111","displayLabel":"Spend","ledgerType":"Spendable"}]}""";
        Assert.Equal(CampaignBuildSubStep.JourneyRequired, WorkflowSkillRegistry.ResolveBuildSubStep(s));
    }

    [Fact]
    public void BuildSessionSkillBlock_includes_activeSkill_and_nextStep()
    {
        var s = GateOpenBriefCaptured();
        var block = WorkflowSkillRegistry.BuildSessionSkillBlock(
            s, ["upsert_point_account_type", "validate_campaign"]);
        Assert.Contains("activeSkill: CampaignBuild", block);
        Assert.Contains("buildSubStep: patrequired", block, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("upsert_point_account_type", block);
        Assert.Contains("nextStep:", block);
    }

    [Fact]
    public void BuildSessionSkillBlock_journey_required_includes_blockedUntilJourneyPersisted()
    {
        var s = GateOpenBriefCaptured();
        s.Artifacts.PointAccountManifest =
            """{"items":[{"id":"11111111-1111-1111-1111-111111111111","displayLabel":"Spend","ledgerType":"Spendable"}]}""";
        var block = WorkflowSkillRegistry.BuildSessionSkillBlock(s, ["validate_campaign", "upsert_campaign"]);
        Assert.Contains("buildSubStep: journeyrequired", block, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("blockedUntilJourneyPersisted", block);
        Assert.Contains("children[] not nodes[]", block);
    }

    [Fact]
    public void BuildSessionSkillBlock_includes_ruleEpisode_when_build_active()
    {
        var s = GateOpenBriefCaptured();
        var block = WorkflowSkillRegistry.BuildSessionSkillBlock(
            s, ["upsert_point_account_type", "validate_campaign"]);
        Assert.Contains("ruleEpisode:", block);
        Assert.Contains("patToolOnSurface: yes", block);
    }

    private static CampaignWorkflowState GateOpenBriefCaptured()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.CampaignKind = CampaignWorkflowKind.EventDriven;
        s.Artifacts.CampaignDesignBriefProposed = """{"objective":"tier loyalty"}""";
        s.Artifacts.ResolvedEventModelContracts =
            """[{"schemaVersion":1,"eventModelId":"evt-1","eventModelName":"Order","isProcessEventEligible":true}]""";
        return s;
    }
}
