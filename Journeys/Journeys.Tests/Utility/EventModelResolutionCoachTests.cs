using System.Text.Json;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.DTO.Models;
using Xunit;

namespace Journeys.Tests.Utility;

public class EventModelResolutionCoachTests
{
    [Fact]
    public void TryGetHint_wrong_discovered_model_names_expected_id()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.CampaignKind = CampaignWorkflowKind.EventDriven;
        state.Artifacts.CampaignDesignBriefApproved = """{"version":1}""";
        EventModelCandidatesArtifact.Write(state, new EventModelCandidateSet
        {
            RecommendedDefaultId = "order-1",
            Candidates = [new EventModelCandidate { EventModelId = "order-1", Name = "order" }]
        });
        EventModelContractsAccumulator.TryMergeFromToolResult(state, "get_model", LineItemJson("line-1"));

        var hint = EventModelResolutionCoach.TryGetHint(state, ["no_resolved_event_model"]);

        Assert.NotNull(hint);
        Assert.Contains("order-1", hint!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("line-1", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryGetHint_names_recommended_id_when_discovered_empty()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.CampaignKind = CampaignWorkflowKind.EventDriven;
        state.Artifacts.CampaignDesignBriefApproved = """{"version":1}""";
        EventModelCandidatesArtifact.Write(state, new EventModelCandidateSet
        {
            RecommendedDefaultId = "order-1",
            Candidates = [new EventModelCandidate { EventModelId = "order-1", Name = "order" }]
        });

        var hint = EventModelResolutionCoach.TryGetHint(state, ["no_resolved_event_model"]);

        Assert.NotNull(hint);
        Assert.Contains("get_model(order-1)", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryGetHint_names_planned_id_from_brief()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.CampaignKind = CampaignWorkflowKind.EventDriven;
        state.Artifacts.CampaignDesignBriefApproved =
            """{"version":1,"plannedEventModelIds":["a6edbbc5-bf43-4c57-b2f1-e015b9efaf03"]}""";

        var hint = EventModelResolutionCoach.TryGetHint(state, ["no_resolved_event_model"]);

        Assert.NotNull(hint);
        Assert.Contains("a6edbbc5-bf43-4c57-b2f1-e015b9efaf03", hint!, StringComparison.OrdinalIgnoreCase);
    }

    private static string LineItemJson(string id) =>
        JsonSerializer.Serialize(new
        {
            id,
            name = "item",
            tag = "eventable",
            modelType = "loyalty",
            attributes = new[] { new { symbol = "sku", type = "Primitive", dataType = "string" } }
        });
}
