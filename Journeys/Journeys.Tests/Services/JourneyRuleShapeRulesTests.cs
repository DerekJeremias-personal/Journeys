using System.Text.Json;
using Journeys.Core.Services;
using Xunit;

namespace Journeys.Tests.Services;

public class JourneyRuleShapeRulesTests
{
    [Fact]
    public void ValidateSingleRuleElement_rejects_array_at_ruleJsonElement_path()
    {
        using var doc = JsonDocument.Parse("""
            [
              { "Kind": "NumericPropertyRule" },
              { "Kind": "HistoricalRule" }
            ]
            """);

        var errors = new List<string>();
        JourneyRuleShapeRules.ValidateSingleRuleElement(
            doc.RootElement, "journey/rules[0]/ruleJsonElement", "ruleJsonElement", errors);

        Assert.Single(errors);
        Assert.Contains("JOURNEY_SHAPE_RULE_JSON_NOT_OBJECT", errors[0], StringComparison.Ordinal);
        Assert.Contains("path=journey/rules[0]/ruleJsonElement", errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateSingleRuleElement_allows_rule_object()
    {
        using var doc = JsonDocument.Parse("""{ "Kind": "AndRule", "Children": [] }""");

        var errors = new List<string>();
        JourneyRuleShapeRules.ValidateSingleRuleElement(
            doc.RootElement, "journey/rules[0]/ruleJsonElement", "ruleJsonElement", errors);

        Assert.Empty(errors);
    }

    [Fact]
    public void WalkRuleTreeShape_rejects_array_navConstraint()
    {
        using var doc = JsonDocument.Parse("""
            {
              "Kind": "SimpleRule",
              "navConstraint": [{ "Kind": "NumericPropertyRule" }]
            }
            """);

        var errors = new List<string>();
        JourneyRuleShapeRules.WalkRuleTreeShape(doc.RootElement, "journey/rules[0]/ruleJsonElement", errors);

        Assert.Contains(errors, e => e.Contains("navConstraint", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("JOURNEY_SHAPE_RULE_JSON_NOT_OBJECT", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateSingleRuleElement_rejects_string_at_ruleJsonElement_path()
    {
        using var doc = JsonDocument.Parse("""
            "{\"Kind\":\"NumericPropertyRule\",\"leftProvider\":{\"$type\":\"ConstantValueProvider\",\"value\":\"0\"}}"
            """);

        var errors = new List<string>();
        JourneyRuleShapeRules.ValidateSingleRuleElement(
            doc.RootElement, "journey/rules[0]/ruleJsonElement", "ruleJsonElement", errors);

        Assert.Single(errors);
        Assert.Contains(JourneyRuleShapeRules.StringEncodedViolationCode, errors[0], StringComparison.Ordinal);
        Assert.Contains("path=journey/rules[0]/ruleJsonElement", errors[0], StringComparison.Ordinal);
        Assert.Contains("not a JSON string", errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WalkRuleTreeShape_rejects_string_navConstraint()
    {
        using var doc = JsonDocument.Parse("""
            {
              "Kind": "SimpleRule",
              "navConstraint": "{\"Kind\":\"NumericPropertyRule\"}"
            }
            """);

        var errors = new List<string>();
        JourneyRuleShapeRules.WalkRuleTreeShape(doc.RootElement, "journey/rules[0]/ruleJsonElement", errors);

        Assert.Contains(errors, e =>
            e.Contains(JourneyRuleShapeRules.StringEncodedViolationCode, StringComparison.Ordinal)
            && e.Contains("navConstraint", StringComparison.OrdinalIgnoreCase));
    }
}
