using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Journeys.API.Mcp;
using Journeys.CampaignAgent.Remediation;
using Journeys.Core.RulesEngine;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public class RulePatternRecipesDigesterTests
{
    private static readonly JsonSerializerOptions RecipeOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void Digest_pattern_recipes_shrinks_and_retains_ids()
    {
        var raw = JsonSerializer.Serialize(RulesEnginePatternRecipes.Build(), RecipeOptions);
        var result = RulePatternRecipesDigester.Digest(raw);

        Assert.True(result.Transformed);
        Assert.True(result.DigestChars < result.OriginalChars);

        using var doc = JsonDocument.Parse(result.Json);
        var root = doc.RootElement;

        Assert.Equal("2026-06-16", root.GetProperty("schemaVersion").GetString());
        Assert.True(root.TryGetProperty("patterns", out var patterns) && patterns.GetArrayLength() == 6);
        Assert.False(patterns[0].TryGetProperty("minimalSkeleton", out _));
        Assert.True(patterns[0].TryGetProperty("whenToUse", out _));
        Assert.True(root.TryGetProperty("ruleSemantics", out var semantics) && semantics.GetArrayLength() > 0);
    }

    [Fact]
    public void Digest_with_full_skeleton_pattern_id_retains_minimal_skeleton()
    {
        var raw = JsonSerializer.Serialize(RulesEnginePatternRecipes.Build(), RecipeOptions);
        var result = RulePatternRecipesDigester.Digest(raw, "tier-navigation-point-balance");

        Assert.True(result.Transformed);
        using var doc = JsonDocument.Parse(result.Json);
        var patterns = doc.RootElement.GetProperty("patterns");
        Assert.Equal(1, patterns.GetArrayLength());
        Assert.True(patterns[0].TryGetProperty("minimalSkeleton", out _));
        Assert.Equal("tier-navigation-point-balance", patterns[0].GetProperty("id").GetString());
    }
}
