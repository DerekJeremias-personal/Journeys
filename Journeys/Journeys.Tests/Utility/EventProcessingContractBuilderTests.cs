using Backend.Dto.Structures.Model;
using Backend.Dto.Structures.Model.Attributes;
using Journeys.Core.Utility;
using System.Text.Json;
using Xunit;

namespace Journeys.Tests.Utility;

public class EventProcessingContractBuilderTests
{
    [Fact]
    public void BuildFromModel_order_example_metadata()
    {
        var model = new ModelDto
        {
            ID = "a6edbbc5-bf43-4c57-b2f1-e015b9efaf03",
            Name = "order",
            ModelType = "loyalty",
            Tag = "eventable",
            Attributes =
            [
                new ModelAttributePrimitiveDto { Symbol = "orderid", DataType = "string", DisplayName = "Order id", Status = "Live" },
                new ModelAttributePrimitiveDto { Symbol = "profileid", DataType = "string", DisplayName = "Profile", Status = "Live" },
                new ModelAttributePrimitiveDto { Symbol = "timestamp", DataType = "datetime", DisplayName = "Ts", Status = "Live" }
            ],
            ModelMetaData = new Dictionary<string, string>
            {
                ["Wrapper"] = "f00df00d-dead-f00d-f00d-ea7f00d1337e",
                ["NaturalKeySymbols"] = "[\"orderid\"]",
                ["AccountXIdSymbol"] = "profileid",
                ["TimeOfOccurrence"] = "timestamp"
            }
        };

        var digest = EventProcessingContractBuilder.BuildFromModel(model);
        Assert.Equal(1, digest.SchemaVersion);
        Assert.Equal("profileid", digest.AccountLink.SymbolPath);
        Assert.Equal("f00df00d-dead-f00d-f00d-ea7f00d1337e", digest.WrapperModelId);
        Assert.Single(digest.NaturalKey.Symbols);
        Assert.Equal("orderid", digest.NaturalKey.Symbols[0]);
        Assert.Equal("timestamp", digest.TimeOfOccurrenceSymbol);
        Assert.True(digest.IsProcessEventEligible);
        Assert.Equal(EventModelEligibilityValidation.RoleStandard, digest.ProcessingRole);
        Assert.False(digest.IsLoyaltyAccountCreationEvent);
        Assert.Empty(digest.Warnings);
    }

