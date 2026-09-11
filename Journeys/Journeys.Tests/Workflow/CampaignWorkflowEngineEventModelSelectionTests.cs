using System.Text.Json;

using Journeys.API.CampaignAgent.Workflow;

using Journeys.Core.Models;

using Journeys.Core.Utility;

using Journeys.DTO.Models;

using Xunit;



namespace Journeys.Tests.Workflow;



public class CampaignWorkflowEngineEventModelSelectionTests

{

    private static readonly JsonSerializerOptions JsonOpts = new()

    {

        PropertyNamingPolicy = JsonNamingPolicy.CamelCase

    };



    private static CampaignWorkflowState GatedState(string? recommendedId = "o1")

    {

        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");

        state.Phase = CampaignWorkflowPhase.EventModels;

        state.CampaignKind = CampaignWorkflowKind.EventDriven;

        state.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.EventModelSelection;

        EventModelCandidatesArtifact.Write(state, new EventModelCandidateSet

        {

            RecommendedDefaultId = recommendedId,

            Candidates =

            {

                new EventModelCandidate { EventModelId = "o1", Name = "Order", IsStrongMatch = false },

                new EventModelCandidate { EventModelId = "e1", Name = "EmailOpened" }

            }

        });

        SeedDiscoveredContracts(state);

        return state;

    }



    private static void SeedDiscoveredContracts(CampaignWorkflowState state)

    {

        EventModelContractsAccumulator.TryMergeFromToolResult(state, "get_model", ModelToolJson("o1", "Order"));

        EventModelContractsAccumulator.TryMergeFromToolResult(state, "get_model", ModelToolJson("e1", "EmailOpened"));

    }



    [Fact]

    public void Confirm_default_clears_gate_records_selection_and_returns_focus_to_data_analysis_until_brief()

    {

        var state = GatedState();

        CampaignWorkflowEngine.ApplyUserMessage(state, "yes, use it");



        Assert.Equal(CampaignWorkflowApprovalKind.None, state.Artifacts.AwaitingApproval);

        Assert.Equal("o1", state.Artifacts.SelectedEventModelId);

        Assert.Equal(CampaignWorkflowPhase.DataAnalysis, state.Phase);

        Assert.True(state.ModelGatePassed);

        Assert.Single(EventModelContractsAccumulator.ReadResolved(state));

    }



    [Fact]

    public void Pick_by_name_clears_gate_and_returns_focus_to_data_analysis_until_brief()

    {

        var state = GatedState();

        CampaignWorkflowEngine.ApplyUserMessage(state, "use EmailOpened");



        Assert.Equal(CampaignWorkflowApprovalKind.None, state.Artifacts.AwaitingApproval);

        Assert.Equal("e1", state.Artifacts.SelectedEventModelId);

        Assert.Equal(CampaignWorkflowPhase.DataAnalysis, state.Phase);

        Assert.Single(EventModelContractsAccumulator.ReadResolved(state));

    }



    [Fact]

    public void Selected_model_returns_data_analysis_focus_and_move_on_without_brief_keeps_events_incomplete()

    {

        var state = GatedState();

        CampaignWorkflowEngine.ApplyUserMessage(state, "use order");



        Assert.Equal(CampaignWorkflowPhase.DataAnalysis, state.Phase);

        Assert.True(state.ModelGatePassed);

        Assert.Single(EventModelContractsAccumulator.ReadResolved(state));



        state.Phase = CampaignWorkflowPhase.EventModels;

        state.ModelGatePassed = false;

        state.Artifacts.SelectedEventModelId = "o1";

        EventModelContractsAccumulator.Clear(state);



        CampaignWorkflowEngine.ApplyUserMessage(state, "move on to the next phase");



        Assert.Equal(CampaignWorkflowPhase.DataAnalysis, state.Phase);

        Assert.False(state.ModelGatePassed);

    }



    [Fact]

    public void Create_new_clears_gate_with_null_selection()

    {

        var state = GatedState();

        CampaignWorkflowEngine.ApplyUserMessage(state, "create a new event model");



        Assert.Equal(CampaignWorkflowApprovalKind.None, state.Artifacts.AwaitingApproval);

        Assert.Null(state.Artifacts.SelectedEventModelId);

        Assert.Equal(CampaignWorkflowPhase.EventModels, state.Phase);

    }



    [Fact]

    public void Create_the_model_clears_gate_with_null_selection()

    {

        var state = GatedState();

        CampaignWorkflowEngine.ApplyUserMessage(state, "create the model");



        Assert.Equal(CampaignWorkflowApprovalKind.None, state.Artifacts.AwaitingApproval);

        Assert.Null(state.Artifacts.SelectedEventModelId);

        Assert.True(state.Artifacts.UserRequestedNewEventModel);

    }



    [Fact]

    public void Create_a_model_called_sets_UserRequestedNewEventModel_before_gate()

    {

        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");

        state.Phase = CampaignWorkflowPhase.EventModels;

        CampaignWorkflowEngine.ApplyUserMessage(state, "create a model called Review for campaign reviews");



        Assert.True(state.Artifacts.UserRequestedNewEventModel);

        Assert.Equal(CampaignWorkflowApprovalKind.None, state.Artifacts.AwaitingApproval);

    }



    [Fact]

    public void Ambiguous_reply_keeps_the_gate()

    {

        var state = GatedState();

        CampaignWorkflowEngine.ApplyUserMessage(state, "what does eventable mean?");



        Assert.Equal(CampaignWorkflowApprovalKind.EventModelSelection, state.Artifacts.AwaitingApproval);

        Assert.Null(state.Artifacts.SelectedEventModelId);

    }



    [Fact]

    public void Rewind_to_data_analysis_phrase_during_selection_gate_does_not_clear_brief_artifacts()

    {

        var state = GatedState();

        state.Phase = CampaignWorkflowPhase.EventModels;

        state.Artifacts.CampaignDesignBriefApproved = """{"version":1,"objective":"test"}""";
        state.Artifacts.CampaignDesignBriefProposed = state.Artifacts.CampaignDesignBriefApproved;

        CampaignWorkflowEngine.ApplyUserMessage(state, "redo analysis");

        Assert.Equal(CampaignWorkflowPhase.DataAnalysis, state.Phase);

        Assert.NotNull(state.Artifacts.CampaignDesignBriefApproved);
        Assert.NotNull(state.Artifacts.CampaignDesignBriefProposed);

        Assert.Equal(0, state.Artifacts.EventModelSaveFailureCount);

    }



    [Fact]

    public void Rewind_to_event_models_phrase_during_selection_gate_keeps_candidate_artifacts()

    {

        var state = GatedState();

        state.Artifacts.SelectedEventModelId = "o1";

        state.Artifacts.EarningIntent = EarningIntent.PointEarning;



        CampaignWorkflowEngine.ApplyUserMessage(state, "redo models");



        Assert.True(EventModelCandidatesArtifact.HasComputed(state));

        Assert.Equal("o1", state.Artifacts.SelectedEventModelId);

        Assert.Equal(EarningIntent.PointEarning, state.Artifacts.EarningIntent);

    }



    private static string ModelToolJson(string id, string name) =>

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


