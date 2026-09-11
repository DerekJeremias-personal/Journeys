using Journeys.API.CampaignAgent;
using Journeys.Core.Models;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public class JourneyPatternPrepResolverTests
{
    [Fact]
    public void TryPrepare_sets_artifacts_when_manifest_and_tier_brief_ready()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefApproved =
            """{"objective":"bronze silver gold tier program"}""";
        state.Artifacts.CampaignDesignBriefProposed = state.Artifacts.CampaignDesignBriefApproved;
        state.Artifacts.PointAccountManifest =
            """{"schemaVersion":1,"items":[{"id":"pat-tqp","displayLabel":"TQP","role":"tierQualification"}]}""";

        Assert.True(JourneyPatternPrepResolver.TryPrepare(state));
        Assert.True(state.Artifacts.JourneyPatternPrepComplete);
        Assert.Equal("tier-navigation-point-balance", state.Artifacts.JourneyPatternId);
        Assert.Contains("pat-tqp", state.Artifacts.JourneyPatternSkeletonJson!, StringComparison.Ordinal);
    }

    [Fact]
    public void TryPrepare_skips_when_already_complete()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.JourneyPatternPrepComplete = true;
        state.Artifacts.CampaignDesignBriefApproved = """{"objective":"gold tier"}""";
        state.Artifacts.PointAccountManifest =
            """{"schemaVersion":1,"items":[{"id":"pat-1","displayLabel":"TQP","role":"tierQualification"}]}""";

        Assert.False(JourneyPatternPrepResolver.TryPrepare(state));
    }
}
