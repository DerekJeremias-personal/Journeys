using System.Text.Json;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.DTO.Models;
using Xunit;

namespace Journeys.Tests.Utility;

public class EventModelsReadinessTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void wrapper_save_does_not_populate_resolved()
    {
        var s = InEventModels();
        const string eventId = "23f40be8-28ce-4972-9dc6-76c38e0b0768";
        const string wrapperId = "4a282fdf-375e-4d6b-942e-a226a9f86b2d";

        EventModelContractsAccumulator.TryMergeFromToolResult(s, "save_model", CorruptWrapperJson(wrapperId, eventId));

        Assert.Empty(EventModelContractsAccumulator.ReadResolved(s));
        Assert.NotEmpty(WrapperContractArtifact.Read(s));
        Assert.False(EventModelsReadiness.Evaluate(s).IsReady);
    }

    [Fact]
    public void pending_review_blocked_when_only_wrapper_resolved()
    {
        var s = InEventModels();
        s.Artifacts.ResolvedEventModelContracts =
            """[{"schemaVersion":1,"eventModelId":"376e6456-d764-45d6-be74-915c6bb75279","eventModelName":"ReviewAndRuleState","eventModelType":"loyalty","isProcessEventEligible":false}]""";

        var result = EventModelsReadiness.Evaluate(s);

        Assert.False(result.IsReady);
        Assert.Contains("wrapper_in_resolved_list", result.Blockers);
        Assert.Contains("no_resolved_event_model", result.Blockers);
    }

    [Fact]
    public void create_requested_get_model_does_not_satisfy()
    {
        var s = InEventModels();
        PendingEventModelSpecArtifact.Write(s, new PendingEventModelSpecDto { Name = "Review" });
        s.Artifacts.UserRequestedNewEventModel = true;

        EventModelContractsAccumulator.TryMergeFromToolResult(s, "get_model", EventModelJson("r1", "Review", "w1", tag: "eventable"));

        var result = EventModelsReadiness.Evaluate(s);

        Assert.False(result.IsReady);
        Assert.Contains("event_model_reuse_confirm_pending", result.Blockers);
        Assert.Empty(EventModelContractsAccumulator.ReadResolved(s));
    }

    [Fact]
    public void valid_event_and_wrapper_ready()
    {
        const string eventId = "23f40be8-28ce-4972-9dc6-76c38e0b0768";
        const string wrapperId = "4a282fdf-375e-4d6b-942e-a226a9f86b2d";
        var s = InEventModels();
        PendingEventModelSpecArtifact.Write(s, new PendingEventModelSpecDto { Name = "Review" });
        EventModelContractsAccumulator.TryMergeFromToolResult(s, "get_model", EventModelJson(eventId, "Review", wrapperId, tag: "eventable"));
        EventModelContractsAccumulator.TryMergeFromToolResult(s, "get_model", CanonicalWrapperJson(wrapperId, eventId));

        Assert.True(EventModelsReadiness.Evaluate(s).IsReady);
    }

    [Fact]
    public void corrupt_wrapper_coach_mentions_build_event_wrapper()
    {
        const string eventId = "23f40be8-28ce-4972-9dc6-76c38e0b0768";
        const string wrapperId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        var s = InEventModels();
        PendingEventModelSpecArtifact.Write(s, new PendingEventModelSpecDto { Name = "Review" });
        EventModelContractsAccumulator.TryMergeFromToolResult(s, "get_model", EventModelJson(eventId, "Review", wrapperId, tag: "eventable"));
        EventModelContractsAccumulator.TryMergeFromToolResult(s, "get_model", CorruptWrapperJson(wrapperId, eventId));

        var result = EventModelsReadiness.Evaluate(s);

        Assert.False(result.IsReady);
        Assert.Contains("wrapper_contract_invalid", result.Blockers);
        Assert.Contains("wrapper invalid", result.CoachMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("build_event_wrapper", result.CoachMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(eventId, result.CoachMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(wrapperId, result.CoachMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void save_model_pat_shaped_does_not_populate_resolved()
    {
        var s = InEventModels();
        EventModelContractsAccumulator.TryMergeFromToolResult(s, "save_model", PatShapedModelJson());

        Assert.Empty(EventModelContractsAccumulator.ReadResolved(s));
        Assert.False(EventModelsReadiness.Evaluate(s).IsReady);
    }

    [Fact]
    public void save_model_campaign_shaped_does_not_populate_resolved()
    {
        var s = InEventModels();
        EventModelContractsAccumulator.TryMergeFromToolResult(s, "save_model", CampaignShapedModelJson());

        Assert.Empty(EventModelContractsAccumulator.ReadResolved(s));
    }

    [Fact]
    public void selection_pending_strong_match_coach_mentions_one_line_confirm()
    {
        var s = InEventModels();
        s.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.EventModelSelection;
        EventModelCandidatesArtifact.Write(s, new EventModelCandidateSet
        {
            RecommendedDefaultId = "o1",
            Candidates =
            [
                new EventModelCandidate { EventModelId = "o1", Name = "Order", IsStrongMatch = true }
            ]
        });

        var result = EventModelsReadiness.Evaluate(s);

        Assert.False(result.IsReady);
        Assert.Contains("one-line", result.CoachMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Order", result.CoachMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void reuse_confirm_pending_when_discovered_matches_pending()
    {
        var s = InEventModels();
        PendingEventModelSpecArtifact.Write(s, new PendingEventModelSpecDto { Name = "Review" });
        s.Artifacts.UserRequestedNewEventModel = true;
        EventModelContractsAccumulator.TryMergeFromToolResult(s, "get_model", EventModelJson("r1", "Review", "w1", tag: "eventable"));

        var result = EventModelsReadiness.Evaluate(s);

        Assert.False(result.IsReady);
        Assert.Contains("event_model_reuse_confirm_pending", result.Blockers);
        Assert.Contains("Confirm reuse", result.CoachMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void reuse_confirm_promotes_discovered_to_resolved()
    {
        var s = InEventModels();
        PendingEventModelSpecArtifact.Write(s, new PendingEventModelSpecDto { Name = "Review" });
        s.Artifacts.UserRequestedNewEventModel = true;
        EventModelContractsAccumulator.TryMergeFromToolResult(s, "get_model", EventModelJson("r1", "Review", "w1", tag: "eventable"));

        Assert.True(EventModelReuseHelper.TryConfirmReuseFromUserMessage(s, "yes reuse the existing Review"));

        Assert.Single(EventModelContractsAccumulator.ReadResolved(s));
        Assert.False(s.Artifacts.UserRequestedNewEventModel);
    }

    private static CampaignWorkflowState InEventModels()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Phase = CampaignWorkflowPhase.EventModels;
        s.CampaignKind = CampaignWorkflowKind.EventDriven;
        s.Artifacts.CampaignDesignBriefProposed = """{"objective":"x"}""";
        return s;
    }

    private static string EventModelJson(string id, string name, string wrapperId, string? tag = null) =>
        JsonSerializer.Serialize(new
        {
            id,
            name,
            tag,
            modelType = "loyalty",
            modelMetaData = new Dictionary<string, string>
            {
                ["Wrapper"] = wrapperId,
                ["NaturalKeySymbols"] = "[\"reviewid\"]",
                ["AccountXIdSymbol"] = "customerid"
            },
            attributes = new[] { new { symbol = "reviewid", type = "Primitive", dataType = "string", displayName = "Id" } }
        }, JsonOpts);

    private static string CorruptWrapperJson(string id, string eventModelId) =>
        $$"""
            {
              "id": "{{id}}",
              "name": "ReviewAndRuleState",
              "modelType": "loyalty",
              "isContainer": true,
              "attributes": [
                { "symbol": "accountid", "type": "Primitive", "dataType": "String", "status": "Live" },
                { "symbol": "naturalkey", "type": "Primitive", "dataType": "String", "status": "Live" },
                { "symbol": "timeofoccurrence", "type": "Primitive", "dataType": "Date", "status": "Live" },
                {
                  "symbol": "event",
                  "type": "ModelObject",
                  "dataType": "Object",
                  "modelId": "{{eventModelId}}",
                  "modelType": "loyalty",
                  "status": "Live"
                },
                { "symbol": "appliedcampaigns", "type": "ModelList", "dataType": "List", "listAttributeDataType": "String", "status": "Live" },
                {
                  "symbol": "journeys",
                  "type": "ModelList",
                  "dataType": "List",
                  "listAttributeDataType": "Object",
                  "status": "Live"
                }
              ]
            }
            """;

    private static string CanonicalWrapperJson(string wrapperId, string eventModelId) =>
        $$"""
            {
              "id": "{{wrapperId}}",
              "name": "ReviewAndRuleState",
              "modelType": "loyalty",
              "isContainer": true,
              "attributes": [
                { "symbol": "accountid", "type": "Primitive", "dataType": "String", "status": "Live" },
                { "symbol": "naturalkey", "type": "Primitive", "dataType": "String", "status": "Live" },
                { "symbol": "timeofoccurrence", "type": "Primitive", "dataType": "Date", "status": "Live" },
                { "symbol": "lastprocessed", "type": "Primitive", "dataType": "Date", "status": "Live" },
                {
                  "symbol": "event",
                  "type": "ModelObject",
                  "dataType": "Object",
                  "modelId": "{{eventModelId}}",
                  "modelType": "loyalty",
                  "status": "Live"
                },
                { "symbol": "eventwrapper", "type": "ModelDynamic", "dataType": "Dynamic", "status": "Live" },
                { "symbol": "appliedcampaigns", "type": "ModelList", "dataType": "List", "listAttributeDataType": "String", "status": "Live" },
                { "symbol": "appliedrulesetids", "type": "ModelList", "dataType": "List", "listAttributeDataType": "String", "status": "Live" },
                {
                  "symbol": "providerstates",
                  "type": "ModelKeyValue",
                  "dataType": "KeyValue",
                  "keyValueAttributeDataType": "Object",
                  "modelId": "11111111-1111-1111-1111-111111111111",
                  "modelType": "loyalty",
                  "status": "Live"
                },
                {
                  "symbol": "journeystates",
                  "type": "ModelKeyValue",
                  "dataType": "KeyValue",
                  "keyValueAttributeDataType": "Object",
                  "modelId": "22222222-2222-2222-2222-222222222222",
                  "modelType": "loyalty",
                  "status": "Live"
                },
                {
                  "symbol": "outcomestates",
                  "type": "ModelList",
                  "dataType": "List",
                  "listAttributeDataType": "Object",
                  "modelId": "33333333-3333-3333-3333-333333333333",
                  "modelType": "loyalty",
                  "status": "Live"
                }
              ]
            }
            """;

    private static string PatShapedModelJson() =>
        """
            {
              "id": "6983defa-b920-4a4f-a3fa-d4508083dddf",
              "name": "ReviewRewards",
              "modelType": "loyalty",
              "isContainer": true,
              "attributes": [
                { "symbol": "extaccountid", "type": "Primitive", "dataType": "String" },
                { "symbol": "pointsourceid", "type": "Primitive", "dataType": "String" },
                { "symbol": "ledgertype", "type": "Primitive", "dataType": "String" }
              ]
            }
            """;

    private static string CampaignShapedModelJson() =>
        """
            {
              "id": "541e2cc8-0000-0000-0000-000000000001",
              "name": "HighQualityReviewerRewards",
              "modelType": "loyalty",
              "isContainer": true,
              "attributes": [
                { "symbol": "extcampaignid", "type": "Primitive", "dataType": "String" },
                { "symbol": "journey", "type": "ModelObject", "dataType": "Object" }
              ]
            }
            """;
}
