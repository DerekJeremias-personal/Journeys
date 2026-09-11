using CampaignContextAudit.Analysis;
using CampaignContextAudit.Models;
using Xunit;

namespace CampaignContextAudit.Tests;

public class EffectiveWallMsResolverTests
{
    [Fact]
    public void SparseTs_usesGapSumOrToolDurations()
    {
        var rows = new List<AgentMessageDoc>
        {
            new() { Sequence = 1, Role = "user", CosmosTimestamp = 100 },
            new() { Sequence = 2, Role = "assistant", CosmosTimestamp = 160 },
            new() { Sequence = 3, Role = "tool", ToolDurationMs = 50_000, CosmosTimestamp = 220 },
        };
        var performance = new PerformanceSummaryJson
        {
            DataSource = "_ts_inferred",
            TurnCount = 1,
            TotalWallMs = 0,
            ByCategory = new PerformanceByCategoryJson()
        };

        var wall = EffectiveWallMsResolver.Resolve(rows, performance);

        Assert.True(wall >= 120_000, $"expected gap sum >= 120000, got {wall}");
    }

    [Fact]
    public void TurnMetricsJson_preferredWhenPresent()
    {
        var rows = new List<AgentMessageDoc>
        {
            new() { Sequence = 0, Role = "workflow", TurnMetricsJson = """{"totalMs":480000}""" },
            new() { Sequence = 1, Role = "user", CosmosTimestamp = 100 },
            new() { Sequence = 2, Role = "assistant", CosmosTimestamp = 200 },
        };
        var performance = new PerformanceSummaryJson
        {
            DataSource = "persisted",
            TurnCount = 1,
            TotalWallMs = 100_000,
            ByCategory = new PerformanceByCategoryJson()
        };

        var wall = EffectiveWallMsResolver.Resolve(rows, performance);

        Assert.Equal(480_000, wall);
    }
}
