using CampaignContextAudit.Analysis;
using CampaignContextAudit.Models;
using Xunit;

namespace CampaignContextAudit.Tests;

public class ToolCallTimelineTests
{
    [Fact]
    public void Joins_call_to_tool_row_result_chars_without_double_counting()
    {
        var envelope = """{"v":1,"meai":true,"role":"assistant","contents":[{"kind":"functionCall","callId":"c1","name":"get_all_models","arguments":{}}]}""";
        var rows = new List<AgentMessageDoc>
        {
            new() { Sequence = 0, Role = "user", Content = "hi" },
            new() { Sequence = 1, Role = "assistant", Content = envelope },
            new() { Sequence = 2, Role = "tool", ToolCallId = "c1", ToolResultJson = new string('x', 5000) },
        };

        var timeline = ToolCallTimeline.Build(rows);

        Assert.Single(timeline);
        Assert.Equal("get_all_models", timeline[0].ToolName);
        Assert.Equal(1, timeline[0].Sequence);
        Assert.Equal(5000, timeline[0].ResultChars);
    }
}
