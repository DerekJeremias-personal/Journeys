using System.Text.Json;
using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Utility;

public class ToolResultJsonNormalizerTests
{
    [Fact]
    public void Unwrap_object_passthrough()
    {
        const string json = """{"id":"m1","name":"Order"}""";
        Assert.Equal(json, ToolResultJsonNormalizer.Unwrap(json));
    }

    [Fact]
    public void Unwrap_double_encoded_object_is_unwrapped()
    {
        const string inner = """{"id":"m1","name":"Order"}""";
        var doubleEncoded = JsonSerializer.Serialize(inner);

        var result = ToolResultJsonNormalizer.Unwrap(doubleEncoded);

        using var doc = JsonDocument.Parse(result!);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        Assert.Equal("m1", doc.RootElement.GetProperty("id").GetString());
    }

    [Fact]
    public void Unwrap_double_encoded_array_is_unwrapped()
    {
        const string inner = """[{"id":"a"},{"id":"b"}]""";
        var doubleEncoded = JsonSerializer.Serialize(inner);

        var result = ToolResultJsonNormalizer.Unwrap(doubleEncoded);

        using var doc = JsonDocument.Parse(result!);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(2, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public void Unwrap_meai_text_envelope_is_unwrapped()
    {
        const string inner = """{"id":"m1","name":"Order"}""";
        // MEAI TextContent serializes with a $type discriminator and the payload under "text".
        var envelope = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["$type"] = "text",
            ["text"] = inner
        });

        var result = ToolResultJsonNormalizer.Unwrap(envelope);

        using var doc = JsonDocument.Parse(result!);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        Assert.Equal("Order", doc.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public void Unwrap_meai_text_envelope_array_is_concatenated_and_unwrapped()
    {
        const string inner = """{"id":"m1"}""";
        var envelopeArray = JsonSerializer.Serialize(new[]
        {
            new Dictionary<string, object?> { ["$type"] = "text", ["text"] = inner }
        });

        var result = ToolResultJsonNormalizer.Unwrap(envelopeArray);

        using var doc = JsonDocument.Parse(result!);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        Assert.Equal("m1", doc.RootElement.GetProperty("id").GetString());
    }

    [Fact]
    public void Unwrap_plain_string_left_unchanged()
    {
        var json = JsonSerializer.Serialize("just a message");
        Assert.Equal(json, ToolResultJsonNormalizer.Unwrap(json));
    }

    [Fact]
    public void Unwrap_real_object_with_text_property_not_envelope()
    {
        // A genuine model attribute named "text" must NOT be treated as an envelope (no $type:text).
        const string json = """{"id":"m1","text":"some description"}""";
        Assert.Equal(json, ToolResultJsonNormalizer.Unwrap(json));
    }

    [Fact]
    public void Unwrap_null_or_empty_passthrough()
    {
        Assert.Null(ToolResultJsonNormalizer.Unwrap(null));
        Assert.Equal("   ", ToolResultJsonNormalizer.Unwrap("   "));
    }
}
