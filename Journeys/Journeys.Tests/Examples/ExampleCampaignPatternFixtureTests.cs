using System.IO;
using System.Text.Json;
using Journeys.Core.Services;
using Xunit;

namespace Journeys.Tests.Examples;

public class ExampleCampaignPatternFixtureTests
{
    private static readonly string ApiContentRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "Journeys.API"));

    private static string ReadExample(string fileName)
    {
        var path = Path.Combine(ApiContentRoot, "Examples", "Journeys", "Campaigns", fileName);
        return File.ReadAllText(path);
    }

    [Theory]
    [InlineData("historical-spend-threshold-30d.json")]
    [InlineData("historical-count-90d.json")]
    public void Example_campaign_json_passes_Tier_A_polymorphic_validation(string fileName)
    {
        var json = ReadExample(fileName);
        using var doc = JsonDocument.Parse(json);

        var ex = Record.Exception(() =>
            CampaignJourneyPolymorphicMetadataValidator.ValidateCampaignJson(doc.RootElement));

        Assert.Null(ex);
    }

    [Theory]
    [InlineData("historical-spend-threshold-30d.json", "historical-spend-threshold")]
    [InlineData("historical-count-90d.json", "historical-event-count")]
    public void Example_campaign_json_has_patternId(string fileName, string expectedPatternId)
    {
        var json = ReadExample(fileName);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("patternId", out var patternId));
        Assert.Equal(expectedPatternId, patternId.GetString());
    }
}
