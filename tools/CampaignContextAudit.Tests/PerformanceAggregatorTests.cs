using CampaignContextAudit.Analysis;
using CampaignContextAudit.Models;
using Xunit;

namespace CampaignContextAudit.Tests;

public class PerformanceAggregatorTests
{
    [Fact]
    public void Uses_persisted_turn_metrics_when_present()
    {
        var rows = new List<AgentMessageDoc>
        {
            new()
            {
                Role = "workflow",
                Sequence = 0,
                TurnMetricsJson = """
                {
                  "totalMs": 1000,
                  "spans": { "llmMs": 700, "toolsMs": 200, "persistMs": 50 },
                  "toolCalls": [{ "tool": "upsert_campaign", "durationMs": 200 }]
                }
                """
            }
        };

        var (summary, _) = PerformanceAggregator.Build(rows, [], slowToolMs: 10_000);

        Assert.Equal("persisted", summary.DataSource);
        Assert.Equal(700, summary.ByCategory.Llm);
        Assert.Equal(200, summary.ByCategory.Tools);
    }

    [Fact]
    public void Falls_back_to_ts_inferred_without_metrics_fields()
    {
        var rows = new List<AgentMessageDoc>
        {
            new() { Sequence = 1, Role = "user", Content = "hi", CosmosTimestamp = 100 },
            new() { Sequence = 2, Role = "assistant", Content = "{}", CosmosTimestamp = 105 },
            new() { Sequence = 3, Role = "tool", ToolResultJson = "{}", CosmosTimestamp = 120 }
        };

        var (summary, findings) = PerformanceAggregator.Build(rows, [], slowToolMs: 5_000);

        Assert.Equal("_ts_inferred", summary.DataSource);
        Assert.True(summary.ByCategory.Tools > 0);
        Assert.Contains(findings, f => f.Code == "SLOW_TOOL");
    }

    [Fact]
    public void Uses_tool_duration_ms_when_present_without_turn_metrics()
    {
        var rows = new List<AgentMessageDoc>
        {
            new() { Sequence = 1, Role = "user", CosmosTimestamp = 100 },
            new() { Sequence = 2, Role = "tool", ToolDurationMs = 1500, CosmosTimestamp = 102 }
        };

        var (summary, _) = PerformanceAggregator.Build(rows, [], slowToolMs: 10_000);

        Assert.Equal("partial_persisted", summary.DataSource);
        Assert.Equal(1500, summary.ByCategory.Tools);
    }
}
