using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.Core.Workflow;
using Xunit;

namespace Journeys.Tests.Workflow;

public class JourneyDeliveryGateTests
{
    [Fact]
    public void Blocks_upsert_when_manifest_full_journey_payload_zero_validate_count()
    {
        var s = PatManifestReady();
        s.Artifacts.LastValidateRuleSetCount = 0;
        var json = """{"journey":{"rules":[{"name":"R","ruleJsonElement":{"Kind":"SimpleRule"}}]}}""";
        Assert.True(JourneyDeliveryGate.ShouldBlockJourneyUpsert(s, json));
    }

    [Fact]
    public void Allows_upsert_when_last_validate_rule_set_count_positive()
    {
        var s = PatManifestReady();
        s.Artifacts.LastValidateRuleSetCount = 2;
        var json = """{"journey":{"rules":[{"name":"R"}]}}""";
        Assert.False(JourneyDeliveryGate.ShouldBlockJourneyUpsert(s, json));
    }

    [Fact]
    public void Allows_shell_only_upsert()
    {
        var s = PatManifestReady();
        s.Artifacts.LastValidateRuleSetCount = 0;
        var json = """{"name":"Shell","status":"Draft","journey":{}}""";
        Assert.False(JourneyDeliveryGate.ShouldBlockJourneyUpsert(s, json));
    }

    [Fact]
    public void Allows_when_creation_complete()
    {
        var s = PatManifestReady();
        s.Artifacts.CreationSnapshot = """
            {"schemaVersion":1,"creationComplete":true,"campaignId":"c1","journeyRuleSetCount":3}
            """;
        var json = """{"journey":{"rules":[{"name":"R"}]}}""";
        Assert.False(JourneyDeliveryGate.ShouldBlockJourneyUpsert(s, json));
    }

    private static CampaignWorkflowState PatManifestReady()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.PointAccountManifest =
            """{"items":[{"id":"11111111-1111-1111-1111-111111111111","displayLabel":"P"}]}""";
        s.Artifacts.CampaignShellRef = """{"campaignId":"c1"}""";
        return s;
    }
}