    [Fact]
    public void BuildFromModel_loyalty_account_details_eligibility()
    {
        var model = new ModelDto
        {
            ID = "c55fcdab-4dec-42d7-a746-941496c8f01f",
            Name = "LoyaltyAccountDetails",
            ModelType = "loyalty",
            Tag = "eventable",
            Attributes =
            [
                new ModelAttributePrimitiveDto { Symbol = "extaccountid", DataType = "string", DisplayName = "Ext", Status = "Live" },
                new ModelAttributePrimitiveDto { Symbol = "type", DataType = "string", DisplayName = "Type", Status = "Live" },
                new ModelAttributePrimitiveDto { Symbol = "timestamp", DataType = "datetime", DisplayName = "Ts", Status = "Live" }
            ],
            ModelMetaData = new Dictionary<string, string>
            {
                ["Wrapper"] = "a79bcc89-07cd-4ae9-87e9-95c45aa48819",
                ["NaturalKeySymbols"] = "[\"type\", \"extaccountid\"]",
                ["IsLoyaltyAccount"] = "true",
                ["AccountXIdSymbol"] = "extaccountid",
                ["TimeOfOccurrence"] = "timestamp",
                ["ProcessingType"] = "Engine"
            }
        };

        var digest = EventProcessingContractBuilder.BuildFromModel(model);
        Assert.True(digest.IsProcessEventEligible);
        Assert.True(digest.IsLoyaltyAccountCreationEvent);
        Assert.Equal(EventModelEligibilityValidation.RoleLoyaltyAccountCreation, digest.ProcessingRole);
        Assert.Contains(digest.Warnings, w => w.Contains("Verification", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildFromModel_missing_eventable_tag_adds_warning()
    {
        var model = new ModelDto
        {
            ID = "m1",
            Name = "Order",
            ModelMetaData = new Dictionary<string, string>
            {
                ["Wrapper"] = "w1",
                ["NaturalKeySymbols"] = "[\"orderid\"]",
                ["AccountXIdSymbol"] = "orderid"
            },
            Attributes = [new ModelAttributePrimitiveDto { Symbol = "orderid", DataType = "string", DisplayName = "Id" }]
        };

        var digest = EventProcessingContractBuilder.BuildFromModel(model);
        Assert.False(digest.IsProcessEventEligible);
        Assert.Contains(digest.Warnings, w => w.Contains("eventable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildFromModel_missing_wrapper_adds_warning()
    {
        var model = new ModelDto
        {
            ID = "m1",
            Name = "Order",
            ModelMetaData = new Dictionary<string, string>
            {
                ["NaturalKeySymbols"] = "[\"orderid\"]",
                ["AccountXIdSymbol"] = "orderid"
            },
            Attributes = [new ModelAttributePrimitiveDto { Symbol = "orderid", DataType = "string", DisplayName = "Id" }]
        };

        var digest = EventProcessingContractBuilder.BuildFromModel(model);
        Assert.Contains(digest.Warnings, w => w.Contains("Wrapper", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TryBuildFromToolResult_parses_save_model_json()
    {
        var model = new ModelDto
        {
            ID = "m1",
            Name = "Order",
            ModelMetaData = new Dictionary<string, string>
            {
                ["Wrapper"] = "w1",
                ["NaturalKeySymbols"] = "[\"orderid\"]",
                ["AccountXIdSymbol"] = "orderid"
            },
            Attributes = [new ModelAttributePrimitiveDto { Symbol = "orderid", DataType = "string", DisplayName = "Id" }]
        };
        var json = JsonSerializer.Serialize(model);

        var ok = EventProcessingContractBuilder.TryBuildFromToolResult(json, out var digestJson);
        Assert.True(ok);
        Assert.Contains("accountLink", digestJson!, StringComparison.Ordinal);
        Assert.Contains("schemaVersion", digestJson!, StringComparison.Ordinal);
    }

    [Fact]
    public void TryBuildFromToolResult_false_on_garbage()
    {
        Assert.False(EventProcessingContractBuilder.TryBuildFromToolResult("""{"errors":{"x":"y"}}""", out _));
    }

    [Fact]
    public void TryBuildFromToolResult_parses_double_encoded_string_result()
    {
        var model = new ModelDto
        {
            ID = "m1",
            Name = "Order",
            ModelMetaData = new Dictionary<string, string>
            {
                ["Wrapper"] = "w1",
                ["NaturalKeySymbols"] = "[\"orderid\"]",
                ["AccountXIdSymbol"] = "orderid"
            },
            Attributes = [new ModelAttributePrimitiveDto { Symbol = "orderid", DataType = "string", DisplayName = "Id" }]
        };
        // Simulate an MCP transport that returns the model JSON wrapped as a JSON string.
        var doubleEncoded = JsonSerializer.Serialize(JsonSerializer.Serialize(model));

        var ok = EventProcessingContractBuilder.TryBuildFromToolResult(doubleEncoded, out var digestJson);
        Assert.True(ok);
        Assert.Contains("accountLink", digestJson!, StringComparison.Ordinal);
    }

    [Fact]
    public void TryBuildFromToolResult_parses_meai_text_envelope_result()
    {
        var model = new ModelDto
        {
            ID = "a17daa79-8908-4e89-9fa2-6e5f629a2202",
            Name = "Order",
            Tag = "eventable",
            ModelMetaData = new Dictionary<string, string>
            {
                ["Wrapper"] = "8416a802-7537-4ec8-b214-51ed5961fffe",
                ["NaturalKeySymbols"] = "[\"orderid\"]",
                ["AccountXIdSymbol"] = "customerid",
                ["TimeOfOccurrence"] = "orderdate"
            },
            Attributes = [new ModelAttributePrimitiveDto { Symbol = "orderid", DataType = "string", DisplayName = "Id", Status = "Live" }]
        };
        // Reproduces the get_model result shape from the live transcript: a MEAI TextContent envelope
        // whose "text" property holds the real model JSON.
        var envelope = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["$type"] = "text",
            ["text"] = JsonSerializer.Serialize(model)
        });

        var ok = EventProcessingContractBuilder.TryBuildFromToolResult(envelope, out var digestJson);
        Assert.True(ok);
        Assert.Contains("accountLink", digestJson!, StringComparison.Ordinal);
    }
}
