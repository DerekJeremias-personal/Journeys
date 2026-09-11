using CampaignContextAudit.Governance;
using Xunit;

namespace CampaignContextAudit.Tests;

public class GovernanceCorpusLoaderTests
{
    private static string? GovernanceDir
    {
        get
        {
            var dir = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..",
                "Journeys", "Journeys.API", "CampaignAgent"));
            return Directory.Exists(dir) ? dir : null;
        }
    }

    [Fact]
    public void Load_classifies_journey_file_as_orphan()
    {
        if (GovernanceDir is null) return;
        var corpus = GovernanceCorpusLoader.Load(GovernanceDir, dataWarehouseEnabled: false);
        var journey = corpus.First(e =>
            e.FileName.Equals("WorkflowPhaseCampaignJourneyGovernance.txt", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(GovernanceFileClassification.Orphan, journey.Classification);
    }

    [Fact]
    public void Load_classifies_legacy_stub()
    {
        if (GovernanceDir is null) return;
        var corpus = GovernanceCorpusLoader.Load(GovernanceDir, dataWarehouseEnabled: false);
        var stub = corpus.FirstOrDefault(e =>
            e.FileName.Equals("CampaignGovernance.txt", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(stub);
        Assert.Equal(GovernanceFileClassification.LegacyStub, stub!.Classification);
    }
}
