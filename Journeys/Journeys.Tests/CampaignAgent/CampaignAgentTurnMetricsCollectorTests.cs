using System.Text.Json;
using Journeys.API.CampaignAgent;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public sealed class CampaignAgentTurnMetricsCollectorTests
{
    [Fact]
    public void BuildJson_deserializes_with_spans_and_caps_tool_calls()
    {
        var collector = new CampaignAgentTurnMetricsCollector();
        collector.MarkLoadHistoryDone(10);
        collector.AddLlm(100);
        for (var i = 0; i < 55; i++)
        {
            collector.ToolStarted($"call-{i}", "upsert_campaign");
            collector.ToolFinished($"call-{i}", "upsert_campaign", "ok");
        }

        var json = collector.BuildJson("req-1");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(100, root.GetProperty("spans").GetProperty("llmMs").GetInt64());
        Assert.Equal(50, root.GetProperty("toolCalls").GetArrayLength());
        Assert.True(root.GetProperty("truncated").GetBoolean());
    }

    [Fact]
    public void ToolStarted_and_finished_records_duration()
    {
        var collector = new CampaignAgentTurnMetricsCollector();
        collector.ToolStarted("call-1", "save_model");
        Thread.Sleep(5);
        collector.ToolFinished("call-1", "save_model", "ok");

        Assert.True(collector.GetToolDurationMs("call-1") >= 0);
    }
}
