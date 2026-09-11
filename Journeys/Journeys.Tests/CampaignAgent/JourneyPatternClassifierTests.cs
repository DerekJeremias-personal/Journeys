using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public class JourneyPatternClassifierTests
{
    [Fact]
    public void Resolve_tier_brief_returns_tier_navigation_pattern()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefProposed =
            """{"objective":"3 tiers bronze silver gold water loyalty"}""";

        Assert.Equal(
            JourneyPatternClassifier.TierNavigationPointBalance,
            JourneyPatternClassifier.Resolve(state));
    }

    [Fact]
    public void Resolve_flat_cashback_brief_returns_null()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefProposed =
            """{"objective":"flat 2% cashback on all purchases"}""";

        Assert.Null(JourneyPatternClassifier.Resolve(state));
    }

    [Fact]
    public void Resolve_tier_qual_pat_in_manifest_returns_pattern_without_tier_words_in_brief()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefProposed = """{"objective":"loyalty program"}""";
        state.Artifacts.PointAccountManifest =
            """{"schemaVersion":1,"items":[{"id":"tqp-1","displayLabel":"Qual","role":"tierQualification"}]}""";

        Assert.Equal(
            JourneyPatternClassifier.TierNavigationPointBalance,
            JourneyPatternClassifier.Resolve(state));
    }
}
