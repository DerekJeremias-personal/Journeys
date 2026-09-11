using CampaignContextAudit.Models;
using CampaignContextAudit.Reporting;
using Xunit;

namespace CampaignContextAudit.Tests;

public class TranscriptDiffWriterTests
{
    [Fact]
    public void Diff_lists_added_finding_code()
    {
        var baseline = new AuditReportJson
        {
            SourceFile = "baseline.json",
            ConversationId = "c1",
            GeneratedAtUtc = DateTimeOffset.UtcNow.AddHours(-1),
            OutcomeSummary = new OutcomeSummary { CreationComplete = false, WorkflowPhase = "Done" },
            Findings = [],
            BudgetByTurn = []
        };
        var current = new AuditReportJson
        {
            SourceFile = "current.json",
            ConversationId = "c1",
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            OutcomeSummary = new OutcomeSummary { CreationComplete = false, WorkflowPhase = "Done" },
            Findings =
            [
                new FindingJson
                {
                    Code = "CREATION_INCOMPLETE_AT_DONE",
                    Severity = "blocking",
                    CitedSequences = ["0"],
                    Summary = "blocking"
                }
            ],
            BudgetByTurn = []
        };

        var md = TranscriptDiffWriter.Build(baseline, current, "conversion");

        Assert.Contains("CREATION_INCOMPLETE_AT_DONE", md);
        Assert.Contains("### Added", md);
    }
}
