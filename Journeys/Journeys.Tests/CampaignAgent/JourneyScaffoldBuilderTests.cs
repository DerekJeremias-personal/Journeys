using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public class JourneyScaffoldBuilderTests
{
    private const string Manifest = """
        {"schemaVersion":1,"items":[
          {"id":"tqp-guid","displayLabel":"Tier Qual Points","isSpendable":false},
          {"id":"spend-guid","displayLabel":"Spendable Points","isSpendable":true}
        ]}
        """;

    [Fact]
    public void Build_tier_ladder_includes_validate_before_upsert_footer()
    {
        var result = JourneyScaffoldBuilder.Build(RuleEpisode.TierLadder, Manifest, "tier-navigation-point-balance");
        Assert.NotNull(result);
        Assert.Contains("validate_campaign before upsert_campaign", result!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_tier_ladder_anti_nodes_and_tier_transition()
    {
        var result = JourneyScaffoldBuilder.Build(RuleEpisode.TierLadder, Manifest, "tier-navigation-point-balance");
        Assert.NotNull(result);
        Assert.Contains("Do NOT use journey.nodes[]", result!, StringComparison.Ordinal);
        Assert.Contains("navigation.Transition", result!, StringComparison.Ordinal);
        Assert.Contains("Do NOT put navigation.Entry on tier nodes", result!, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_tier_ladder_includes_Entry_name()
    {
        var result = JourneyScaffoldBuilder.Build(RuleEpisode.TierLadder, Manifest, "tier-navigation-point-balance");
        Assert.NotNull(result);
        Assert.Contains("\"name\": \"Entry\"", result!, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_tier_ladder_includes_deposit_points_template()
    {
        var result = JourneyScaffoldBuilder.Build(RuleEpisode.TierLadder, Manifest, null);
        Assert.NotNull(result);
        Assert.Contains("PointsPerDollar", result!, StringComparison.Ordinal);
        Assert.Contains("PathValueProvider", result!, StringComparison.Ordinal);
        Assert.Contains("Do not use AggregateValueProvider or SimpleCalculationProvider", result!, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_tier_ladder_includes_children_and_entry_json()
    {
        var result = JourneyScaffoldBuilder.Build(RuleEpisode.TierLadder, Manifest, "tier-navigation-point-balance");

        Assert.NotNull(result);
        Assert.Contains("Do not author flat journey.rules[]", result!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"children\"", result!, StringComparison.Ordinal);
        Assert.Contains("\"navigation\"", result!, StringComparison.Ordinal);
        Assert.Contains("\"Entry\"", result!, StringComparison.Ordinal);
        Assert.Contains("tqp-guid", result!, StringComparison.Ordinal);
        Assert.Contains("spend-guid", result!, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_generic_episode_prose_only_no_json_fragment()
    {
        var result = JourneyScaffoldBuilder.Build(RuleEpisode.Generic, Manifest, patternId: null);

        Assert.Null(result);
    }

    [Fact]
    public void Build_tier_ladder_line_item_brief_adds_ordertotal_note()
    {
        var brief = """{"objective":"earn on sum of line items and qty"}""";
        var result = JourneyScaffoldBuilder.Build(RuleEpisode.TierLadder, Manifest, null, brief);

        Assert.NotNull(result);
        Assert.Contains("event.ordertotal", result!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("line-item", result!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_empty_manifest_returns_null()
    {
        var result = JourneyScaffoldBuilder.Build(RuleEpisode.TierLadder, """{"schemaVersion":1,"items":[]}""", null);
        Assert.Null(result);
    }
}
