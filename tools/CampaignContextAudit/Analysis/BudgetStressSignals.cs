using CampaignContextAudit.Reporting;

namespace CampaignContextAudit.Analysis;

public sealed record BudgetStressSignals(
    bool AnyEviction,
    bool HeavyEviction,
    bool NearBudget,
    int MaxHistoryChars,
    int TotalDroppedChars,
    int DroppedSegmentCount,
    int UserTurnCount);

public static class BudgetStressAnalyzer
{
    public static BudgetStressSignals Analyze(IReadOnlyList<TurnBudget> turns)
    {
        if (turns.Count == 0)
            return new BudgetStressSignals(false, false, false, 0, 0, 0, 0);

        var maxHistory = turns.Max(t => t.HistoryChars);
        var droppedChars = turns.Sum(t => t.DroppedChars);
        var droppedSegs = turns.Sum(t => t.DroppedSegmentCount);
        var anyEviction = turns.Any(t => t.DroppedSegmentCount > 0);
        var heavyEviction = turns.Any(t => t.DroppedChars > 80_000);
        var nearBudget = !anyEviction && maxHistory >= 60_000;

        return new BudgetStressSignals(
            anyEviction,
            heavyEviction,
            nearBudget,
            maxHistory,
            droppedChars,
            droppedSegs,
            turns.Count);
    }
}
