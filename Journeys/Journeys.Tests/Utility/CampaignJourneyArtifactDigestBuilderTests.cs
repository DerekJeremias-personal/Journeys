using Journeys.Core.Models;
using Journeys.Core.RulesEngine;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.RulesEngine.Outcomes;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Utility;

public class CampaignJourneyArtifactDigestBuilderTests
{
    [Fact]
    public void BuildFromCampaign_pat_in_manifest_sets_inManifest_true()
    {
        var campaign = CreateCampaignWithPatRefs("pat-a");

        var digest = CampaignJourneyArtifactDigestBuilder.BuildFromCampaign(campaign, new[] { "pat-a" });

        var patRef = Assert.Single(digest.ReferencedPointAccountTypes);
        Assert.Equal("pat-a", patRef.PointAccountTypeId);
        Assert.True(patRef.InManifest);
        Assert.Empty(digest.UnresolvedPatIds);
        Assert.Contains(OutcomeKindDiscriminators.DepositPointsOutcome, patRef.OutcomeKinds);
        Assert.Equal(1, patRef.UsageCount);
    }

    [Fact]
    public void BuildFromCampaign_pat_not_in_manifest_is_unresolved()
    {
        var campaign = CreateCampaignWithPatRefs("pat-a", "pat-missing");

        var digest = CampaignJourneyArtifactDigestBuilder.BuildFromCampaign(campaign, new[] { "pat-a" });

        Assert.Contains("pat-missing", digest.UnresolvedPatIds);
        Assert.DoesNotContain("pat-a", digest.UnresolvedPatIds);

        var inManifest = digest.ReferencedPointAccountTypes.Single(r => r.PointAccountTypeId == "pat-a");
        Assert.True(inManifest.InManifest);

        var missing = digest.ReferencedPointAccountTypes.Single(r => r.PointAccountTypeId == "pat-missing");
        Assert.False(missing.InManifest);
    }

    private static Campaign CreateCampaignWithPatRefs(params string[] patIds)
    {
        var outcomes = patIds
            .Select(patId => (OutcomeBase)new DepositPointsOutcome
            {
                AffectedPointAccountTypeIds = new List<string> { patId }
            })
            .ToList();

        var root = new JourneyNode(
            "root",
            new List<RuleSet>
            {
                new RuleSet
                {
                    Name = "rs1",
                    Outcomes = outcomes
                }
            });

        return new Campaign(
            null,
            "Draft",
            "Test Campaign",
            null,
            DateTimeOffset.UtcNow,
            null,
            null,
            root,
            "tenant-1",
            "camp-1");
    }
}
