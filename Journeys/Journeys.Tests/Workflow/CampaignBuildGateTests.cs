using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.Core.Workflow;
using Xunit;

namespace Journeys.Tests.Workflow;

public class CampaignBuildGateTests
{
    [Fact]
    public void ShouldBlockJourneyPayload_empty_manifest_with_rules()
    {
        var s = GateOpenBriefCaptured();
        var json = """{"name":"x","journey":{"rules":[{"name":"earn"}]}}""";
        Assert.True(CampaignBuildGate.ShouldBlockJourneyPayload(s, json));
    }

    [Fact]
    public void ShouldBlockJourneyPayload_manifest_populated_allows()
    {
        var s = GateOpenBriefCaptured();
        s.Artifacts.PointAccountManifest =
            """{"items":[{"id":"11111111-1111-1111-1111-111111111111","displayLabel":"Spend"}]}""";
        var json = """{"name":"x","journey":{"rules":[{"name":"earn"}]}}""";
        Assert.False(CampaignBuildGate.ShouldBlockJourneyPayload(s, json));
    }

    [Fact]
    public void ShouldBlockJourneyPayload_inline_fake_pat_ids()
    {
        var s = GateOpenBriefCaptured();
        var json = """{"pointAccountManifest":[{"id":"pat-fake"}],"journey":{"rules":[]}}""";
        Assert.True(CampaignBuildGate.ShouldBlockJourneyPayload(s, json));
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
