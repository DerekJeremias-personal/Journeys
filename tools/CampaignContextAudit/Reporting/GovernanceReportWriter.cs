using System.Text;
using CampaignContextAudit.Governance;

namespace CampaignContextAudit.Reporting;

public static class GovernanceReportWriter
{
    public static string Build(StaticGovernanceReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Campaign Agent Governance Static Audit");
        sb.AppendLine();
        sb.AppendLine($"_Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC._");
        sb.AppendLine();

        WriteExecutiveSummary(sb, report);
        WriteCorpusInventory(sb, report);
        WriteOrphanSection(sb, report);
        WriteDuplicationSection(sb, report);
        WriteCrossLayerAssessment(sb, report);
        WriteContradictionsSection(sb, report);
        WriteSkillScorecard(sb, report);
        WriteTraceCorrelation(sb, report);
        WriteRemediation(sb, report);
        WriteFidelityNotes(sb, report);

        return sb.ToString();
    }

    private static void WriteExecutiveSummary(StringBuilder sb, StaticGovernanceReport report)
    {
        sb.AppendLine("## Executive summary");
        sb.AppendLine();
        sb.AppendLine("| Metric | Count |");
        sb.AppendLine("|---|---|");
        foreach (var kv in report.CorpusCounts.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            sb.AppendLine($"| {kv.Key} | {kv.Value} |");

        var worstBand = report.SkillProfiles
            .OrderBy(p => BandRank(p.CompetencyBand))
            .FirstOrDefault()?.CompetencyBand ?? "n/a";
        sb.AppendLine();
        sb.AppendLine($"**Worst skill competency band:** {worstBand}");
        sb.AppendLine();
        sb.AppendLine("**Top remediation items:**");
        foreach (var item in report.Remediation.Take(5))
            sb.AppendLine($"{item.Rank}. {item.Action}");
        sb.AppendLine();
    }

    private static void WriteCorpusInventory(StringBuilder sb, StaticGovernanceReport report)
    {
        sb.AppendLine("## Corpus inventory");
        sb.AppendLine();
        sb.AppendLine("| File | Classification | Lines | Chars | Loaded for skills |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var e in report.Corpus)
        {
            var skills = e.LoadedForSkills.Count == 0 ? "—" : string.Join(", ", e.LoadedForSkills);
            sb.AppendLine($"| {e.FileName} | {e.Classification} | {e.LineCount} | {e.CharCount} | {skills} |");
        }
        sb.AppendLine();
    }

    private static void WriteOrphanSection(StringBuilder sb, StaticGovernanceReport report)
    {
        sb.AppendLine("## Orphan & deprecated content");
        sb.AppendLine();
        var orphanFindings = report.Findings.Where(f =>
            f.Code is "ORPHAN_GOVERNANCE_FILE" or "DEPRECATED_FULL_CONTENT" or "LEGACY_STUB").ToList();
        if (orphanFindings.Count == 0)
        {
            sb.AppendLine("_None._");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("| Code | Severity | Sources | Summary |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var f in orphanFindings)
            sb.AppendLine($"| {f.Code} | {f.Severity} | {string.Join("; ", f.Sources)} | {f.Summary} |");
        sb.AppendLine();
    }

    private static void WriteDuplicationSection(StringBuilder sb, StaticGovernanceReport report)
    {
        sb.AppendLine("## Duplication clusters (deduplicated)");
        sb.AppendLine();
        if (report.DuplicationClusters.Count == 0)
        {
            sb.AppendLine("_None._");
            sb.AppendLine();
            return;
        }

        sb.AppendLine($"_{report.DuplicationClusters.Count} unique clusters (Jaccard ≥ 0.85, cross-layer only)._");
        sb.AppendLine();
        foreach (var c in report.DuplicationClusters.Take(25))
        {
            sb.AppendLine($"- **{c.Theme}** — cluster `{c.ClusterId}` ({c.MemberCount} members, {c.LayerCount} layers, ~{c.EstimatedRedundantChars} redundant chars)");
            sb.AppendLine($"  - Excerpt: _{c.Excerpt}_");
            sb.AppendLine($"  - Sources: {string.Join(", ", c.Sources)}");
        }

        if (report.DuplicationClusters.Count > 25)
            sb.AppendLine($"\n_… and {report.DuplicationClusters.Count - 25} more clusters — see cross-layer assessment doc._");

        sb.AppendLine();
    }

    private static void WriteCrossLayerAssessment(StringBuilder sb, StaticGovernanceReport report)
    {
        sb.AppendLine("## Cross-layer duplication assessment");
        sb.AppendLine();
        if (report.DuplicationClusters.Count == 0)
        {
            sb.AppendLine("_None._");
            sb.AppendLine();
            return;
        }

        var byTheme = report.DuplicationClusters
            .GroupBy(c => c.Theme, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Sum(c => c.EstimatedRedundantChars))
            .ToList();

        sb.AppendLine("| Theme | Clusters | Layers | ~Redundant chars | Canonical owner |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var g in byTheme)
        {
            var sample = g.First();
            sb.AppendLine(
                $"| {g.Key} | {g.Count()} | {string.Join("/", g.SelectMany(c => c.Layers).Distinct())} "
                + $"| {g.Sum(c => c.EstimatedRedundantChars)} | {sample.CanonicalOwner} |");
        }

        sb.AppendLine();
        sb.AppendLine("**Recommended consolidation order:**");
        var groupedRemediation = report.DuplicationClusters
            .GroupBy(c => c.Theme, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Sum(c => c.EstimatedRedundantChars))
            .Take(8)
            .Select((g, i) => $"{i + 1}. **{g.Key}** — {g.First().RemediationAction}")
            .ToList();
        foreach (var line in groupedRemediation)
            sb.AppendLine(line);
        sb.AppendLine();
    }

    private static void WriteContradictionsSection(StringBuilder sb, StaticGovernanceReport report)
    {
        sb.AppendLine("## Contradictions & manifest conflicts");
        sb.AppendLine();
        var items = report.Findings.Where(f =>
            f.Code is "CONTRADICTORY_GUIDANCE" or "MANIFEST_PHASE_CONFLICT" or "DOC_GOVERNANCE_DRIFT").ToList();
        if (items.Count == 0)
        {
            sb.AppendLine("_None._");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("| Code | Severity | Skill | Sources | Summary |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var f in items)
            sb.AppendLine($"| {f.Code} | {f.Severity} | {f.Skill ?? "—"} | {string.Join("; ", f.Sources)} | {f.Summary} |");
        sb.AppendLine();
    }

    private static void WriteSkillScorecard(StringBuilder sb, StaticGovernanceReport report)
    {
        sb.AppendLine("## Per-skill competency scorecard");
        sb.AppendLine();
        sb.AppendLine("| Skill | Stable chars | Layers | Redundancy | Specificity | Coach dep. | Band |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        foreach (var p in report.SkillProfiles)
        {
            sb.AppendLine(
                $"| {p.Skill} | {p.StableChars} | {p.LayerCount} | {p.RedundancyIndex:P0} | {p.SpecificityScore:P0} | {p.CoachDependency:P0} | **{p.CompetencyBand}** |");
        }
        sb.AppendLine();
        sb.AppendLine("_Reference skills: EventModels + Verification. Bands compare redundancy/specificity/size to their average._");
        sb.AppendLine();
    }

    private static void WriteTraceCorrelation(StringBuilder sb, StaticGovernanceReport report)
    {
        sb.AppendLine("## Trace correlation");
        sb.AppendLine();
        if (!report.TraceCorrelationRun)
        {
            sb.AppendLine("_Trace correlation not run._");
            sb.AppendLine();
            return;
        }

        var boosted = report.Findings.Where(f => f.TraceBoosted).ToList();
        if (boosted.Count == 0)
        {
            sb.AppendLine("_No findings boosted by trace data._");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("| Code | Severity | Boosted | Summary |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var f in boosted)
            sb.AppendLine($"| {f.Code} | {f.Severity} | yes | {f.Summary} |");
        sb.AppendLine();
    }

    private static void WriteRemediation(StringBuilder sb, StaticGovernanceReport report)
    {
        sb.AppendLine("## Prioritized remediation");
        sb.AppendLine();
        foreach (var item in report.Remediation.Take(15))
            sb.AppendLine($"{item.Rank}. [{string.Join(", ", item.FindingCodes)}] {item.Action}");
        sb.AppendLine();
    }

    private static void WriteFidelityNotes(StringBuilder sb, StaticGovernanceReport report)
    {
        sb.AppendLine("## Fidelity notes");
        sb.AppendLine();
        sb.AppendLine("- Load set ports `CampaignAgentPromptComposer` + `CampaignWorkflowPhaseGovernanceFiles` (2026-06-19).");
        sb.AppendLine("- Duplication uses Jaccard ≥ 0.85 on tokens length ≥ 4.");
        sb.AppendLine($"- Trace correlation: {(report.TraceCorrelationRun ? "run" : "not run")}.");
        sb.AppendLine();
    }

    private static int BandRank(string band) =>
        band switch
        {
            "F" => 0,
            "D" => 1,
            "C" => 2,
            "B" => 3,
            "A" => 4,
            _ => 5
        };
}
