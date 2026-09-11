using CampaignContextAudit.Governance;
using CampaignContextAudit.Reporting;
using Xunit;

namespace CampaignContextAudit.Tests;

public class GovernanceReportWriterTests
{
    [Fact]
    public void Report_starts_with_executive_summary_and_ends_with_fidelity_notes()
    {
        var report = new StaticGovernanceReport(
            [],
            [],
            [],
            [],
            new Dictionary<string, int>(),
            [],
            TraceCorrelationRun: false);

        var md = GovernanceReportWriter.Build(report);
        Assert.StartsWith("# Campaign Agent Governance Static Audit", md);
        Assert.Contains("## Executive summary", md);
        Assert.Contains("## Fidelity notes", md);
        Assert.True(md.IndexOf("Executive summary", StringComparison.Ordinal)
                    < md.IndexOf("Per-skill competency", StringComparison.Ordinal));
    }
}
