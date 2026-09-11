using CampaignContextAudit.Budget;
using CampaignContextAudit.Models;
using Xunit;

namespace CampaignContextAudit.Tests;

public class HistoryBudgeterTests
{
    [Fact]
    public void Over_char_budget_shrinks_before_dropping_pinned_recent_segment()
    {
        var big = new string('x', 85_000);
        var rows = new List<AgentMessageDoc>
        {
            Row(0, "user", "first"),
            Row(1, "assistant", null),
            Row(2, "user", "second"),
            Row(3, "tool", null, big, "validate_campaign"),
            Row(4, "user", "third"),
        };
        var result = HistoryBudgeter.Apply(rows);
        Assert.Contains(result.Kept, r => r.Sequence == 0);
        Assert.Contains(result.Kept, r => r.Sequence == 2);
        Assert.Contains(result.Kept, r => r.Sequence == 4);
        var tool = Assert.Single(result.Kept, r => r.Sequence == 3);
        Assert.Contains("historyStub", tool.ToolResultJson!, StringComparison.Ordinal);
        Assert.Empty(result.DroppedSegments);
    }

    [Fact]
    public void Over_char_budget_drops_oldest_user_segment_when_shrink_disabled()
    {
        var big = new string('x', 60000);
        var rows = new List<AgentMessageDoc>
        {
            Row(0, "user", "first"),
            Row(1, "assistant", null), Row(2, "tool", null, big),
            Row(3, "user", "second"),
            Row(4, "assistant", null), Row(5, "tool", null, big),
        };
        var result = HistoryBudgeter.Apply(rows, new HistoryBudgetSettings { ShrinkToolResults = false });
        Assert.Contains(result.Kept, r => r.Sequence == 0);
        Assert.DoesNotContain(result.Kept, r => r.Sequence == 3);
        Assert.Single(result.DroppedSegments);
    }

    private static AgentMessageDoc Row(long seq, string role, string? content = null, string? toolResult = null, string? toolName = null)
        => new() { Sequence = seq, Role = role, Content = content, ToolResultJson = toolResult, ToolName = toolName };

    [Fact]
    public void Under_budget_keeps_all_rows()
    {
        var rows = new List<AgentMessageDoc> { Row(0, "user", "hi"), Row(1, "assistant", "hello") };
        var result = HistoryBudgeter.Apply(rows);
        Assert.Equal(2, result.Kept.Count);
        Assert.Empty(result.DroppedSegments);
    }

    [Fact]
    public void Over_char_budget_shrinks_instead_of_dropping_when_one_tool_shrink_fits()
    {
        var big = new string('x', 60000);
        var rows = new List<AgentMessageDoc>
        {
            Row(0, "user", "first"),
            Row(1, "assistant", null), Row(2, "tool", null, big),
            Row(3, "user", "second"),
            Row(4, "assistant", null), Row(5, "tool", null, big),
        };
        var result = HistoryBudgeter.Apply(rows);
        Assert.Contains(result.Kept, r => r.Sequence == 0);
        Assert.Contains(result.Kept, r => r.Sequence == 3);
        Assert.Contains(result.Kept, r => r.ToolResultJson != null && r.ToolResultJson.Contains("historyStub", StringComparison.Ordinal));
        Assert.Empty(result.DroppedSegments);
    }

    [Fact]
    public void Leading_non_user_rows_are_trimmed_first()
    {
        var rows = new List<AgentMessageDoc> { Row(0, "assistant", "orphan"), Row(1, "user", "hi") };
        var result = HistoryBudgeter.Apply(rows);
        Assert.Equal("user", result.Kept[0].Role);
    }

    [Fact]
    public void Contract_summary_in_protected_segment_not_shrunk_when_pin_active()
    {
        var contract = new string('c', 85_000);
        var rows = new List<AgentMessageDoc>
        {
            Row(0, "user", "first"),
            Row(1, "tool", null, contract, "get_rules_engine_contract_summary"),
            Row(2, "user", "second"),
            Row(3, "tool", null, new string('x', 85_000), "validate_campaign"),
            Row(4, "user", "third"),
        };

        var result = HistoryBudgeter.Apply(rows, new HistoryBudgetSettings
        {
            JourneyContractSummaryPinActive = true
        });

        var contractRow = Assert.Single(result.Kept, r => r.Sequence == 1);
        Assert.DoesNotContain("historyStub", contractRow.ToolResultJson!, StringComparison.Ordinal);
        var validateRow = Assert.Single(result.Kept, r => r.Sequence == 3);
        Assert.Contains("historyStub", validateRow.ToolResultJson!, StringComparison.Ordinal);
    }
}
