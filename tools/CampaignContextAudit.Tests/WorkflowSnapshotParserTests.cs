using CampaignContextAudit.Analysis;
using CampaignContextAudit.Transcript;
using Xunit;

namespace CampaignContextAudit.Tests;

public class WorkflowSnapshotParserTests
{
    private static string ConversionSamplePath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "AgentMessageSamples", "conversion.json"));

    [Fact]
    public void Parses_conversion_workflow_row_content()
    {
        var loaded = TranscriptLoader.Load(ConversionSamplePath);
        var workflow = loaded.WorkflowRows.OrderByDescending(w => w.Sequence).First();

        var snapshot = WorkflowSnapshotParser.Parse(workflow);

        Assert.NotNull(snapshot);
        Assert.False(snapshot!.CreationComplete);
        Assert.Equal(0, snapshot.JourneyRuleSetCount);
        Assert.Equal("CampaignJourney", snapshot.WorkflowPhase);
        Assert.Equal(2, snapshot.PatManifestCount);
        Assert.Equal(0, snapshot.VerificationPatCount);
        Assert.True(snapshot.UpsertFailedSinceValidate);
        Assert.False(snapshot.UserRequestedNewEventModel);
        Assert.False(snapshot.FetchFailed);
        Assert.NotNull(snapshot.LastRemediationPreview);
        Assert.True(snapshot.LastRemediationPreview!.Length <= 200);
        Assert.Contains("invocation", snapshot.LastRemediationPreview!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parses_journey_rule_set_count_from_digest_when_snapshot_missing()
    {
        var content = """
            {
              "journeyDigestProposed": "{\"ruleSetCount\":2}"
            }
            """;

        var snapshot = WorkflowSnapshotParser.Parse(content, "CampaignJourney");

        Assert.NotNull(snapshot);
        Assert.Equal(2, snapshot!.JourneyRuleSetCount);
    }
}
