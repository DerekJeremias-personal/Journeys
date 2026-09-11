using Journeys.API.CampaignAgent;
using Journeys.Core.Models;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public class CampaignAgentTokenBudgetTests
{
    [Fact]
    public void Apply_pins_first_user_segment_when_over_char_budget()
    {
        var messages = new List<AgentMessage>
        {
            Msg(1, "user", "Opening business intent: milestone thresholds 500/1000/2000"),
            Msg(2, "assistant", "ack"),
            Msg(3, "user", "proceed"),
            Msg(4, "assistant", new string('x', 90_000)),
        };

        var result = CampaignAgentTokenBudget.Apply(messages, pinFirstUserSegment: true, maxChars: 80_000);

        Assert.True(CampaignAgentHistoryCharCounter.CountEffective(result) <= 80_000);
        Assert.Contains(result, m => m.Sequence == 1);
        Assert.Contains(result, m => m.Sequence == 3);
        var asst = Assert.Single(result, m => m.Sequence == 4);
        Assert.Contains("historyTextTrimmed", asst.Content!, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_removes_first_segment_when_pin_disabled()
    {
        var big = new string('x', 85_000);
        var messages = new List<AgentMessage>
        {
            Msg(1, "user", "Opening intent"),
            ToolMsg(2, "validate_campaign", big),
            Msg(3, "user", "proceed"),
            Msg(4, "assistant", "ok"),
        };

        var result = CampaignAgentTokenBudget.Apply(messages, new HistoryBudgetOptions
        {
            PinFirstUserSegment = false,
            ShrinkToolResults = false,
            MaxChars = 80_000
        });

        Assert.DoesNotContain(result, m => m.Sequence == 1);
        Assert.Contains(result, m => m.Sequence == 3);
    }

    private static AgentMessage Msg(long seq, string role, string content) =>
        new("t", "u", "c", seq, role, content, null, null, null, null, null);

    private static AgentMessage ToolMsg(long seq, string toolName, string toolResult) =>
        new("t", "u", "c", seq, "tool", string.Empty, null, null, toolName, null, toolResult);

    [Fact]
    public void Apply_shrinks_tool_rows_before_dropping_recent_segment()
    {
        var big = new string('x', 85_000);
        var messages = new List<AgentMessage>
        {
            Msg(1, "user", "Opening business intent"),
            Msg(2, "assistant", "ack"),
            Msg(3, "user", "continue to journey"),
            ToolMsg(4, "validate_campaign", big),
            Msg(5, "user", "build journey"),
        };

        var result = CampaignAgentTokenBudget.Apply(messages, new HistoryBudgetOptions
        {
            PinFirstUserSegment = true,
            PinMostRecentCompletedSegment = true,
            MaxChars = 80_000
        });

        Assert.Contains(result, m => m.Sequence == 1);
        Assert.Contains(result, m => m.Sequence == 3);
        Assert.Contains(result, m => m.Sequence == 5);
        var tool = Assert.Single(result, m => m.Sequence == 4);
        Assert.Contains("historyStub", tool.ToolResultJson!, StringComparison.Ordinal);
        Assert.DoesNotContain(result, m => m.Sequence == 2 && (m.Content?.Length ?? 0) > 1000);
    }

    [Fact]
    public void GetProtectedSegmentIndices_pins_first_recent_and_current_when_three_segments()
    {
        var options = new HistoryBudgetOptions { PinFirstUserSegment = true, PinMostRecentCompletedSegment = true };
        var protectedIdx = CampaignAgentTokenBudget.GetProtectedSegmentIndices(3, options);
        Assert.Equal(3, protectedIdx.Count);
        Assert.Contains(0, protectedIdx);
        Assert.Contains(1, protectedIdx);
        Assert.Contains(2, protectedIdx);
    }

    [Fact]
    public void GetProtectedSegmentIndices_two_segments_pins_first_only()
    {
        var options = new HistoryBudgetOptions { PinFirstUserSegment = true, PinMostRecentCompletedSegment = true };
        var protectedIdx = CampaignAgentTokenBudget.GetProtectedSegmentIndices(2, options);
        Assert.Single(protectedIdx);
        Assert.Contains(0, protectedIdx);
    }

    [Fact]
    public void Apply_shrinks_embedded_meai_before_dropping_when_all_segments_pinned()
    {
        var big = new string('x', 85_000);
        var envelope =
            "{\"v\":1,\"meai\":true,\"role\":\"assistant\",\"contents\":[" +
            "{\"kind\":\"functionCall\",\"callId\":\"c1\",\"name\":\"validate_campaign\",\"arguments\":{}}," +
            "{\"kind\":\"functionResult\",\"callId\":\"c1\",\"result\":{\"payload\":\"" + big + "\"}}" +
            "]}";
        var messages = new List<AgentMessage>
        {
            Msg(1, "user", "Opening business intent"),
            Msg(2, "assistant", "ack"),
            Msg(3, "user", "continue"),
            Msg(4, "assistant", envelope),
            Msg(5, "user", "build journey"),
        };

        var result = CampaignAgentTokenBudget.Apply(messages, new HistoryBudgetOptions
        {
            PinFirstUserSegment = true,
            PinMostRecentCompletedSegment = true,
            MaxChars = 80_000
        });

        Assert.True(CampaignAgentHistoryCharCounter.CountEffective(result) <= 80_000);
        var asst = Assert.Single(result, m => m.Sequence == 4);
        Assert.Contains("historyStub", asst.Content!, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_preserves_contract_summary_in_protected_segment_when_pin_active()
    {
        var contract = new string('c', 85_000);
        var messages = new List<AgentMessage>
        {
            Msg(1, "user", "Opening business intent"),
            ToolMsg(2, "get_rules_engine_contract_summary", contract),
            Msg(3, "user", "continue to journey"),
            ToolMsg(4, "validate_campaign", new string('x', 85_000)),
            Msg(5, "user", "build journey"),
        };

        var result = CampaignAgentTokenBudget.Apply(messages, new HistoryBudgetOptions
        {
            PinFirstUserSegment = true,
            PinMostRecentCompletedSegment = true,
            JourneyContractSummaryPinActive = true,
            MaxChars = 80_000
        });

        var contractRow = Assert.Single(result, m => m.Sequence == 2);
        Assert.DoesNotContain("historyStub", contractRow.ToolResultJson!, StringComparison.Ordinal);
        var validateRow = Assert.Single(result, m => m.Sequence == 4);
        Assert.Contains("historyStub", validateRow.ToolResultJson!, StringComparison.Ordinal);
    }
}
