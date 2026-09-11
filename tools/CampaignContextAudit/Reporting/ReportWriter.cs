using System.Text;
using CampaignContextAudit.Analysis;
using CampaignContextAudit.Models;

namespace CampaignContextAudit.Reporting;

public static class ReportWriter
{
    public static string Build(
        string sourceName,
        IReadOnlyList<TurnBudget> turns,
        IReadOnlyList<Finding> findings,
        WorkflowSnapshot? snapshot = null,
        PerformanceSummaryJson? performance = null,
        string? tenantSliceNote = null,
        RubricScorecard? rubric = null,
        DeliveryScorecard? delivery = null,
        string? linkedCampaignId = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Campaign Agent Context Audit — `{sourceName}`").AppendLine();
        sb.AppendLine($"_Generated: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC. Char counts are exact; token counts are ~chars/4 estimates._").AppendLine();

        if (snapshot is not null)
        {
            sb.AppendLine("## Outcome summary").AppendLine();
            sb.AppendLine("| Field | Value |");
            sb.AppendLine("|---|---|");
            sb.AppendLine($"| creationComplete | {FormatNullable(snapshot.CreationComplete)} |");
            sb.AppendLine($"| journeyRuleSetCount | {FormatNullable(snapshot.JourneyRuleSetCount)} |");
            sb.AppendLine($"| workflowPhase | {snapshot.WorkflowPhase ?? "(unknown)"} |");
            sb.AppendLine($"| patManifestCount | {snapshot.PatManifestCount} |");
            sb.AppendLine($"| verificationPatCount | {snapshot.VerificationPatCount} |");
            if (!string.IsNullOrWhiteSpace(linkedCampaignId))
                sb.AppendLine($"| linkedCampaignId | {linkedCampaignId} |");
            var blocking = findings.Where(f => string.Equals(f.Severity, "blocking", StringComparison.OrdinalIgnoreCase)).ToList();
            if (blocking.Count > 0)
                sb.AppendLine($"| blocking findings | {string.Join(", ", blocking.Select(f => f.Code))} |");
            sb.AppendLine();
        }

        if (delivery is not null)
        {
            sb.AppendLine("## Creation delivery").AppendLine();
            sb.AppendLine($"**Delivery grade: {delivery.Grade}**").AppendLine();
            sb.AppendLine("| Milestone | Status |");
            sb.AppendLine("|---|---|");
            foreach (var m in delivery.Milestones)
                sb.AppendLine($"| {m.Label} | {(m.Reached ? "✓" : "✗")} |");
            sb.AppendLine();
            sb.AppendLine($"_Highest reached: {delivery.HighestReached ?? "(none)"}. Expected minimum: {delivery.ExpectedMinimum ?? "(none)"}. Effective wall time: ~{delivery.EffectiveWallMs:N0} ms._").AppendLine();
        }

        sb.AppendLine("## Per-turn context budget").AppendLine();
        sb.AppendLine("| User seq | Phase | Stable chars | History chars | SESSION (est.) | Total (est.) | History user-turns | Dropped segs | Dropped chars | Approx tokens |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
        foreach (var t in turns)
            sb.AppendLine($"| {t.UserSequence} | {t.Phase} | {t.StableChars:N0} | {t.HistoryChars:N0} | {t.SessionChars:N0} | {t.TotalChars:N0} | {t.HistoryUserTurns} | {t.DroppedSegmentCount} | {t.DroppedChars:N0} | {t.ApproxTotalTokens:N0} |");
        sb.AppendLine();

        var sessionNote = tenantSliceNote is null
            ? "SESSION is estimated from the workflow row and budgeted thread context via production prompt builders; tenant curated slice omitted unless `--tenant-slice-file` is supplied."
            : tenantSliceNote;
        sb.AppendLine($"_Phase is an approximate tool-driven floor — per-turn phase is not stored in transcripts, so it is inferred from observed workflow tools and Stable chars follow it. \"History chars\" is budgeted history entering the turn (a turn's own tool results land in later rows). {sessionNote}_").AppendLine();

        if (performance is not null)
        {
            sb.AppendLine("## Performance summary").AppendLine();
            var wallNote = delivery is not null && delivery.EffectiveWallMs != performance.TotalWallMs
                ? $"; effective wall ~{delivery.EffectiveWallMs:N0} ms"
                : "";
            sb.AppendLine($"_Data source: `{performance.DataSource}`; turn count {performance.TurnCount}; total wall ~{performance.TotalWallMs:N0} ms{wallNote}._").AppendLine();
            sb.AppendLine("| Category | Ms (est.) |");
            sb.AppendLine("|---|---|");
            sb.AppendLine($"| llm | {performance.ByCategory.Llm:N0} |");
            sb.AppendLine($"| tools | {performance.ByCategory.Tools:N0} |");
            sb.AppendLine($"| persist | {performance.ByCategory.Persist:N0} |");
            sb.AppendLine($"| other | {performance.ByCategory.Other:N0} |");
            sb.AppendLine();
        }

        sb.AppendLine("## Detected findings").AppendLine();
        if (findings.Count == 0) sb.AppendLine("_No findings._").AppendLine();
        else
        {
            sb.AppendLine("| Code | Severity | Cited seqs | Summary |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var f in findings)
                sb.AppendLine($"| {f.Code} | {f.Severity} | {string.Join(", ", f.CitedSequences)} | {f.Summary} |");
            sb.AppendLine();
        }

        rubric ??= RubricGrader.Grade(findings, snapshot, turns, delivery?.Grade);

        sb.AppendLine("## Rubric scorecard").AppendLine();
        sb.AppendLine("| # | Dimension | Lens | Grade | Rationale (cite seqs/code) |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var d in rubric.Dimensions)
            sb.AppendLine($"| {d.DimensionId} | {d.Dimension} | {d.Lens} | **{d.Grade}** | {d.Rationale} |");
        sb.AppendLine();
        sb.AppendLine("## Overall verdict").AppendLine();
        if (delivery is not null)
            sb.AppendLine($"**Creation delivery: {delivery.Grade}**  ");
        var competencyLine = rubric.Competency.Grade;
        if (rubric.UncappedCompetency is not null && rubric.UncappedCompetency.Grade != rubric.Competency.Grade)
            competencyLine += $" _(capped; uncapped {rubric.UncappedCompetency.Grade})_";
        sb.AppendLine($"**Overall competency (dims 1–5): {competencyLine}**  ");
        sb.AppendLine($"**Efficiency (dims 6–8): {rubric.Efficiency.Grade}**");
        return sb.ToString();
    }

    private static string FormatNullable<T>(T? value) where T : struct => value?.ToString() ?? "(unknown)";
}
