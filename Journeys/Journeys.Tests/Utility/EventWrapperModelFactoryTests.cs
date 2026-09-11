using System.Text.Json;
using Backend.Dto.Structures.Model;
using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Utility;

public class EventWrapperModelFactoryTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void TryExtractEngineReferenceIds_from_canonical_reference_succeeds()
    {
        var wrapper = Deserialize(CanonicalWrapperJson("f00df00d-dead-f00d-f00d-ea7f00d1337e", "23f40be8-28ce-4972-9dc6-76c38e0b0768"));

        var ok = EventWrapperEngineReferenceIds.TryExtract(wrapper, out var ids);

        Assert.True(ok);
        Assert.NotNull(ids);
        Assert.Equal("11111111-1111-1111-1111-111111111111", ids.ProviderStatesModelId);
        Assert.Equal("22222222-2222-2222-2222-222222222222", ids.JourneyStatesModelId);
        Assert.Equal("33333333-3333-3333-3333-333333333333", ids.OutcomeStatesModelId);
    }

    [Fact]
    public void Build_create_produces_validator_passing_wrapper()
    {
        var eventModel = Deserialize(EventModelJson("23f40be8-28ce-4972-9dc6-76c38e0b0768", "Review"));
        var referenceWrapper = Deserialize(CanonicalWrapperJson("f00df00d-dead-f00d-f00d-ea7f00d1337e", eventModel.ID));

        var result = EventWrapperModelFactory.Build(eventModel, referenceWrapper);

        Assert.True(result.Success);
        Assert.NotNull(result.Model);
        Assert.Null(result.Model.ID);
        Assert.Equal("ReviewAndRuleState", result.Model.Name);
        Assert.Equal(referenceWrapper.ID, result.ReferenceWrapperModelId);

        var validation = WrapperModelContractValidator.Validate(result.Model, eventModel);
        Assert.True(validation.IsStructurallyValid);
        Assert.Empty(validation.Errors);
    }

    [Fact]
    public void Build_repair_preserves_wrapper_id()
    {
        var existingWrapperId = "7f0fa13d-f6af-4410-8854-617ebcbf633a";
        var eventModel = Deserialize(EventModelJson("23f40be8-28ce-4972-9dc6-76c38e0b0768", "Review"));
        var referenceWrapper = Deserialize(CanonicalWrapperJson("f00df00d-dead-f00d-f00d-ea7f00d1337e", eventModel.ID));

        var result = EventWrapperModelFactory.Build(eventModel, referenceWrapper, existingWrapperId);

        Assert.True(result.Success);
        Assert.NotNull(result.Model);
        Assert.Equal(existingWrapperId, result.Model.ID);
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

    private static string EventModelJson(string eventModelId, string eventName, bool includeWrapperModelMetaData = false)
    {
        var wrapperMetadata = includeWrapperModelMetaData
            ? """
              ,
              "modelMetaData": {
                "Wrapper": "true"
              }
              """
            : string.Empty;

        return $$"""
            {
              "id": "{{eventModelId}}",
              "name": "{{eventName}}",
              "modelType": "loyalty",
              "tag": "eventable"{{wrapperMetadata}}
            }
            """;
    }
}
