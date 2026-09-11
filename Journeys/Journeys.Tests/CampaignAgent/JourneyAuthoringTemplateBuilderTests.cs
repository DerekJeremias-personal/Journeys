using Journeys.API.CampaignAgent.Workflow;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public class JourneyAuthoringTemplateBuilderTests
{
    private const string ManifestJson = """
        {"schemaVersion":1,"items":[
          {"id":"fe7bcfd9-0000-0000-0000-000000000001","displayLabel":"Tier Qual Points","isSpendable":false},
          {"id":"spend-guid","displayLabel":"Spendable Points","isSpendable":true}
        ]}
        """;

    [Fact]
    public void Build_tier_ladder_has_children_not_nodes()
    {
        var json = JourneyAuthoringTemplateBuilder.Build(ManifestJson, eventModelId: "evt-1", patternId: "tier-navigation-point-balance");
        Assert.NotNull(json);
        Assert.Contains("\"children\"", json!, StringComparison.Ordinal);
        Assert.DoesNotContain("\"nodes\"", json!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_fills_pat_ids_and_event_string_array()
    {
        var json = JourneyAuthoringTemplateBuilder.Build(
            ManifestJson,
            "a6edbbc5-bf43-4c57-b2f1-e015b9efaf03",
            "tier-navigation-point-balance");
        Assert.Contains("a6edbbc5-bf43-4c57-b2f1-e015b9efaf03", json!);
        Assert.Contains("fe7bcfd9", json!);
        Assert.Contains("spend-guid", json!);
    }

    [Fact]
    public void Build_returns_null_when_manifest_empty()
    {
        Assert.Null(JourneyAuthoringTemplateBuilder.Build("""{"schemaVersion":1,"items":[]}""", "evt-1", null));
    }

    [Fact]
    public void Build_returns_null_when_event_model_missing()
    {
        Assert.Null(JourneyAuthoringTemplateBuilder.Build(ManifestJson, null, null));
    }
}
