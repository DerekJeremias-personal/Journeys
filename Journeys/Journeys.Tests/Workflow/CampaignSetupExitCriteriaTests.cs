using System.Text.Json;
using Journeys.API.CampaignAgent.Workflow.Steps;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Workflow;

public class CampaignSetupExitCriteriaTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void Not_met_when_shell_events_not_in_resolved_contracts()
    {
        var s = InCampaignSetup();
        EventModelContractsAccumulator.TryMergeFromToolResult(s, "save_model", ModelJson("r1", "Review"));
        s.Artifacts.CampaignShellRef = """{"schemaVersion":1,"eventModelIds":["wrong-id"]}""";
        Assert.False(new CampaignSetupExitCriteria().IsMet(s));
    }

    [Fact]
    public void Met_when_shell_event_ids_subset_of_resolved_contracts()
    {
        var s = InCampaignSetup();
        EventModelContractsAccumulator.TryMergeFromToolResult(s, "save_model", ModelJson("r1", "Review"));
        s.Artifacts.CampaignShellRef = """{"schemaVersion":1,"eventModelIds":["r1"]}""";
        Assert.True(new CampaignSetupExitCriteria().IsMet(s));
    }

    [Fact]
    public void Met_when_shell_has_no_event_ids()
    {
        var s = InCampaignSetup();
        s.Artifacts.CampaignShellRef = """{"schemaVersion":1,"campaignId":"c1","status":"Draft"}""";
        Assert.True(new CampaignSetupExitCriteria().IsMet(s));
    }

    private static CampaignWorkflowState InCampaignSetup()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Phase = CampaignWorkflowPhase.CampaignSetup;
        s.CampaignKind = CampaignWorkflowKind.EventDriven;
        return s;
    }

    private static string ModelJson(string id, string name) =>
        JsonSerializer.Serialize(new
        {
            id,
            name,
            tag = "eventable",
            modelType = "event",
            modelMetaData = new Dictionary<string, string>
            {
                ["Wrapper"] = "w1",
                ["NaturalKeySymbols"] = "[\"orderid\"]",
                ["AccountXIdSymbol"] = "orderid"
            },
            attributes = new[] { new { symbol = "orderid", type = "Primitive", dataType = "string", displayName = "Id" } }
        }, JsonOpts);
}
