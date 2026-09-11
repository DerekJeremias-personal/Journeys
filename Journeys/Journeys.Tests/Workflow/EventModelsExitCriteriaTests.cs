using System.Text.Json;
using Journeys.API.CampaignAgent.Workflow.Steps;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Workflow;

public class EventModelsExitCriteriaTests
{
    private static readonly EventModelsExitCriteria Criteria = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void Not_met_for_discovery_only()
    {
        var s = InEventModels();
        EventModelContractsAccumulator.TryMergeFromToolResult(s, "get_model", ModelJson("d1", "LoyaltyAccountDetails"));
        Assert.False(Criteria.IsMet(s));
    }

    private static void SetValidWrapperValidation(CampaignWorkflowState s, string wrapperId = "w1") =>
        s.Artifacts.WrapperContractValidation =
            $$"""[{"wrapperModelId":"{{wrapperId}}","wrapperModelName":"ReviewAndRuleState","errors":[],"warnings":[]}]""";

    [Fact]
    public void Met_for_pending_review_after_get_model_promotes_discovered()
    {
        var s = InEventModels();
        PendingEventModelSpecArtifact.Write(s, new PendingEventModelSpecDto { Name = "Review" });
        EventModelContractsAccumulator.TryMergeFromToolResult(s, "get_model", ModelJson("r1", "Review", tag: "eventable"));
        SetValidWrapperValidation(s);
        Assert.True(Criteria.IsMet(s));
    }

    [Fact]
    public void Met_for_pending_review_after_save_model()
    {
        var s = InEventModels();
        PendingEventModelSpecArtifact.Write(s, new PendingEventModelSpecDto { Name = "Review" });
        EventModelContractsAccumulator.TryMergeFromToolResult(s, "save_model", ModelJson("r1", "Review", tag: "eventable"));
        SetValidWrapperValidation(s);
        Assert.True(Criteria.IsMet(s));
    }

    [Fact]
    public void Not_met_when_pending_review_but_only_wrong_model_saved()
    {
        var s = InEventModels();
        PendingEventModelSpecArtifact.Write(s, new PendingEventModelSpecDto { Name = "Review" });
        EventModelContractsAccumulator.TryMergeFromToolResult(s, "save_model", ModelJson("x1", "Order"));
        Assert.False(Criteria.IsMet(s));
    }

    [Fact]
    public void Not_met_when_pending_review_but_planned_ids_satisfied_by_other_model()
    {
        var s = InEventModels();
        PendingEventModelSpecArtifact.Write(s, new PendingEventModelSpecDto { Name = "Review" });
        s.Artifacts.CampaignDesignBriefApproved = BriefWithPlannedIds(["idA"]);
        EventModelContractsAccumulator.TryMergeFromToolResult(s, "save_model", ModelJson("idA", "Order"));
        Assert.False(Criteria.IsMet(s));
    }

    [Fact]
    public void Not_met_when_event_model_selection_gate_active()
    {
        var s = InEventModels();
        s.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.EventModelSelection;
        EventModelContractsAccumulator.TryMergeFromToolResult(s, "save_model", ModelJson("r1", "Review"));
        Assert.False(Criteria.IsMet(s));
    }

    [Fact]
    public void Met_for_reuse_when_selected_id_has_resolved_contract()
    {
        var s = InEventModels();
        s.Artifacts.SelectedEventModelId = "d1";
        EventModelContractsAccumulator.TryMergeFromToolResult(s, "save_model", ModelJson("d1", "LoyaltyAccountDetails", tag: "eventable"));
        SetValidWrapperValidation(s);
        Assert.True(Criteria.IsMet(s));
    }

    [Fact]
    public void Not_met_when_event_resolved_but_wrapper_structurally_invalid()
    {
        const string eventId = "23f40be8-28ce-4972-9dc6-76c38e0b0768";
        const string wrapperId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        var s = InEventModels();
        PendingEventModelSpecArtifact.Write(s, new PendingEventModelSpecDto { Name = "Review" });
        EventModelContractsAccumulator.TryMergeFromToolResult(s, "get_model", EventModelJson(eventId, "Review", wrapperId));
        EventModelContractsAccumulator.TryMergeFromToolResult(s, "get_model", CorruptWrapperJson(wrapperId, eventId));
        Assert.False(Criteria.IsMet(s));
    }

    [Fact]
    public void Met_when_event_and_wrapper_both_valid()
    {
        const string eventId = "23f40be8-28ce-4972-9dc6-76c38e0b0768";
        const string wrapperId = "4a282fdf-375e-4d6b-942e-a226a9f86b2d";
        var s = InEventModels();
        PendingEventModelSpecArtifact.Write(s, new PendingEventModelSpecDto { Name = "Review" });
        EventModelContractsAccumulator.TryMergeFromToolResult(s, "get_model", EventModelJson(eventId, "Review", wrapperId, tag: "eventable"));
        EventModelContractsAccumulator.TryMergeFromToolResult(s, "get_model", CanonicalWrapperJson(wrapperId, eventId));
        Assert.True(Criteria.IsMet(s));
    }

    private static CampaignWorkflowState InEventModels()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Phase = CampaignWorkflowPhase.EventModels;
        s.CampaignKind = CampaignWorkflowKind.EventDriven;
        return s;
    }

    private static string BriefWithPlannedIds(IEnumerable<string> ids) =>
        JsonSerializer.Serialize(new { version = 1, plannedEventModelIds = ids }, JsonOpts);

    private static string ModelJson(string id, string name, string? tag = null) =>
        EventModelJson(id, name, "w1", tag);

    private static string EventModelJson(string id, string name, string wrapperId, string? tag = null) =>
        JsonSerializer.Serialize(new
        {
            id,
            name,
            tag,
            modelType = "event",
            modelMetaData = new Dictionary<string, string>
            {
                ["Wrapper"] = wrapperId,
                ["NaturalKeySymbols"] = "[\"orderid\"]",
                ["AccountXIdSymbol"] = "orderid"
            },
            attributes = new[] { new { symbol = "orderid", type = "Primitive", dataType = "string", displayName = "Id" } }
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
                },
                {
                  "symbol": "outcomes",
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
}
