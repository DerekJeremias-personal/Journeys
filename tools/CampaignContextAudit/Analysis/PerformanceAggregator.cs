using System.Text.Json;
using CampaignContextAudit.Analysis;
using CampaignContextAudit.Models;

namespace CampaignContextAudit.Analysis;

public static class PerformanceAggregator
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public const int DefaultLongTurnMs = 60_000;

    public static (PerformanceSummaryJson Summary, IReadOnlyList<Finding> ExtraFindings) Build(
        IReadOnlyList<AgentMessageDoc> allRows,
        IReadOnlyList<ToolEvent> timeline,
        int slowToolMs,
        int longTurnMs = DefaultLongTurnMs,
        long? effectiveWallMsOverride = null)
    {
        var workflow = allRows.FirstOrDefault(r => r.IsWorkflowRow);
        if (!string.IsNullOrWhiteSpace(workflow?.TurnMetricsJson))
        {
            var summary = ParsePersisted(workflow.TurnMetricsJson, "persisted");
            summary = ApplyWallOverride(summary, effectiveWallMsOverride);
            return (summary, EmitSlowAndLongFindings(allRows, timeline, summary, slowToolMs, longTurnMs));
        }

        var toolRows = allRows.Where(r =>
            string.Equals(r.Role, "tool", StringComparison.OrdinalIgnoreCase)
            && r.ToolDurationMs is > 0).ToList();
        if (toolRows.Count > 0)
        {
            var partial = InferFromToolDurations(allRows, timeline, toolRows, slowToolMs);
            partial = ApplyWallOverride(partial, effectiveWallMsOverride);
            return (partial, EmitSlowAndLongFindings(allRows, timeline, partial, slowToolMs, longTurnMs));
        }

        var inferred = InferFromTimestamps(allRows, timeline, slowToolMs);
        inferred = ApplyWallOverride(inferred, effectiveWallMsOverride);
        return (inferred, EmitSlowAndLongFindings(allRows, timeline, inferred, slowToolMs, longTurnMs));
    }

    private static PerformanceSummaryJson ApplyWallOverride(
        PerformanceSummaryJson summary,
        long? effectiveWallMsOverride)
    {
        if (effectiveWallMsOverride is not > 0 || summary.TotalWallMs > 0) return summary;
        return new PerformanceSummaryJson
        {
            DataSource = summary.DataSource,
            TurnCount = summary.TurnCount,
            TotalWallMs = effectiveWallMsOverride.Value,
            ByCategory = summary.ByCategory,
            SlowTools = summary.SlowTools
        };
    }

    private static PerformanceSummaryJson ParsePersisted(string turnMetricsJson, string dataSource)
    {
        using var doc = JsonDocument.Parse(turnMetricsJson);
        var root = doc.RootElement;
        var spans = root.TryGetProperty("spans", out var spansEl) ? spansEl : default;
        long llm = ReadLong(spans, "llmMs");
        long tools = ReadLong(spans, "toolsMs");
        long persist = ReadLong(spans, "persistMs");
        long total = ReadLong(root, "totalMs");
        long other = Math.Max(0, total - llm - tools - persist);

        var slowTools = new List<SlowToolJson>();
        if (root.TryGetProperty("toolCalls", out var toolCalls) && toolCalls.ValueKind == JsonValueKind.Array)
        {
            foreach (var group in toolCalls.EnumerateArray()
                         .Select(t => (
                             Tool: t.TryGetProperty("tool", out var tn) ? tn.GetString() ?? "unknown" : "unknown",
                             Ms: ReadLong(t, "durationMs")))
                         .GroupBy(x => x.Tool, StringComparer.OrdinalIgnoreCase))
            {
                slowTools.Add(new SlowToolJson
                {
                    Tool = group.Key,
                    MaxMs = group.Max(g => g.Ms),
                    Count = group.Count(),
                    Sequences = []
                });
            }
        }

        return new PerformanceSummaryJson
        {
            DataSource = dataSource,
            TurnCount = 1,
            TotalWallMs = total,
            ByCategory = new PerformanceByCategoryJson { Llm = llm, Tools = tools, Persist = persist, Other = other },
            SlowTools = slowTools
        };
    }

    private static PerformanceSummaryJson InferFromToolDurations(
        IReadOnlyList<AgentMessageDoc> allRows,
        IReadOnlyList<ToolEvent> timeline,
        IReadOnlyList<AgentMessageDoc> toolRows,
        int slowToolMs)
    {
        var ordered = allRows.OrderBy(r => r.Sequence).ToList();
        var totalWall = EstimateWallMs(ordered);
        var toolsMs = toolRows.Sum(r => r.ToolDurationMs ?? 0);
        var llmMs = Math.Max(0, totalWall - toolsMs);

        return new PerformanceSummaryJson
        {
            DataSource = "partial_persisted",
            TurnCount = CountUserTurns(ordered),
            TotalWallMs = totalWall,
            ByCategory = new PerformanceByCategoryJson { Llm = llmMs, Tools = toolsMs, Persist = 0, Other = 0 },
            SlowTools = BuildSlowToolsFromRows(toolRows, timeline, slowToolMs)
        };
    }

    private static PerformanceSummaryJson InferFromTimestamps(
        IReadOnlyList<AgentMessageDoc> allRows,
        IReadOnlyList<ToolEvent> timeline,
        int slowToolMs)
    {
        var ordered = allRows.Where(r => r.CosmosTimestamp is > 0).OrderBy(r => r.Sequence).ToList();
        long llmMs = 0, toolsMs = 0, persistMs = 0;
        var slowByTool = new Dictionary<string, (long MaxMs, int Count, List<string> Seqs)>(StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i < ordered.Count; i++)
        {
            var prev = ordered[i - 1];
            var cur = ordered[i];
            var gapMs = ((cur.CosmosTimestamp ?? 0) - (prev.CosmosTimestamp ?? 0)) * 1000L;
            if (gapMs <= 0) continue;

            if (string.Equals(cur.Role, "tool", StringComparison.OrdinalIgnoreCase))
            {
                toolsMs += gapMs;
                var tool = cur.ToolName ?? timeline.FirstOrDefault(t => t.Sequence == cur.Sequence)?.ToolName ?? "unknown";
                if (gapMs >= slowToolMs)
                    RecordSlow(slowByTool, tool, gapMs, cur.Sequence.ToString());
            }
            else if (string.Equals(cur.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                llmMs += gapMs;
            else if (string.Equals(cur.Role, "user", StringComparison.OrdinalIgnoreCase))
                persistMs += Math.Min(gapMs, 5000);
            else
                persistMs += gapMs;
        }

        var totalWall = EstimateWallMs(allRows.OrderBy(r => r.Sequence).ToList());

        return new PerformanceSummaryJson
        {
            DataSource = "_ts_inferred",
            TurnCount = CountUserTurns(allRows),
            TotalWallMs = totalWall,
            ByCategory = new PerformanceByCategoryJson { Llm = llmMs, Tools = toolsMs, Persist = persistMs, Other = 0 },
            SlowTools = slowByTool.Select(kv => new SlowToolJson
            {
                Tool = kv.Key,
                MaxMs = kv.Value.MaxMs,
                Count = kv.Value.Count,
                Sequences = kv.Value.Seqs
            }).ToList()
        };
    }

    private static List<SlowToolJson> BuildSlowToolsFromRows(
        IReadOnlyList<AgentMessageDoc> toolRows,
        IReadOnlyList<ToolEvent> timeline,
        int slowToolMs)
    {
        var slowByTool = new Dictionary<string, (long MaxMs, int Count, List<string> Seqs)>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in toolRows)
        {
            var ms = row.ToolDurationMs ?? 0;
            if (ms < slowToolMs) continue;
            var tool = row.ToolName ?? timeline.FirstOrDefault(t => t.Sequence == row.Sequence)?.ToolName ?? "unknown";
            RecordSlow(slowByTool, tool, ms, row.Sequence.ToString());
        }

        return slowByTool.Select(kv => new SlowToolJson
        {
            Tool = kv.Key,
            MaxMs = kv.Value.MaxMs,
            Count = kv.Value.Count,
            Sequences = kv.Value.Seqs
        }).ToList();
    }

    private static IReadOnlyList<Finding> EmitSlowAndLongFindings(
        IReadOnlyList<AgentMessageDoc> allRows,
        IReadOnlyList<ToolEvent> timeline,
        PerformanceSummaryJson summary,
        int slowToolMs,
        int longTurnMs)
    {
        var findings = new List<Finding>();
        foreach (var slow in summary.SlowTools.Where(s => s.MaxMs >= slowToolMs))
        {
            findings.Add(new Finding(
                "SLOW_TOOL",
                "degrading",
                $"{slow.Tool} exceeded {slowToolMs} ms (max {slow.MaxMs} ms, {slow.Count} call(s)).",
                slow.Sequences.Count > 0 ? slow.Sequences : ["0"]));
        }

        var userSeqs = allRows
            .Where(r => string.Equals(r.Role, "user", StringComparison.OrdinalIgnoreCase) && r.CosmosTimestamp is > 0)
            .OrderBy(r => r.Sequence)
            .ToList();
        for (var i = 1; i < userSeqs.Count; i++)
        {
            var gapMs = ((userSeqs[i].CosmosTimestamp ?? 0) - (userSeqs[i - 1].CosmosTimestamp ?? 0)) * 1000L;
            if (gapMs >= longTurnMs)
            {
                findings.Add(new Finding(
                    "LONG_TURN",
                    "degrading",
                    $"User turn at seq {userSeqs[i].Sequence} followed prior user turn after {gapMs} ms wall time.",
                    [userSeqs[i].Sequence.ToString()]));
            }
        }

        var userCount = allRows.Count(r => string.Equals(r.Role, "user", StringComparison.OrdinalIgnoreCase));
        if (userCount == 1 && summary.TotalWallMs >= longTurnMs)
        {
            var seq = allRows.First(r => string.Equals(r.Role, "user", StringComparison.OrdinalIgnoreCase)).Sequence;
            findings.Add(new Finding(
                "LONG_SINGLE_TURN",
                "degrading",
                $"Single user turn spanned ~{summary.TotalWallMs:N0} ms wall time.",
                [seq.ToString()]));
        }

        return findings;
    }

    private static void RecordSlow(
        Dictionary<string, (long MaxMs, int Count, List<string> Seqs)> map,
        string tool,
        long ms,
        string seq)
    {
        if (!map.TryGetValue(tool, out var cur))
            map[tool] = (ms, 1, [seq]);
        else
            map[tool] = (Math.Max(cur.MaxMs, ms), cur.Count + 1, cur.Seqs.Concat([seq]).ToList());
    }

    private static long EstimateWallMs(IReadOnlyList<AgentMessageDoc> ordered)
    {
        var withTs = ordered.Where(r => r.CosmosTimestamp is > 0).ToList();
        if (withTs.Count < 2) return 0;
        return ((withTs[^1].CosmosTimestamp ?? 0) - (withTs[0].CosmosTimestamp ?? 0)) * 1000L;
    }

    private static int CountUserTurns(IReadOnlyList<AgentMessageDoc> rows) =>
        rows.Count(r => string.Equals(r.Role, "user", StringComparison.OrdinalIgnoreCase));

    private static long ReadLong(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var prop)) return 0;
        return prop.ValueKind switch
        {
            JsonValueKind.Number when prop.TryGetInt64(out var n) => n,
            _ => 0
        };
    }
}
