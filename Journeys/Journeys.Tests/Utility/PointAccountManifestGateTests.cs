using Journeys.Core.Models;
using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Utility;

public class PointAccountManifestGateTests
{
    [Fact]
    public void IsSatisfied_empty_manifest_false()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        Assert.False(PointAccountManifestGate.IsSatisfied(state, out var missing));
        Assert.Contains("any", missing);
    }

    [Fact]
    public void IsSatisfied_single_pat_non_tier_brief_true()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefApproved =
            """{"objective":"Earn points on purchase","mechanic":"earn on order"}""";

        PointAccountManifestBuilder.AppendFromPatResult(state,
            """{"id":"pat-1","name":"spend","ledgerType":"Spendable","isSpendable":true}""");

        Assert.False(PointAccountManifestGate.RequiresDualBucket(state));
        Assert.True(PointAccountManifestGate.IsSatisfied(state, out var missing));
        Assert.Empty(missing);
    }

    [Fact]
    public void IsSatisfied_milestone_brief_requires_dual_roles()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Artifacts.CampaignDesignBriefApproved =
            """{"mechanic":"Spend threshold milestones with tier rewards"}""";

        Assert.True(PointAccountManifestGate.RequiresDualBucket(state));

        PointAccountManifestBuilder.AppendFromPatResult(state,
            """{"id":"sp","name":"user_spendable","ledgerType":"Spendable","isSpendable":true}""");
        Assert.False(PointAccountManifestGate.IsSatisfied(state, out var missingOne));
        Assert.Contains("tierQualification", missingOne);

        PointAccountManifestBuilder.AppendFromPatResult(state,
            """{"id":"tq","name":"user_tier_qualification","ledgerType":"NonSpendable","isSpendable":false}""");
        Assert.True(PointAccountManifestGate.IsSatisfied(state, out var missingBoth));
        Assert.Empty(missingBoth);
    }
}
