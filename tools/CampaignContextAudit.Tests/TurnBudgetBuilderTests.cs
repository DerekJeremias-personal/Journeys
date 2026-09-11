using CampaignContextAudit.Budget;
using CampaignContextAudit.Models;
using CampaignContextAudit.Reporting;
using Xunit;

namespace CampaignContextAudit.Tests;

public class TurnBudgetBuilderTests
{
    private static AgentMessageDoc Row(long seq, string role, string? content = null, string? toolResult = null, string? phase = null)
        => new() { Sequence = seq, Role = role, Content = content, ToolResultJson = toolResult, WorkflowPhase = phase };

    [Fact]
    public void Builds_one_row_per_user_turn_with_running_history_chars()
    {
        var rows = new List<AgentMessageDoc>
        {
            Row(0, "user", "first"),
            Row(1, "assistant", "{}"),
            Row(2, "user", "second"),
        };
        var stable = new StableSizes(10, 20, 30, 40, "WorkflowPhaseEventModelsGovernance.txt");
        var phases = new Dictionary<long, string> { [0] = "EventModels", [2] = "EventModels" };
        var turns = TurnBudgetBuilder.Build(rows, phases, _ => stable);

        Assert.Equal(2, turns.Count);
        Assert.Equal(0, turns[0].UserSequence);
        Assert.True(turns[1].HistoryChars >= turns[0].HistoryChars);
        Assert.Equal(100, turns[0].StableChars);
        Assert.Equal("EventModels", turns[0].Phase);
    }
}
