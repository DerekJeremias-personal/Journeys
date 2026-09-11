using System.Text.Json;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.Services;
using Xunit;

namespace Journeys.Tests.Services;

public class CampaignJourneyMaterializerTests
{
    [Fact]
    public void Materialize_array_ruleJsonElement_includes_path_in_exception()
    {
        using var ruleDoc = JsonDocument.Parse("""
            [
              { "Kind": "NumericPropertyRule" },
              { "Kind": "HistoricalRule" }
            ]
            """);

        var ruleSet = new RuleSet("earn", ruleDoc.RootElement.Clone(), null, null, null);
        var journey = new JourneyNode("root", new List<RuleSet> { ruleSet }, "j1", "j1", null, null);
        var campaign = new Campaign("ext", CampaignStatusStrings.Draft, "n", null,
            DateTimeOffset.UtcNow, null, null, journey, "t1", "c1");

        var ex = Assert.Throws<JourneyMaterializePathException>(() =>
            CampaignJourneyMaterializer.Materialize(campaign));

        Assert.Contains("path=journey/rules[0]/ruleJsonElement", ex.Message, StringComparison.Ordinal);
        Assert.Contains("JOURNEY_SHAPE_RULE_JSON_NOT_OBJECT", ex.Message, StringComparison.Ordinal);
        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public void Materialize_string_ruleJsonElement_includes_path_and_string_violation()
    {
        using var doc = JsonDocument.Parse("""
            {
              "name": "Earn",
              "ruleJsonElement": "{\"Kind\":\"NumericPropertyRule\"}"
            }
            """);

        var ruleJson = doc.RootElement.GetProperty("ruleJsonElement");
        Assert.Equal(JsonValueKind.String, ruleJson.ValueKind);

        var ruleSet = new RuleSet("earn", ruleJson.Clone(), null, null, null);
        var journey = new JourneyNode("root", new List<RuleSet> { ruleSet }, "j1", "j1", null, null);
        var campaign = new Campaign("ext", CampaignStatusStrings.Draft, "n", null,
            DateTimeOffset.UtcNow, null, null, journey, "t1", "c1");

        var ex = Assert.Throws<JourneyMaterializePathException>(() =>
            CampaignJourneyMaterializer.Materialize(campaign));

        Assert.Contains("path=journey/rules[0]/ruleJsonElement", ex.Message, StringComparison.Ordinal);
        Assert.Contains(JourneyRuleShapeRules.StringEncodedViolationCode, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(JourneyRuleShapeRules.ViolationCode, ex.Message, StringComparison.Ordinal);
    }
}
