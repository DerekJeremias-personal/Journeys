using Journeys.API.CampaignAgent;
using Journeys.Core.Models;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public class CampaignAgentHistoryCharCounterTests
{
    [Fact]
    public void CountEffective_dedupes_embedded_result_when_tool_row_exists()
    {
        var big = new string('z', 20_000);
        var envelope =
            "{\"v\":1,\"meai\":true,\"role\":\"assistant\",\"contents\":[" +
            "{\"kind\":\"functionResult\",\"callId\":\"c1\",\"result\":{\"payload\":\"" + big + "\"}}" +
            "]}";
        var messages = new List<AgentMessage>
        {
            Msg(1, "user", "hi"),
            Msg(2, "assistant", envelope),
            Tool(3, "c1", "validate_campaign", big),
        };

        var effective = CampaignAgentHistoryCharCounter.CountEffective(messages);
        var naive = messages.Sum(m => (m.Content?.Length ?? 0) + (m.ToolResultJson?.Length ?? 0));

        Assert.Equal(2 + big.Length, effective);
        Assert.True(naive > effective + 15_000);
    }

    private static AgentMessage Msg(long seq, string role, string content) =>
        new("t", "u", "c", seq, role, content, null, null, null, null, null);

    private static AgentMessage Tool(long seq, string callId, string toolName, string result) =>
        new("t", "u", "c", seq, "tool", string.Empty, null, callId, toolName, null, result);
}
