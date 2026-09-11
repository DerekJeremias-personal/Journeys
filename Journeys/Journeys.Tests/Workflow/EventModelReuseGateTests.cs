using System.Text.Json;
using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.DTO.Models;
using Xunit;

namespace Journeys.Tests.Workflow;

public class EventModelReuseGateTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void Deferred_selection_applies_proceed_to_create_campaign_after_gate_raised()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Phase = CampaignWorkflowPhase.EventModels;
        state.Artifacts.CampaignDesignBriefProposed = """{"version":1}""";
        state.Artifacts.CampaignDesignBriefApproved = state.Artifacts.CampaignDesignBriefProposed;
        state.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.EventModelSelection;

        EventModelCandidatesArtifact.Write(state, new EventModelCandidateSet
        {
            RecommendedDefaultId = "order-1",
            Candidates =
            [
                new EventModelCandidate { EventModelId = "order-1", Name = "order", IsStrongMatch = false }
            ]
        });

        EventModelContractsAccumulator.TryMergeFromToolResult(state, "get_model", OrderModelJson("order-1"));

        Assert.Equal(CampaignWorkflowApprovalKind.EventModelSelection, state.Artifacts.AwaitingApproval);

        Assert.True(CampaignWorkflowStepManager.TryApplyDeferredEventModelSelection(
            state, "ok, proceed to create the campaign"));

        Assert.Equal(CampaignWorkflowApprovalKind.None, state.Artifacts.AwaitingApproval);
        Assert.Equal("order-1", state.Artifacts.SelectedEventModelId);
        Assert.Single(EventModelContractsAccumulator.ReadResolved(state));
        Assert.True(state.ModelGatePassed);
        Assert.False(CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(state));
    }

    [Fact]
    public void Get_model_strong_match_default_auto_promotes_and_clears_selection_gate()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefApproved = """{"version":1}""";
        state.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.EventModelSelection;

        EventModelCandidatesArtifact.Write(state, new EventModelCandidateSet
        {
            RecommendedDefaultId = "order-1",
            Candidates =
            [
                new EventModelCandidate
                {
                    EventModelId = "order-1",
                    Name = "order",
                    IsStrongMatch = true
                }
            ]
        });

        EventModelContractsAccumulator.TryMergeFromToolResult(
            state, "get_model", OrderModelJson("order-1"));

        Assert.Equal(CampaignWorkflowApprovalKind.None, state.Artifacts.AwaitingApproval);
        Assert.Equal("order-1", state.Artifacts.SelectedEventModelId);
        Assert.Single(EventModelContractsAccumulator.ReadResolved(state));
        Assert.False(CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(state));
    }

    [Fact]
    public void Promote_discovered_seeds_wrapper_validation_for_eligible_reuse()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        EventModelContractsAccumulator.TryMergeFromToolResult(state, "get_model", OrderModelJson("order-1"));

        EventModelContractsAccumulator.PromoteDiscoveredToResolved(state, "order-1");

        var validation = WrapperContractArtifact.Read(state);
        Assert.Single(validation);
        Assert.Equal("f00df00d-dead-f00d-f00d-ea7f00d1337e", validation[0].WrapperModelId);
        Assert.Empty(validation[0].Errors);
    }

    [Fact]
    public void Get_model_complete_contract_auto_promotes_and_clears_false_positive_new_event_flag()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefApproved = """{"version":1}""";
        state.Artifacts.UserRequestedNewEventModel = true;

        EventModelContractsAccumulator.TryMergeFromToolResult(state, "get_model", OrderModelJson("order-1"));

        Assert.False(state.Artifacts.UserRequestedNewEventModel);
        Assert.Single(EventModelContractsAccumulator.ReadResolved(state));
        Assert.False(CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(state));
    }

    [Fact]
    public void ApplyUserMessage_make_new_campaign_does_not_set_UserRequestedNewEventModel()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        CampaignWorkflowEngine.ApplyUserMessage(
            state,
            "I want to make a new campaign that has the following three tiers");

        Assert.False(state.Artifacts.UserRequestedNewEventModel);
    }

    [Fact]
    public void ApplyUserMessage_yes_create_it_does_not_set_UserRequestedNewEventModel()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        CampaignWorkflowEngine.ApplyUserMessage(state, "yes, create it");

        Assert.False(state.Artifacts.UserRequestedNewEventModel);
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
