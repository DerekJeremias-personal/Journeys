using System.Text.Json;
using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Utility;

public class CampaignShellDigestBuilderTests
{
    [Fact]
    public void TryBuildFromUpsertResult_flags_ineligible_event_model_ids()
    {
        const string json = """
            {
              "id": "camp-1",
              "name": "Summer Promo",
              "status": "Draft",
              "startDate": "2025-06-01T00:00:00Z",
              "events": ["evt-eligible", "evt-bad"]
            }
            """;

        var eligibility = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["evt-eligible"] = true,
            ["evt-bad"] = false
        };

        var ok = CampaignShellDigestBuilder.TryBuildFromUpsertResult(json, out var digestJson, eligibility);
        Assert.True(ok);

        using var doc = JsonDocument.Parse(digestJson!);
        var ineligible = doc.RootElement.GetProperty("ineligibleEventModelIds")
            .EnumerateArray()
            .Select(e => e.GetString())
            .ToList();
        Assert.Equal(["evt-bad"], ineligible);
    }

    [Fact]
    public void TryBuildFromUpsertResult_minimal_campaign_shell_digest()
    {
        const string json = """
            {
              "id": "camp-1",
              "name": "Summer Promo",
              "status": "Draft",
              "startDate": "2025-06-01T00:00:00Z",
              "events": ["evt-a", "evt-b"]
            }
            """;

        var ok = CampaignShellDigestBuilder.TryBuildFromUpsertResult(json, out var digestJson);
        Assert.True(ok);

        using var doc = JsonDocument.Parse(digestJson!);
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("camp-1", root.GetProperty("campaignId").GetString());
        Assert.False(root.GetProperty("hasJourneyPayload").GetBoolean());

        var eventIds = root.GetProperty("eventModelIds").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Equal(new[] { "evt-a", "evt-b" }, eventIds);
        Assert.Empty(root.GetProperty("warnings").EnumerateArray());
    }

    [Fact]
    public void TryBuildFromUpsertResult_journey_with_rules_adds_warning()
    {
        const string json = """
            {
              "id": "camp-2",
              "name": "Journey Shell",
              "status": "Draft",
              "startDate": "2025-06-01T00:00:00Z",
              "journey": {
                "id": "j-root",
                "name": "Root",
                "rules": [
                  { "id": "rs-1", "name": "Earn", "ruleJsonElement": { "Kind": "SimpleRule" } }
                ]
              }
            }
            """;

        var ok = CampaignShellDigestBuilder.TryBuildFromUpsertResult(json, out var digestJson);
        Assert.True(ok);

        using var doc = JsonDocument.Parse(digestJson!);
        var root = doc.RootElement;
        Assert.True(root.GetProperty("hasJourneyPayload").GetBoolean());

        var warnings = root.GetProperty("warnings").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains(
            warnings,
            w => w != null && w.Contains("CampaignJourney", StringComparison.OrdinalIgnoreCase));
    }
}
