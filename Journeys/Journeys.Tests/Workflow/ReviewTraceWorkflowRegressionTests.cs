using System.Text.Json;
using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Workflow;

public class ReviewTraceWorkflowRegressionTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void Test_with_sample_payload_after_journey_advances_to_verification()
    {
        var s = CampaignWorkflowState.CreateDefault("primo", "u", "c");
        s.Phase = CampaignWorkflowPhase.CampaignJourney;
        s.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.None;
        s.Artifacts.JourneyDigestProposed = BriefJson();

        CampaignWorkflowStepManager.ApplyUserMessage(s,
            "Awesome! Please test with a sample payload, you can use the test_exp_01 user", dataWarehouseEnabled: false);

        Assert.Equal(CampaignWorkflowPhase.Verification, s.Phase);
        Assert.Equal(CampaignWorkflowApprovalKind.None, s.Artifacts.AwaitingApproval);
    }

    [Fact]
    public void Yes_after_brief_capture_keeps_focus_on_event_models_without_clearing_approval_flag()
    {
        var s = CampaignWorkflowState.CreateDefault("primo", "u", "c");
        s.Artifacts.DataAnalysisObjectiveCaptured = true;
        s.Artifacts.CampaignDesignBriefProposed = BriefJson();
        s.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.DesignBrief;
        PendingEventModelSpecArtifact.TryCaptureFromUserMessage(s,
            "create a model called Review with CustomerId and Title");
        s.Artifacts.UserRequestedNewEventModel = true;

        CampaignWorkflowStepManager.ApplyUserMessage(s, "yes", dataWarehouseEnabled: false);

        Assert.Equal(CampaignWorkflowApprovalKind.DesignBrief, s.Artifacts.AwaitingApproval);
        Assert.Null(s.Artifacts.CampaignDesignBriefApproved);
        Assert.Equal(CampaignWorkflowPhase.EventModels, s.Phase);
    }

    [Fact]
    public void Review_trace_sequence_without_successful_review_save_never_reaches_campaign_setup()
    {
        var s = CampaignWorkflowState.CreateDefault("primo", "u", "c");

        CampaignWorkflowStepManager.ApplyUserMessage(s,
            "create a model called Review and give it String CustomerId, String Title", true);
        var pending = PendingEventModelSpecArtifact.Read(s);
        Assert.NotNull(pending);
        Assert.Equal("Review", pending!.Name);

        s.Artifacts.CampaignDesignBriefApproved = BriefJson();
        s.Artifacts.CampaignDesignBriefProposed = s.Artifacts.CampaignDesignBriefApproved;
        s.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.None;
        CampaignWorkflowStepManager.ApplyUserMessage(s, null);
        Assert.Equal(CampaignWorkflowPhase.EventModels, s.Phase);

        for (var i = 0; i < 3; i++)
            CampaignWorkflowStepManager.ApplyToolResults(s,
                [("save_model", """{"error":true,"message":"fail"}""")], true);
        Assert.Equal(CampaignWorkflowPhase.EventModels, s.Phase);
        Assert.Equal(3, s.Artifacts.EventModelSaveFailureCount);
        Assert.Empty(EventModelContractsAccumulator.ReadResolved(s));

        CampaignWorkflowStepManager.ApplyToolResults(s,
            [("get_model", ModelJson("d55fcdab-4dec-42d7-a746-941496c8f01f", "LoyaltyAccountDetails"))], true);
        Assert.Equal(CampaignWorkflowPhase.EventModels, s.Phase);
        Assert.False(s.ModelGatePassed);
        Assert.Empty(EventModelContractsAccumulator.ReadResolved(s));
        Assert.Single(EventModelContractsAccumulator.ReadDiscovered(s));
        Assert.Equal("LoyaltyAccountDetails", EventModelContractsAccumulator.ReadDiscovered(s)[0].EventModelName);
    }

    [Fact]
    public void Review_discovery_after_failed_save_get_model_only_does_not_complete_events()
    {
        var s = CampaignWorkflowState.CreateDefault("primo", "u", "c");
        PendingEventModelSpecArtifact.TryCaptureFromUserMessage(s,
            "create a model called Review with CustomerId and Title");
        s.Artifacts.UserRequestedNewEventModel = true;
        s.Artifacts.CampaignDesignBriefApproved = BriefJson();
        s.Artifacts.CampaignDesignBriefProposed = s.Artifacts.CampaignDesignBriefApproved;
        CampaignWorkflowStepManager.ApplyUserMessage(s, null);
        Assert.Equal(CampaignWorkflowPhase.EventModels, s.Phase);

        CampaignWorkflowStepManager.ApplyToolResults(s,
            [("save_model", """{"error":true,"message":"fail"}""")], true);
        CampaignWorkflowStepManager.ApplyToolResults(s,
            [("get_model", ReviewModelJson())], true);

        Assert.Equal(CampaignWorkflowPhase.EventModels, s.Phase);
        Assert.False(s.ModelGatePassed);
        Assert.False(CampaignWorkflowChecklist.IsItemComplete(s, CampaignWorkflowPhase.EventModels));
        Assert.False(PendingEventModelSpecArtifact.IsSatisfied(s));
    }

    [Fact]
    public void Wrapper_contract_errors_block_campaign_mutators_in_tool_filter()
    {
        var s = CampaignWorkflowState.CreateDefault("primo", "u", "c");
        s.Artifacts.CampaignDesignBriefApproved = BriefJson();
        s.Artifacts.CampaignDesignBriefProposed = s.Artifacts.CampaignDesignBriefApproved;
        s.Artifacts.UserRequestedNewEventModel = true;
        s.Artifacts.ResolvedEventModelContracts =
            """[{"schemaVersion":1,"eventModelId":"4d9f50bd-17ab-46e4-9369-70861dcaa664","eventModelName":"Review","wrapperModelId":"376e6456-d764-45d6-be74-915c6bb75279"}]""";
        s.Artifacts.WrapperContractValidation =
            """[{"wrapperModelId":"376e6456-d764-45d6-be74-915c6bb75279","wrapperModelName":"ReviewAndRuleState","errors":["Missing required wrapper attribute 'accountid'."],"warnings":[]}]""";

        CampaignWorkflowStepManager.TryUpdateChecklist(s);

        Assert.False(s.ModelGatePassed);
        Assert.False(CampaignWorkflowChecklist.IsItemComplete(s, CampaignWorkflowPhase.EventModels));
        Assert.True(CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(s));
    }

    private static string BriefJson() =>
        JsonSerializer.Serialize(new { version = 1 }, JsonOpts);

    private static string ModelJson(string id, string name) =>
        JsonSerializer.Serialize(new
        {
            id,
            name,
            modelType = "event",
            modelMetaData = new Dictionary<string, string>
            {
                ["Wrapper"] = "w1",
                ["NaturalKeySymbols"] = "[\"orderid\"]",
                ["AccountXIdSymbol"] = "orderid"
            },
            attributes = new[] { new { symbol = "orderid", type = "Primitive", dataType = "string", displayName = "Id" } }
        }, JsonOpts);

    private static string ReviewModelJson() =>
        JsonSerializer.Serialize(new
        {
            id = "fbfe5201-d2e2-4ea4-9fc9-e79343adcfc1",
            name = "Review",
            tag = "eventable",
            modelType = "loyalty",
            modelMetaData = new Dictionary<string, string>
            {
                ["Wrapper"] = "993c1871-fb79-4399-8dcb-1bda5f6106cc",
                ["NaturalKeySymbols"] = "[\"reviewid\"]",
                ["AccountXIdSymbol"] = "customerid",
                ["TimeOfOccurrence"] = "reviewdate"
            },
            attributes = new[]
            {
                new { symbol = "customerid", type = "Primitive", dataType = "string", displayName = "Customer" },
                new { symbol = "reviewid", type = "Primitive", dataType = "string", displayName = "Review" }
            }
        }, JsonOpts);
}
