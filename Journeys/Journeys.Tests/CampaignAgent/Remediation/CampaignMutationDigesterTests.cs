using System.Collections.Generic;
using System.Text.Json;
using Journeys.CampaignAgent.Remediation;
using Xunit;

namespace Journeys.Tests.CampaignAgent.Remediation;

public class CampaignMutationDigesterTests
{
    private const string SuccessCampaignJson = """
    {
      "Id": "d7033a80-7d50-450a-b910-e4ffe3e86f7f",
      "ExtCampaignId": "tiered loyalty program",
      "Status": "draft",
      "Name": "tiered loyalty program",
      "StartDate": "2024-12-19T00:00:00+00:00",
      "EndDate": null,
      "Events": ["a17daa79-8908-4e89-9fa2-6e5f629a2202"],
      "Segments": [],
      "Journey": {
        "Name": "Tiered Earning Journey",
        "Children": [
          {
            "Name": "Tier-Based Earning Node",
            "Children": [],
            "Rules": [
              { "Name": "Bronze Tier Earning", "RuleJsonElement": { "Kind": "NumericPropertyRule" },
                "OutcomesJsonElement": [ { "Kind": "DepositPointsOutcome" }, { "Kind": "DepositPointsOutcome" } ] },
              { "Name": "Silver Tier Earning", "RuleJsonElement": { "Kind": "NumericPropertyRule" },
                "OutcomesJsonElement": [ { "Kind": "DepositPointsOutcome" } ] }
            ]
          }
        ]
      }
    }
    """;

    private const string FailureJson = """
    { "errors": { "journey.validation.0": "[violation=TIER_A_SIMPLE_RULE_MISSING_LEFT_PROVIDER] ruleSet=Bronze" } }
    """;

    [Fact]
    public void Digest_success_builds_skeleton_ack_and_shrinks()
    {
        var result = CampaignMutationDigester.Digest(SuccessCampaignJson);

        Assert.True(result.Transformed);
        Assert.True(result.DigestChars < result.OriginalChars);

        using var doc = JsonDocument.Parse(result.Json);
        var root = doc.RootElement;

        Assert.False(string.IsNullOrEmpty(root.GetProperty("note").GetString()));

        var campaign = root.GetProperty("campaign");
        Assert.Equal("d7033a80-7d50-450a-b910-e4ffe3e86f7f", campaign.GetProperty("Id").GetString());
        Assert.Equal("tiered loyalty program", campaign.GetProperty("ExtCampaignId").GetString());
        Assert.Equal("draft", campaign.GetProperty("Status").GetString());
        Assert.Equal(JsonValueKind.Null, campaign.GetProperty("EndDate").ValueKind);
        Assert.Equal(1, campaign.GetProperty("Events").GetArrayLength());
        Assert.Equal(0, campaign.GetProperty("segmentCount").GetInt32());

        var journey = root.GetProperty("journey");
        Assert.Equal("Tiered Earning Journey", journey.GetProperty("name").GetString());
        var nodes = journey.GetProperty("nodes");
        Assert.Equal(1, nodes.GetArrayLength());
        var ruleSets = nodes[0].GetProperty("ruleSets");
        Assert.Equal(2, ruleSets.GetArrayLength());
        Assert.Equal("Bronze Tier Earning", ruleSets[0].GetProperty("name").GetString());
        Assert.Equal(2, ruleSets[0].GetProperty("outcomes").GetInt32());
        Assert.Equal(1, ruleSets[1].GetProperty("outcomes").GetInt32());

        Assert.DoesNotContain("RuleJsonElement", result.Json);
        Assert.DoesNotContain("NumericPropertyRule", result.Json);
    }

