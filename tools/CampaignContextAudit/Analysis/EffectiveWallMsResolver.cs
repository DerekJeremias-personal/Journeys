using System.Text.Json;
using CampaignContextAudit.Models;

namespace CampaignContextAudit.Analysis;

public static class EffectiveWallMsResolver
{
    public static long Resolve(
        IReadOnlyList<AgentMessageDoc> allRows,
        PerformanceSummaryJson? performance)
    {
        var candidates = new List<long>();
        if (performance?.TotalWallMs > 0)
            candidates.Add(performance.TotalWallMs);

        candidates.Add(EstimateTsSpan(allRows));
        candidates.Add(SumPositiveTsGaps(allRows));
        candidates.Add(ReadTurnMetricsTotalMs(allRows));
        candidates.Add(SumToolDurations(allRows));

        return candidates.Count == 0 ? 0 : candidates.Max();
    }

    private static long EstimateTsSpan(IReadOnlyList<AgentMessageDoc> rows)
    {
        var withTs = rows.Where(r => r.CosmosTimestamp is > 0).OrderBy(r => r.Sequence).ToList();
        if (withTs.Count < 2) return 0;
        return ((withTs[^1].CosmosTimestamp ?? 0) - (withTs[0].CosmosTimestamp ?? 0)) * 1000L;
    }

    private static long SumPositiveTsGaps(IReadOnlyList<AgentMessageDoc> rows)
    {
        var ordered = rows.Where(r => r.CosmosTimestamp is > 0).OrderBy(r => r.Sequence).ToList();
        long sum = 0;
        for (var i = 1; i < ordered.Count; i++)
        {
            var gap = ((ordered[i].CosmosTimestamp ?? 0) - (ordered[i - 1].CosmosTimestamp ?? 0)) * 1000L;
            if (gap > 0) sum += gap;
        }
        return sum;
    }

    private static long ReadTurnMetricsTotalMs(IReadOnlyList<AgentMessageDoc> rows)
    {
        var workflow = rows.FirstOrDefault(r => r.IsWorkflowRow);
        if (string.IsNullOrWhiteSpace(workflow?.TurnMetricsJson)) return 0;
        try
        {
            using var doc = JsonDocument.Parse(workflow.TurnMetricsJson);
            if (doc.RootElement.TryGetProperty("totalMs", out var total)
                && total.TryGetInt64(out var ms))
                return ms;
        }
        catch (JsonException)
        {
            // ignore malformed metrics
        }
        return 0;
    }

    private static long SumToolDurations(IReadOnlyList<AgentMessageDoc> rows) =>
        rows.Where(r => string.Equals(r.Role, "tool", StringComparison.OrdinalIgnoreCase))
            .Sum(r => (long)(r.ToolDurationMs ?? 0));
}
