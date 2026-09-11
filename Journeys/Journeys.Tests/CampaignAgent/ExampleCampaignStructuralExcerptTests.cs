using System.Text.Json;
using Journeys.CampaignAgent.Remediation;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public class ExampleCampaignStructuralExcerptTests
{
    [Fact]
    public void TryBuild_includes_navigation_and_first_child()
    {
        const string json = """
        {
          "journey": {
            "navigation": { "$type": "SimpleNavigationCriteria", "pointAccountId": "pat-1" },
            "children": [
              { "name": "Bronze", "rules": [{ "name": "Bronze rules" }] }
            ]
          }
        }
        """;

        var excerpt = ExampleCampaignStructuralExcerpt.TryBuild(json);

        Assert.NotNull(excerpt);
        using var doc = JsonDocument.Parse(excerpt!);
        Assert.True(doc.RootElement.TryGetProperty("navigation", out var nav));
        Assert.Equal("SimpleNavigationCriteria", nav.GetProperty("$type").GetString());
        Assert.True(doc.RootElement.TryGetProperty("firstChild", out _));
    }
}