    [Fact]
    public void Digest_camel_case_journey_builds_skeleton_ack()
    {
        const string camelJson = """
        {
          "id": "camp-camel",
          "status": "draft",
          "name": "tier",
          "events": [],
          "journey": {
            "name": "Main",
            "rules": [
              {
                "name": "Bronze",
                "ruleJsonElement": { "Kind": "SimpleRule" },
                "outcomesJsonElement": [ { "Kind": "DepositPointsOutcome" } ]
              }
            ]
          }
        }
        """;

        var result = CampaignMutationDigester.Digest(camelJson);
        Assert.True(result.Transformed);
        using var doc = JsonDocument.Parse(result.Json);
        Assert.Equal("camp-camel", doc.RootElement.GetProperty("campaign").GetProperty("Id").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("journey").GetProperty("nodes")[0].GetProperty("ruleSets").GetArrayLength());
    }

    [Fact]
    public void Digest_root_level_rules_emit_single_node()
    {
        const string rootRulesJson = """
        {
          "Id": "camp-root",
          "Status": "draft",
          "Name": "root rules",
          "Events": [],
          "Journey": {
            "Name": "Main",
            "Rules": [
              {
                "Name": "Bronze",
                "RuleJsonElement": { "Kind": "NumericPropertyRule" },
                "OutcomesJsonElement": [ { "Kind": "DepositPointsOutcome" } ]
              }
            ]
          }
        }
        """;

        var result = CampaignMutationDigester.Digest(rootRulesJson);
        Assert.True(result.Transformed);

        using var doc = JsonDocument.Parse(result.Json);
        var nodes = doc.RootElement.GetProperty("journey").GetProperty("nodes");
        Assert.Equal(1, nodes.GetArrayLength());
        Assert.Equal(1, nodes[0].GetProperty("ruleSets").GetArrayLength());
        Assert.Equal("Bronze", nodes[0].GetProperty("ruleSets")[0].GetProperty("name").GetString());
    }

    [Fact]
    public void Digest_failure_passes_through_verbatim()
    {
        var result = CampaignMutationDigester.Digest(FailureJson);
        Assert.False(result.Transformed);
        Assert.Equal(FailureJson, result.Json);
    }

    [Fact]
    public void Digest_shell_creation_without_journey_acks_campaign_only()
    {
        const string shell = """
        { "Id": "7e13dc16-5437-497a-a5aa-cfb1af7e68b3", "Status": "draft", "Name": "tiered", "Events": [], "Segments": [] }
        """;

        var result = CampaignMutationDigester.Digest(shell);
        Assert.True(result.Transformed);
        using var doc = JsonDocument.Parse(result.Json);
        var root = doc.RootElement;
        Assert.Equal("7e13dc16-5437-497a-a5aa-cfb1af7e68b3", root.GetProperty("campaign").GetProperty("Id").GetString());
        Assert.False(root.TryGetProperty("journey", out _));
    }

    [Fact]
    public void Digest_unwraps_and_rewraps_meai_text_envelope()
    {
        var envelope = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["$type"] = "text",
            ["text"] = SuccessCampaignJson
        });

        var result = CampaignMutationDigester.Digest(envelope);

        Assert.True(result.Transformed);
        using var doc = JsonDocument.Parse(result.Json);
        var root = doc.RootElement;
        Assert.Equal("text", root.GetProperty("$type").GetString());

        using var innerDoc = JsonDocument.Parse(root.GetProperty("text").GetString()!);
        Assert.Equal("draft", innerDoc.RootElement.GetProperty("campaign").GetProperty("Status").GetString());
    }

    [Fact]
    public void Digest_envelope_wrapping_failure_passes_through()
    {
        var envelope = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["$type"] = "text",
            ["text"] = FailureJson
        });

        var result = CampaignMutationDigester.Digest(envelope);
        Assert.False(result.Transformed);
        Assert.Equal(envelope, result.Json);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{ \"unexpected\": true }")]
    [InlineData("[1,2,3]")]
    [InlineData("")]
    public void Digest_returns_original_when_not_a_campaign_success(string input)
    {
        var result = CampaignMutationDigester.Digest(input);
        Assert.False(result.Transformed);
        Assert.Equal(input, result.Json);
    }
}
