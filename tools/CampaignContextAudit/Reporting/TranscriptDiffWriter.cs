using System.Text;
using CampaignContextAudit.Models;

namespace CampaignContextAudit.Reporting;

public static class TranscriptDiffWriter
{
    public static string Build(AuditReportJson baseline, AuditReportJson current, string sourceName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Campaign Context Audit Diff — `{sourceName}`").AppendLine();
        sb.AppendLine($"_Baseline: {baseline.SourceFile} ({baseline.GeneratedAtUtc:yyyy-MM-dd HH:mm} UTC)_");
        sb.AppendLine($"_Current: {current.SourceFile} ({current.GeneratedAtUtc:yyyy-MM-dd HH:mm} UTC)_").AppendLine();

        sb.AppendLine("## Outcome summary changes").AppendLine();
        AppendOutcomeFieldDiff(sb, "creationComplete", baseline.OutcomeSummary.CreationComplete, current.OutcomeSummary.CreationComplete);
        AppendOutcomeFieldDiff(sb, "journeyRuleSetCount", baseline.OutcomeSummary.JourneyRuleSetCount, current.OutcomeSummary.JourneyRuleSetCount);
        AppendOutcomeFieldDiff(sb, "workflowPhase", baseline.OutcomeSummary.WorkflowPhase, current.OutcomeSummary.WorkflowPhase);
        AppendOutcomeFieldDiff(sb, "linkedCampaignId", baseline.OutcomeSummary.LinkedCampaignId, current.OutcomeSummary.LinkedCampaignId);
        sb.AppendLine();

        var baselineCodes = baseline.Findings.Select(f => f.Code).ToHashSet(StringComparer.Ordinal);
        var currentCodes = current.Findings.Select(f => f.Code).ToHashSet(StringComparer.Ordinal);
        var added = currentCodes.Except(baselineCodes, StringComparer.Ordinal).OrderBy(c => c).ToList();
        var removed = baselineCodes.Except(currentCodes, StringComparer.Ordinal).OrderBy(c => c).ToList();

        sb.AppendLine("## Finding code changes").AppendLine();
        if (added.Count == 0 && removed.Count == 0)
            sb.AppendLine("_No finding code additions or removals._").AppendLine();
        else
        {
            if (added.Count > 0)
            {
                sb.AppendLine("### Added").AppendLine();
                foreach (var code in added)
                    sb.AppendLine($"- `{code}`");
                sb.AppendLine();
            }

            if (removed.Count > 0)
            {
                sb.AppendLine("### Removed").AppendLine();
                foreach (var code in removed)
                    sb.AppendLine($"- `{code}`");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static void AppendOutcomeFieldDiff(StringBuilder sb, string name, object? baseline, object? current)
    {
        if (Equals(baseline, current)) return;
        sb.AppendLine($"- **{name}**: `{baseline ?? "(null)"}` → `{current ?? "(null)"}`");
    }
}
