using Journeys.CampaignAgent.Remediation;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public class ListCampaignsDigesterTests
{
    [Fact]
    public void Digest_compacts_entities_to_id_name_status()
    {
        const string raw = """
            {
              "count": 2,
              "entities": [
                {
                  "id": "camp-1",
                  "name": "Tier promo",
                  "status": "Draft",
                  "journey": { "ruleSets": [ { "id": "rs1" }, { "id": "rs2" } ] },
                  "segments": [ { "id": "seg-1", "name": "Huge segment payload" } ]
                },
                {
                  "id": "camp-2",
                  "name": "Other",
                  "status": "Live"
                }
              ]
            }
            """;

        var result = ListCampaignsDigester.Digest(raw);

        Assert.True(result.Transformed);
        Assert.True(result.DigestChars < result.OriginalChars);
        Assert.Contains("camp-1", result.Json, StringComparison.Ordinal);
        Assert.Contains("Tier promo", result.Json, StringComparison.Ordinal);
        Assert.Contains("journeyRuleSetCount", result.Json, StringComparison.Ordinal);
        Assert.DoesNotContain("Huge segment payload", result.Json, StringComparison.Ordinal);
    }

    [Fact]
    public void Digest_passes_through_errors()
    {
        const string raw = """{"errors":{"tenantId":"required"}}""";
        var result = ListCampaignsDigester.Digest(raw);
        Assert.False(result.Transformed);
        Assert.Equal(raw, result.Json);
    }
}
