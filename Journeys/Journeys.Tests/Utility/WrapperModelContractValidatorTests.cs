using System.Text.Json;
using Backend.Dto.Structures.Model;
using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Utility;

public class WrapperModelContractValidatorTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void Validate_canonical_order_wrapper_passes()
    {
        var eventId = "23f40be8-28ce-4972-9dc6-76c38e0b0768";
        var wrapper = Deserialize(CanonicalWrapperJson("f00df00d-dead-f00d-f00d-ea7f00d1337e", eventId));

        var result = WrapperModelContractValidator.Validate(wrapper);

        Assert.True(result.IsStructurallyValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_review_corrupt_wrapper_fails_wrong_symbols_and_missing_modelId()
    {
        var wrapper = Deserialize(CorruptReviewWrapperJson());

        var result = WrapperModelContractValidator.Validate(wrapper);

        Assert.False(result.IsStructurallyValid);
        Assert.Contains(result.Errors, e => e.Contains("journeys", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, e => e.Contains("journeystates", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, e => e.Contains("providerstates", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, e => e.Contains("outcomestates", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LooksLikeEventWrapper_true_for_container_andRuleState()
    {
        var wrapper = Deserialize("""
            {
              "id": "w1",
              "name": "ReviewAndRuleState",
              "isContainer": true
            }
            """);

        Assert.True(WrapperModelContractValidator.LooksLikeEventWrapper(wrapper));
    }

    [Fact]
    public void LooksLikeEventWrapper_false_for_event_payload()
    {
        var model = Deserialize("""
            {
              "id": "e1",
              "name": "Review",
              "isContainer": false
            }
            """);

        Assert.False(WrapperModelContractValidator.LooksLikeEventWrapper(model));
    }

    private static ModelDto Deserialize(string json) =>
        JsonSerializer.Deserialize<ModelDto>(json, JsonOpts)!;

    private static string CanonicalWrapperJson(string wrapperId, string eventModelId) =>
        $$"""
            {
              "id": "{{wrapperId}}",
              "name": "OrderAndRuleState",
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

    private static string CorruptReviewWrapperJson() =>
        """
            {
              "id": "4a282fdf-375e-4d6b-942e-a226a9f86b2d",
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
                  "modelId": "23f40be8-28ce-4972-9dc6-76c38e0b0768",
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
}
