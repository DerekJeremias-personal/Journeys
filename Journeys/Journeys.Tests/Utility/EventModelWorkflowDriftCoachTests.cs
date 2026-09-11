using System.Text.Json;
using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Utility;

public class EventModelWorkflowDriftCoachTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void Drift_hint_when_discovered_eligible_but_not_resolved()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefProposed = """{"version":1}""";
        state.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.EventModelSelection;
        EventModelContractsAccumulator.TryMergeFromToolResult(state, "get_model", OrderModelJson("order-1"));

        var hint = EventModelWorkflowDriftCoach.TryGetHint(state);

        Assert.NotNull(hint);
        Assert.Contains("host gate still closed", hint!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("order", hint!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("discovered", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Drift_hint_absent_when_resolved_event_present()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefProposed = """{"version":1}""";
        EventModelContractsAccumulator.TryMergeFromToolResult(state, "get_model", OrderModelJson("order-1"));

        Assert.Null(EventModelWorkflowDriftCoach.TryGetHint(state));
    }

    [Fact]
    public void GetCoachHint_prefers_drift_over_generic_remediation()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefProposed = """{"version":1}""";
        state.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.EventModelSelection;
        state.Artifacts.LastToolRemediationSummary =
            "Error: Requested function \"upsert_campaign\" not found.";
        EventModelContractsAccumulator.TryMergeFromToolResult(state, "get_model", OrderModelJson("order-1"));

        var hint = CampaignWorkflowStepManager.GetCoachHint(state);

        Assert.NotNull(hint);
        Assert.Contains("host gate still closed", hint!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Last remediation", hint!);
    }

    private static string OrderModelJson(string id) =>
        JsonSerializer.Serialize(new
        {
            id,
            name = "order",
            tag = "eventable",
            modelType = "loyalty",
            modelMetaData = new Dictionary<string, string>
            {
                ["Wrapper"] = "f00df00d-dead-f00d-f00d-ea7f00d1337e",
                ["NaturalKeySymbols"] = "[\"orderid\"]",
                ["AccountXIdSymbol"] = "profileid",
                ["TimeOfOccurrence"] = "timestamp"
            },
            attributes = new[] { new { symbol = "orderid", type = "Primitive", dataType = "string" } }
        }, JsonOpts);
}
