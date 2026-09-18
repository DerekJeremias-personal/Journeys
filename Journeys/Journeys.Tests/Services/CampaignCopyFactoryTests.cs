using Journeys.Core.Services;
using Journeys.DTO.Models;
using Xunit;

namespace Journeys.Tests.Services;

public class CampaignCopyFactoryTests
{
    private static CampaignDto Source() => new()
    {
        Id = "src-id",
        Etag = "etag",
        ExtCampaignId = "summer",
        Name = "Summer",
        Status = "live",
        StartDate = DateTimeOffset.Parse("2026-01-01Z"),
        Events = new List<string> { "evt" },
        DeployedDate = DateTimeOffset.Parse("2026-02-01Z"),
        ArchivedDate = DateTimeOffset.Parse("2026-03-01Z")
    };

    [Fact]
    public void ForNewProgram_creates_unique_program_identity_and_draft_name_suffix()
    {
        var source = Source();

        var firstCopy = CampaignCopyFactory.ForNewProgram(source, null);
        var secondCopy = CampaignCopyFactory.ForNewProgram(source, null);

        Assert.False(string.IsNullOrWhiteSpace(firstCopy.Id));
        Assert.NotEqual(source.Id, firstCopy.Id);
        Assert.NotEqual(source.ExtCampaignId, firstCopy.ExtCampaignId);
        Assert.NotEqual(source.ExtCampaignId, secondCopy.ExtCampaignId);
        Assert.NotEqual(firstCopy.ExtCampaignId, secondCopy.ExtCampaignId);
        Assert.Equal("draft", firstCopy.Status);
        Assert.Equal("Summer Copy", firstCopy.Name);
        Assert.Null(firstCopy.Etag);
        Assert.Null(firstCopy.DeployedDate);
        Assert.Null(firstCopy.ArchivedDate);
        Assert.Equal(new List<string> { "evt" }, firstCopy.Events);
    }

    [Fact]
    public void ForNewProgram_uses_name_override()
    {
        var copy = CampaignCopyFactory.ForNewProgram(Source(), "Other");
        Assert.Equal("Other", copy.Name);
    }

    [Fact]
    public void ForRestoreFromArchive_new_id_same_ext_draft()
    {
        var archive = Source();
        archive.Status = "archive";
        var draft = CampaignCopyFactory.ForRestoreFromArchive(archive);
        Assert.NotEqual("src-id", draft.Id);
        Assert.Equal("summer", draft.ExtCampaignId);
        Assert.Equal("draft", draft.Status);
        Assert.Equal("Summer", draft.Name);
        Assert.Null(draft.Etag);
        Assert.Null(draft.DeployedDate);
        Assert.Null(draft.ArchivedDate);
    }
}
