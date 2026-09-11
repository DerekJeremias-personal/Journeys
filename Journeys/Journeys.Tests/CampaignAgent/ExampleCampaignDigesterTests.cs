using System.Text.Json;
using Journeys.CampaignAgent.Remediation;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public class ExampleCampaignDigesterTests
{
    private const string SampleExampleJson = """
    {
      "id": "3c4a8633-3a76-491f-b7b3-c592037ec9da",
      "extCampaignId": "TierSystem",
      "status": "draft",
      "name": "mericantires Tier System",
      "events": ["a6edbbc5-bf43-4c57-b2f1-e015b9efaf03"],
      "journey": {
        "Name": "mericantires Tier System",
        "Children": [
          {
            "Name": "Bronze",
            "Rules": [
              { "Name": "Bronze Point Economy", "OutcomesJsonElement": [ {}, {}, {} ] },
              { "Name": "Silver Point Economy", "OutcomesJsonElement": [ {}, {} ] }
            ]
          },
          {
            "Name": "Silver",
            "Rules": [
              { "Name": "Silver Point Economy", "OutcomesJsonElement": [ {} ] }
            ]
          }
        ]
      }
    }
    """;

    [Fact]
    public void Digest_example_campaign_builds_skeleton_counts()
    {
        var raw = SampleExampleJson;

        var result = ExampleCampaignDigester.Digest(raw);

        Assert.True(result.Transformed);
        Assert.True(result.DigestChars < result.OriginalChars);

        using var doc = JsonDocument.Parse(result.Json);
        var root = doc.RootElement;

        Assert.False(string.IsNullOrEmpty(root.GetProperty("note").GetString()));
        Assert.Equal("mericantires", root.GetProperty("tenant").GetString());

        var journey = root.GetProperty("journey");
        Assert.Equal(3, journey.GetProperty("nodeCount").GetInt32());
        Assert.Equal(3, journey.GetProperty("ruleSetCount").GetInt32());
        Assert.Equal(6, journey.GetProperty("outcomeCount").GetInt32());
        Assert.Equal("tier-navigation-point-balance", root.GetProperty("patternId").GetString());
    }

    [Fact]
    public void Digest_retains_ruleKindHistogram()
    {
        const string json = """
        {
          "extCampaignId": "HistoricalSpendThreshold30d",
          "patternId": "historical-spend-threshold",
          "name": "Spend threshold",
          "events": ["evt-1"],
          "journey": {
            "rules": [{
              "name": "Spend rule",
              "ruleJsonElement": {
                "Kind": "HistoricalRule",
                "HistoricalValueProvider": { "$type": "SimpleCalculationProvider" }
              },
              "outcomesJsonElement": []
            }]
          }
        }
        """;

        var result = ExampleCampaignDigester.Digest(json);

        Assert.True(result.Transformed);
        using var doc = JsonDocument.Parse(result.Json);
        var histogram = doc.RootElement.GetProperty("ruleKindHistogram");
        Assert.Equal(1, histogram.GetProperty("HistoricalRule").GetInt32());
    }

    [Fact]
    public void Digest_tenant_error_passthrough()
    {
        const string failure = """{ "error": "tenantId must be 'mericantires' for example campaign tools." }""";
        var result = ExampleCampaignDigester.Digest(failure);

        Assert.False(result.Transformed);
        Assert.Equal(failure, result.Json);
    }

    [Fact]
    public void Digest_with_structural_excerpt_returns_navigation_shape()
    {
        const string json = """
        {
          "extCampaignId": "TierSystem",
          "journey": {
            "navigation": { "$type": "SimpleNavigationCriteria", "pointAccountId": "pat-1" },
            "children": [
              { "name": "Bronze", "rules": [{ "name": "Bronze rules" }] }
            ]
          }
        }
        """;

        var result = ExampleCampaignDigester.Digest(json, useStructuralExcerpt: true);

        Assert.True(result.Transformed);
        Assert.Contains("SimpleNavigationCriteria", result.Json, StringComparison.Ordinal);
        Assert.DoesNotContain("nodeCount", result.Json, StringComparison.Ordinal);
    }

    [Fact]
    public void Digest_malformed_passthrough()
    {
        const string bad = "not-json";
        var result = ExampleCampaignDigester.Digest(bad);

        Assert.False(result.Transformed);
        Assert.Equal(bad, result.Json);
    }
}
