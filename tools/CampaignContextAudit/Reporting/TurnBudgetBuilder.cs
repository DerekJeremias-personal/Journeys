using CampaignContextAudit.Budget;
using CampaignContextAudit.Models;
using Journeys.Core.Models;

namespace CampaignContextAudit.Reporting;

public sealed record TurnBudget(
    long UserSequence, string Phase,
    int StableChars, int HistoryChars, int SessionChars, int TotalChars,
    int HistoryUserTurns, int DroppedSegmentCount, int DroppedChars, int ApproxTotalTokens);

public static class TurnBudgetBuilder
{
    // ~4 chars/token is a rough heuristic; report labels it as an estimate.
    public static int ApproxTokens(int chars) => (int)Math.Round(chars / 4.0);

    public static IReadOnlyList<TurnBudget> Build(
        IReadOnlyList<AgentMessageDoc> chatRows,
        IReadOnlyDictionary<long, string> phaseByUserTurn,
        Func<string?, StableSizes> stableForPhase,
        CampaignWorkflowState? workflowState = null,
        string? tenantSliceText = null)
    {
        var ordered = chatRows.OrderBy(m => m.Sequence).ToList();
        var userSeqs = ordered.Where(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Sequence).ToList();

        var turns = new List<TurnBudget>();
        foreach (var seq in userSeqs)
        {
            var historyInput = ordered.Where(m => m.Sequence <= seq).ToList();
            var budget = HistoryBudgeter.Apply(historyInput);
            var phase = phaseByUserTurn.TryGetValue(seq, out var p) ? p : "DataAnalysis";
            var stable = stableForPhase(phase);
            var droppedChars = budget.DroppedSegments.Sum(d => d.CharCount);
            var sessionChars = workflowState is null
                ? 0
                : SessionBudgetEstimator.EstimateSessionChars(workflowState, budget.Kept, tenantSliceText);
            var totalChars = stable.TotalStableChars + budget.FinalChars + sessionChars;
            turns.Add(new TurnBudget(
                seq, phase, stable.TotalStableChars, budget.FinalChars, sessionChars, totalChars,
                budget.FinalUserTurns, budget.DroppedSegments.Count, droppedChars,
                ApproxTokens(totalChars)));
        }
        return turns;
    }
}
