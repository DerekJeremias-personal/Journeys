using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public class JourneyPatternSkeletonBuilderTests
{
    [Fact]
    public void TryBuild_substitutes_tier_qualification_pat_id()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.PointAccountManifest =
            """{"schemaVersion":1,"items":[{"id":"11111111-1111-1111-1111-111111111111","displayLabel":"TQP","role":"tierQualification"}]}""";

        var skeleton = JourneyPatternSkeletonBuilder.TryBuild(
            JourneyPatternClassifier.TierNavigationPointBalance,
            state);

        Assert.NotNull(skeleton);
        Assert.Contains("11111111-1111-1111-1111-111111111111", skeleton!, StringComparison.Ordinal);
        Assert.DoesNotContain("<tqp-pat-id>", skeleton!, StringComparison.Ordinal);
        Assert.Contains("SimpleNavigationCriteria", skeleton!, StringComparison.Ordinal);
    }
}
