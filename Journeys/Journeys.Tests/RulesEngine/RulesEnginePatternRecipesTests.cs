using System.Linq;
using System.Text.Json;
using Journeys.Core.RulesEngine;
using Xunit;

namespace Journeys.Tests.RulesEngine;

public class RulesEnginePatternRecipesTests
{
    [Fact]
    public void Build_includes_all_six_patterns()
    {
        var dto = RulesEnginePatternRecipes.Build();

        Assert.Equal("2026-06-16", dto.SchemaVersion);
        Assert.Equal(6, dto.Patterns.Count);

        var ids = dto.Patterns.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("historical-spend-threshold", ids);
        Assert.Contains("tier-navigation-point-balance", ids);
        Assert.Contains("historical-taxonomy-filtered", ids);
        Assert.Contains("historical-event-count", ids);
        Assert.Contains("temporal-event-gate", ids);
        Assert.Contains("composite-qualification", ids);

        foreach (var pattern in dto.Patterns)
        {
            Assert.False(string.IsNullOrWhiteSpace(pattern.Title));
            Assert.False(string.IsNullOrWhiteSpace(pattern.WhenToUse));
            Assert.NotEmpty(pattern.AntiPatterns);
            Assert.Equal(JsonValueKind.Object, pattern.MinimalSkeleton.ValueKind);
        }
    }

    [Fact]
    public void BuildRuleSemantics_includes_HistoricalRule_dual_provider_guidance()
    {
        var semantics = RulesEnginePatternRecipes.BuildRuleSemantics();
        var historical = semantics.Single(s => s.Id == "HistoricalRule");

        Assert.Contains("historicalValueProvider", historical.RequiredProperties);
        Assert.Contains("aggregationValueProvider", historical.RecommendedProperties);
        Assert.Contains("SimpleCalculationProvider", string.Join(' ', historical.NestedRequirements));
        Assert.Contains("historical-spend-threshold", historical.RelatedPatterns);
    }
}
