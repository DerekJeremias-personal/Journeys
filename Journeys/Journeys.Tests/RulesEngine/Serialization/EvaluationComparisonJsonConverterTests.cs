using System.Text.Json;
using System.Text.Json.Serialization;
using Journeys.Core.JsonConverters;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine;
using Journeys.Core.RulesEngine.Comparitors;
using Journeys.Core.RulesEngine.Comparitors.Enums;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.Services;
using Xunit;

namespace Journeys.Tests.RulesEngine.Serialization;

public class EvaluationComparisonJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Theory]
    [InlineData("EvalType")]
    [InlineData("evalType")]
    public void Materialize_maps_legacy_evalType_alias_to_comparison(string aliasProperty)
    {
        var ruleJson = $$"""
            {
              "Kind": "NumericPropertyRule",
              "LeftProvider": { "$type": "PathValueProvider", "PropertyPath": "event.amount" },
              "RightProvider": { "$type": "ConstantValueProvider", "ConstantValue": 0 },
              "Evaluator": { "$type": "NumericEvaluation", "{{aliasProperty}}": "GreaterThanOrEqual" }
            }
            """;

        using var ruleDoc = JsonDocument.Parse(ruleJson);
        var ruleSet = new RuleSet("earn", ruleDoc.RootElement.Clone(), "NumericPropertyRule", null, null);
        var journey = new Journeys.Core.RulesEngine.Journey.JourneyNode(
            "root",
            new List<RuleSet> { ruleSet },
            "j1",
            "j1",
            null,
            null);
        var campaign = new Campaign(
            "ext",
            CampaignStatusStrings.Draft,
            "n",
            null,
            DateTimeOffset.UtcNow,
            null,
            null,
            journey,
            "t1",
            "c1");

        CampaignJourneyMaterializer.Materialize(campaign);

        var rule = (NumericPropertyRule)ruleSet.RuleTree!;
        var numeric = Assert.IsType<NumericEvaluation>(rule.Evaluator);
        Assert.Equal(NumEvalType.GreaterThanOrEqual, numeric.Comparison);
    }

    [Fact]
    public void Normalizer_copies_evalType_to_comparison_when_missing()
    {
        using var doc = JsonDocument.Parse("""
            { "$type": "NumericEvaluation", "EvalType": "GreaterThan" }
            """);

        var normalized = EvaluationComparisonJsonNormalizer.Normalize(doc.RootElement);

        Assert.True(normalized.TryGetProperty("comparison", out var comparison));
        Assert.Equal("GreaterThan", comparison.GetString());
    }
}
