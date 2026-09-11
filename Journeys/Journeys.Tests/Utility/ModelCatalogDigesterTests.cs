using System.Text;
using System.Text.Json;
using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Utility;

public class ModelCatalogDigesterTests
{
    private static string BuildModel(string id, string name, string modelType, string? tag, bool isContainer, int attributeCount = 2)
    {
        var attrs = new StringBuilder();
        attrs.Append('[');
        for (var i = 0; i < attributeCount; i++)
        {
            if (i > 0) attrs.Append(',');
            attrs.Append($"{{\"name\":\"attr_{i}\",\"dataType\":\"string\",\"isRequired\":false,\"description\":\"attribute number {i} on {name}\"}}");
        }
        attrs.Append(']');

        var tagJson = tag is null ? "null" : $"\"{tag}\"";
        return $$"""
            {
              "id": "{{id}}",
              "name": "{{name}}",
              "modelType": "{{modelType}}",
              "tag": {{tagJson}},
              "isContainer": {{(isContainer ? "true" : "false")}},
              "attributes": {{attrs}}
            }
            """;
    }

    private static string BuildEventableOnlyCatalog()
    {
        var lad = BuildModel("lad-1", "LoyaltyAccountDetails", "System", EventModelEligibilityValidation.EventableTag, false);
        var order = BuildModel("order-1", "Order", "Custom", EventModelEligibilityValidation.EventableTag, false);
        return $"{{ \"models\": [ {lad}, {order} ] }}";
    }

    [Fact]
    public void Digest_slims_all_models_in_eventable_only_catalog()
    {
        var raw = BuildEventableOnlyCatalog();

        var result = ModelCatalogDigester.Digest(raw, sourceTag: "eventable");

        Assert.True(result.Transformed);
        Assert.Equal(2, result.ModelCount);
        Assert.Equal(2, result.RetainedCount);
        Assert.True(result.DigestChars < result.OriginalChars);

        using var doc = JsonDocument.Parse(result.Json);
        var root = doc.RootElement;

        Assert.False(root.TryGetProperty("catalog", out _));
        Assert.False(root.TryGetProperty("retainedModels", out _));

        var models = root.GetProperty("models");
        Assert.Equal(2, models.GetArrayLength());

        foreach (var model in models.EnumerateArray())
        {
            Assert.True(model.TryGetProperty("id", out _));
            Assert.True(model.TryGetProperty("attributes", out var attrs));
            Assert.True(attrs.GetArrayLength() > 0);
            var firstAttr = attrs[0];
            Assert.True(firstAttr.TryGetProperty("name", out _) || firstAttr.TryGetProperty("symbol", out _));
            Assert.False(firstAttr.TryGetProperty("description", out _));
        }

        var summary = root.GetProperty("summary");
        Assert.Equal(2, summary.GetProperty("total").GetInt32());
        Assert.Equal("eventable", summary.GetProperty("tag").GetString());

        Assert.True(root.TryGetProperty("note", out var note));
        Assert.Contains("slimmed", note.GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Digest_handles_bare_top_level_array_shape()
    {
        var order = BuildModel("order-1", "Order", "Custom", EventModelEligibilityValidation.EventableTag, false);
        var raw = $"[ {order} ]";

        var result = ModelCatalogDigester.Digest(raw);

        Assert.True(result.Transformed);

        using var doc = JsonDocument.Parse(result.Json);
        Assert.Equal(1, doc.RootElement.GetProperty("summary").GetProperty("total").GetInt32());
        Assert.Equal(1, doc.RootElement.GetProperty("models").GetArrayLength());
    }

    [Fact]
    public void Digest_recognizes_data_property_shape()
    {
        var order = BuildModel("order-1", "Order", "Custom", EventModelEligibilityValidation.EventableTag, false);
        var raw = $"{{ \"data\": [ {order} ] }}";

        var result = ModelCatalogDigester.Digest(raw);

        Assert.True(result.Transformed);

        using var doc = JsonDocument.Parse(result.Json);
        Assert.Equal(1, doc.RootElement.GetProperty("summary").GetProperty("total").GetInt32());
    }

    [Fact]
    public void Digest_handles_list_models_items_shape_and_preserves_paging()
    {
        var order = BuildModel("order-1", "Order", "Custom", EventModelEligibilityValidation.EventableTag, false);
        var raw = $"{{ \"ContinuationToken\": \"\", \"Count\": 1, \"IsLastPage\": true, \"Items\": [ {order} ] }}";

        var result = ModelCatalogDigester.Digest(raw);

        Assert.True(result.Transformed);

        using var doc = JsonDocument.Parse(result.Json);
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("summary").GetProperty("total").GetInt32());
        Assert.True(root.GetProperty("IsLastPage").GetBoolean());
        Assert.Equal(1, root.GetProperty("Count").GetInt32());
    }

    [Fact]
    public void Digest_unwraps_meai_text_envelope_and_rewraps()
    {
        var order = BuildModel("order-1", "Order", "Custom", EventModelEligibilityValidation.EventableTag, false);
        var inner = $"{{ \"models\": [ {order} ] }}";
        var envelope = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["$type"] = "text",
            ["text"] = inner
        });

        var result = ModelCatalogDigester.Digest(envelope);

        Assert.True(result.Transformed);

        using var doc = JsonDocument.Parse(result.Json);
        var root = doc.RootElement;
        Assert.Equal("text", root.GetProperty("$type").GetString());
        var innerText = root.GetProperty("text").GetString();
        using var innerDoc = JsonDocument.Parse(innerText!);
        Assert.Equal(1, innerDoc.RootElement.GetProperty("summary").GetProperty("total").GetInt32());
        Assert.Equal(1, innerDoc.RootElement.GetProperty("models").GetArrayLength());
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{ \"unexpected\": true }")]
    [InlineData("{ \"models\": \"not-an-array\" }")]
    public void Digest_returns_original_when_shape_unrecognized(string raw)
    {
        var result = ModelCatalogDigester.Digest(raw);

        Assert.False(result.Transformed);
        Assert.Equal(raw, result.Json);
    }

    [Fact]
    public void Digest_large_eventable_only_fixture_is_much_smaller_than_raw()
    {
        var sb = new StringBuilder("{\"models\":[");
        for (var i = 0; i < 3; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(BuildModel(
                $"order-{i}",
                $"Order{i}",
                "Custom",
                EventModelEligibilityValidation.EventableTag,
                false,
                attributeCount: 30));
        }
        sb.Append("]}");
        var raw = sb.ToString();

        var result = ModelCatalogDigester.Digest(raw, sourceTag: "eventable");

        Assert.True(result.Transformed);
        Assert.True(result.DigestChars < raw.Length / 2,
            $"Expected slim digest << raw; got {result.DigestChars} vs {raw.Length}");
    }
}
