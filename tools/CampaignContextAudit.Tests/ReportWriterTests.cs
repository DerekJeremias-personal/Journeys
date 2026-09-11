using CampaignContextAudit.Analysis;
using CampaignContextAudit.Reporting;
using Xunit;

namespace CampaignContextAudit.Tests;

public class ReportWriterTests
{
    [Fact]
    public void Renders_table_caveat_and_findings()
    {
        var turns = new List<TurnBudget>
        {
            new(0, "EventModels", 19695, 32, 5000, 24727, 1, 0, 0, 6182),
        };
        var findings = new List<Finding>
        {
            new("TOOL_RESULT_BLOAT", "degrading", "get_all_models result is 5,000 chars, persisted verbatim into history.", new[] { "2" }),
        };

        var md = ReportWriter.Build("conv", turns, findings);

        Assert.Contains("## Per-turn context budget", md);
        Assert.Contains("SESSION (est.)", md);
        Assert.Contains("TOOL_RESULT_BLOAT", md);
        Assert.Contains("## Rubric scorecard", md);
        Assert.Contains("Overall competency (dims 1–5):", md);
        Assert.DoesNotContain("fill grades", md, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("analyst to confirm", md, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreationDeliverySection_beforeRubric()
    {
        var delivery = new DeliveryScorecard(
            "F",
            0.25,
            "aborted",
            480_000,
            CreationMilestoneIds.PointAccountTypes,
            CreationMilestoneIds.CampaignJourney,
            [new(CreationMilestoneIds.CampaignJourney, "Journey saved", false)]);
        var md = ReportWriter.Build("conv", [], [], null, null, null, null, delivery);

        var deliveryIdx = md.IndexOf("## Creation delivery", StringComparison.Ordinal);
        var rubricIdx = md.IndexOf("## Rubric scorecard", StringComparison.Ordinal);
        Assert.True(deliveryIdx >= 0 && rubricIdx > deliveryIdx);
        Assert.Contains("Delivery grade: F", md);
    }
}
