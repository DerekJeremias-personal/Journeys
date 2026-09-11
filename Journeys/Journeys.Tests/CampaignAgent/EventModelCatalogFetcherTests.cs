using System.Linq;
using System.Text.Json;
using Backend.Dto.Structures.Model;
using Journeys.API.CampaignAgent;
using Microsoft.Extensions.AI;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public class EventModelCatalogFetcherTests
{
    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    [Fact]
    public void ParseModels_raw_array_deserializes_both_with_camelcase_fields()
    {
        const string json = """
        [
            { "id": "m1", "name": "Order", "displayName": "Order", "modelType": "event", "tag": "eventable", "isContainer": true },
            { "id": "m2", "name": "Customer", "displayName": "Customer", "modelType": "profile", "tag": "profile", "isContainer": false }
        ]
        """;

        var models = EventModelCatalogFetcher.ParseModels(Parse(json));

        Assert.Equal(2, models.Count);

        Assert.Equal("m1", models[0].ID);
        Assert.Equal("Order", models[0].Name);
        Assert.Equal("event", models[0].ModelType);
        Assert.Equal("eventable", models[0].Tag);
        Assert.True(models[0].IsContainer);

        Assert.Equal("m2", models[1].ID);
        Assert.Equal("Customer", models[1].Name);
        Assert.Equal("profile", models[1].ModelType);
        Assert.Equal("profile", models[1].Tag);
        Assert.False(models[1].IsContainer);
    }

    [Fact]
    public void ParseModels_primitive_attribute_lands_on_attribute_props()
    {
        const string json = """
        [
            {
                "id": "m1",
                "name": "Order",
                "modelType": "event",
                "attributes": [
                    { "type": "Primitive", "symbol": "grandTotal", "dataType": "decimal", "displayName": "Grand Total" }
                ]
            }
        ]
        """;

        var models = EventModelCatalogFetcher.ParseModels(Parse(json));

        Assert.Single(models);
        Assert.NotNull(models[0].Attributes);
        Assert.Single(models[0].Attributes);
        Assert.Equal("decimal", models[0].Attributes[0].DataType);
        Assert.Equal("grandTotal", models[0].Attributes[0].Symbol);
        Assert.Equal("Grand Total", models[0].Attributes[0].DisplayName);
    }

    [Fact]
    public void ParseModels_object_envelope_under_models_is_handled()
    {
        const string json = """
        {
            "count": 1,
            "models": [
                { "id": "m1", "name": "Order", "modelType": "event" }
            ]
        }
        """;

        var models = EventModelCatalogFetcher.ParseModels(Parse(json));

        Assert.Single(models);
        Assert.Equal("m1", models[0].ID);
    }

    [Fact]
    public void ParseModels_unwraps_text_wrapper()
    {
        const string json = """
        { "$type": "text", "text": "[ { \"id\": \"m1\", \"name\": \"Order\", \"modelType\": \"event\" } ]" }
        """;

        var models = EventModelCatalogFetcher.ParseModels(Parse(json));

        Assert.Single(models);
        Assert.Equal("m1", models[0].ID);
        Assert.Equal("Order", models[0].Name);
    }

    [Fact]
    public void ParseModels_skips_malformed_model_keeps_sibling()
    {
        // First model has an attribute missing the required "type" discriminator -> converter throws -> skip it.
        const string json = """
        [
            {
                "id": "bad",
                "name": "Broken",
                "modelType": "event",
                "attributes": [
                    { "symbol": "noType", "dataType": "decimal", "displayName": "No Type" }
                ]
            },
            {
                "id": "good",
                "name": "Order",
                "modelType": "event"
            }
        ]
        """;

        var models = EventModelCatalogFetcher.ParseModels(Parse(json));

        Assert.Single(models);
        Assert.Equal("good", models[0].ID);
    }

    [Fact]
    public void ParseModels_non_array_root_returns_empty_without_throwing()
    {
        const string json = """
        { "message": "no models here", "count": 0 }
        """;

        var models = EventModelCatalogFetcher.ParseModels(Parse(json));

        Assert.NotNull(models);
        Assert.Empty(models);
    }

    [Fact]
    public void BuildCatalogToolNamesToTry_prefers_snake_case_and_dedupes_aliases()
    {
        var tools = new List<AITool>
        {
            new NamedTool("GetAllModels"),
            new NamedTool("get_all_models")
        };

        var names = EventModelCatalogFetcher.BuildCatalogToolNamesToTry(tools);

        Assert.True(names.Count >= 2);
        Assert.Equal("get_all_models", names[0]);
        Assert.Contains("GetAllModels", names);
    }

    private sealed class NamedTool(string name) : AITool
    {
        public override string Name => name;
        public override string Description => name;
    }
}
