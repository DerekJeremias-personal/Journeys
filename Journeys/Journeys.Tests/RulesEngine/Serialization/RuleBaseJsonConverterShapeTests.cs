using System.Text.Json;
using Journeys.Core.RulesEngine.Rules;
using Xunit;

namespace Journeys.Tests.RulesEngine.Serialization;

public class RuleBaseJsonConverterShapeTests
{
    [Fact]
    public void Deserialize_rule_array_throws_JsonException_not_InvalidOperationException()
    {
        const string json = """[{ "Kind": "NumericPropertyRule" }]""";

        var ex = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<RuleBase>(json));

        Assert.Contains("expected a JSON object", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Array", ex.Message, StringComparison.Ordinal);
        Assert.Contains("AndRule.Children", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_string_wrapped_rule_throws_JsonException_with_stringify_hint()
    {
        const string json = """
            "{\"Kind\":\"NumericPropertyRule\",\"leftProvider\":{\"$type\":\"ConstantValueProvider\",\"value\":\"0\"},\"rightProvider\":{\"$type\":\"ConstantValueProvider\",\"value\":\"0\"},\"evaluator\":{\"$type\":\"NumericEvaluation\",\"evalType\":\"GreaterThan\"}}"
            """;

        var ex = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<RuleBase>(json));

        Assert.Contains("expected a JSON object", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("String", ex.Message, StringComparison.Ordinal);
        Assert.Contains("stringified", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
