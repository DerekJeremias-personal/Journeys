using System.Text.Json;
using Journeys.API.CampaignAgent;
using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.DTO.Models;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public class EventModelPrepResolverTests
{
    [Fact]
    public void ApplyStrongMatchModelJson_opens_gate_when_contract_complete()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Phase = CampaignWorkflowPhase.EventModels;
        state.CampaignKind = CampaignWorkflowKind.EventDriven;
        state.Artifacts.CampaignDesignBriefApproved = """{"version":1}""";
        EventModelCandidatesArtifact.Write(state, new EventModelCandidateSet
        {
            RecommendedDefaultId = "order-1",
            Candidates =
            [
                new EventModelCandidate { EventModelId = "order-1", Name = "order", IsStrongMatch = true }
            ]
        });

        var opened = EventModelPrepResolver.ApplyStrongMatchModelJson(state, OrderModelJson("order-1"));

        Assert.True(opened);
        Assert.True(EventModelsReadiness.Evaluate(state).IsReady);
        Assert.False(CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(state));
    }

    [Fact]
    public void ApplyStrongMatchModelJson_returns_false_when_metadata_incomplete()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Phase = CampaignWorkflowPhase.EventModels;
        state.CampaignKind = CampaignWorkflowKind.EventDriven;
        state.Artifacts.CampaignDesignBriefApproved = """{"version":1}""";
        EventModelCandidatesArtifact.Write(state, new EventModelCandidateSet
        {
            RecommendedDefaultId = "order-1",
            Candidates =
            [
                new EventModelCandidate { EventModelId = "order-1", Name = "order", IsStrongMatch = true }
            ]
        });

        var incomplete = """{"id":"order-1","name":"order","tag":"eventable"}""";
        var opened = EventModelPrepResolver.ApplyStrongMatchModelJson(state, incomplete);

        Assert.False(opened);
        Assert.Empty(EventModelContractsAccumulator.ReadResolved(state));
    }

    [Fact]
    public void ShouldAttemptAutoResolve_false_when_not_strong_match()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefApproved = """{"version":1}""";
        EventModelCandidatesArtifact.Write(state, new EventModelCandidateSet
        {
            RecommendedDefaultId = "order-1",
            Candidates =
            [
                new EventModelCandidate { EventModelId = "order-1", Name = "order", IsStrongMatch = false }
            ]
        });

        Assert.False(EventModelPrepResolver.ShouldAttemptAutoResolve(state));
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
        });
}
