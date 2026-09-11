using System.Text.Json;
using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Utility;

public class ModelReadDigesterTests
{
    [Fact]
    public void Digest_slims_verbose_model_attributes()
    {
        var raw = """
            {
              "id": "a6edbbc5-bf43-4c57-b2f1-e015b9efaf03",
              "name": "order",
              "modelType": "loyalty",
              "tag": "eventable",
              "modelMetaData": {
                "Wrapper": "wrapper-id",
                "NaturalKeySymbols": "[\"orderid\"]",
                "AccountXIdSymbol": "profileid"
              },
              "attributes": [
                {
                  "symbol": "orderid",
                  "dataType": "string",
                  "type": "Primitive",
                  "status": "Live",
                  "displayName": "Order id",
                  "description": "Long description that should not be persisted in digest output."
                },
                {
                  "symbol": "ordertotal",
                  "dataType": "decimal",
                  "type": "Primitive",
                  "status": "Live",
                  "displayName": "Order total"
                }
              ]
            }
            """;

        var result = ModelReadDigester.Digest(raw);

        Assert.True(result.Transformed);
        Assert.True(result.DigestChars < result.OriginalChars);
        using var doc = JsonDocument.Parse(result.Json);
        var root = doc.RootElement;
        Assert.Equal("order", root.GetProperty("name").GetString());
        Assert.True(root.TryGetProperty("modelMetaData", out _));
        Assert.Equal(2, root.GetProperty("attributeCount").GetInt32());
        Assert.DoesNotContain("displayName", result.Json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("description", result.Json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Digest_retains_modelId_and_element_type_on_nested_attributes()
    {
        var raw = """
            {
              "id": "w1",
              "name": "ReviewAndRuleState",
              "modelType": "loyalty",
              "isContainer": true,
              "attributes": [
                {
                  "symbol": "event",
                  "type": "ModelObject",
                  "dataType": "Object",
                  "modelId": "e1",
                  "modelType": "loyalty",
                  "status": "Live"
                },
                {
                  "symbol": "outcomestates",
                  "type": "ModelList",
                  "dataType": "List",
                  "listAttributeDataType": "Object",
                  "modelId": "o1",
                  "modelType": "loyalty",
                  "status": "Live"
                },
                {
                  "symbol": "providerstates",
                  "type": "ModelKeyValue",
                  "dataType": "KeyValue",
                  "keyValueAttributeDataType": "Object",
                  "modelId": "p1",
                  "modelType": "loyalty",
                  "status": "Live"
                }
              ]
            }
            """;

        var result = ModelReadDigester.Digest(raw);

        Assert.True(result.Transformed);
        Assert.Contains("\"modelId\":\"e1\"", result.Json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"listAttributeDataType\":\"Object\"", result.Json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"keyValueAttributeDataType\":\"Object\"", result.Json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Digest_passthrough_for_non_model_json()
    {
        var raw = """{"errors":{"model":"not found"}}""";
        var result = ModelReadDigester.Digest(raw);
        Assert.False(result.Transformed);
        Assert.Equal(raw, result.Json);
    }

    [Fact]
    public void DigestForVerification_caps_attributes_and_keeps_symbol_datatype_only()
    {
        var attrs = string.Join(',', Enumerable.Range(0, 30).Select(i =>
            $$"""{"symbol":"attr{{i}}","dataType":"string","displayName":"Verbose {{i}}","description":"noise"}"""));
        var raw = $$"""
            {
              "id": "m1",
              "name": "order",
              "modelType": "loyalty",
              "attributes": [{{attrs}}]
            }
            """;

        var result = ModelReadDigester.DigestForVerification(raw);

        Assert.True(result.Transformed);
        using var doc = JsonDocument.Parse(result.Json);
        var root = doc.RootElement;
        Assert.Equal(30, root.GetProperty("attributeCount").GetInt32());
        Assert.True(root.GetProperty("attributesTruncated").GetBoolean());
        Assert.Equal(ModelReadDigester.VerificationMaxAttributes, root.GetProperty("attributes").GetArrayLength());
        Assert.DoesNotContain("displayName", result.Json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("description", result.Json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DigestForCreation_large_order_model_under_900_chars()
    {
        var attrs = string.Join(',', Enumerable.Range(0, 30).Select(i =>
            $$"""{"symbol":"attr{{i}}","dataType":"string","type":"Primitive","displayName":"Verbose {{i}}","description":"noise"}"""));
        var raw = $$"""
            {
              "id": "a6edbbc5-bf43-4c57-b2f1-e015b9efaf03",
              "name": "order",
              "modelType": "loyalty",
              "tag": "eventable",
              "modelMetaData": { "Wrapper": "w1", "NaturalKeySymbols": "[\"orderid\"]" },
              "attributes": [{{attrs}}]
            }
            """;

        var result = ModelReadDigester.DigestForCreation(raw);

        Assert.True(result.Transformed);
        Assert.True(result.DigestChars <= ModelReadDigester.CreationMaxChars, $"got {result.DigestChars}");
        Assert.DoesNotContain("displayName", result.Json, StringComparison.OrdinalIgnoreCase);
    }
}
