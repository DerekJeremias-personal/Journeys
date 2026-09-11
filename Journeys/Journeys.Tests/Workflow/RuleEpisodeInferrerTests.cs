using Journeys.Core.Models;
using Journeys.Core.Workflow;
using Xunit;

namespace Journeys.Tests.Workflow;

public class RuleEpisodeInferrerTests
{
    [Fact]
    public void Infer_tier_brief_returns_TierLadder()
    {
        var brief = """{"objective":"Bronze Silver Gold tiers by point balance"}""";
        Assert.Equal(RuleEpisode.TierLadder, RuleEpisodeInferrer.Infer(brief, null, null));
    }

    [Fact]
    public void Infer_generic_brief_returns_Generic()
    {
        var brief = """{"objective":"Welcome email campaign"}""";
        Assert.Equal(RuleEpisode.Generic, RuleEpisodeInferrer.Infer(brief, null, null));
    }

    [Fact]
    public void Infer_simple_earn_brief_returns_SimpleEarn()
    {
        var brief = """{"objective":"Earn 1 point per dollar spent"}""";
        Assert.Equal(RuleEpisode.SimpleEarn, RuleEpisodeInferrer.Infer(brief, null, null));
    }
}
