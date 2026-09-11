using Journeys.Core.Models;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.RulesEngine.Outcomes;
using Journeys.Core.RulesEngine;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.Services;
using Xunit;

namespace Journeys.Tests.Services;

public class CampaignAssistantDigestBuilderTests
{
    [Fact]
    public void Build_counts_nodes_rule_sets_and_outcome_kinds()
    {
        var inner = new JourneyNode(
            "inner",
            new List<RuleSet>
            {
                new RuleSet
                {
                    Name = "rs2",
                    Outcomes = new List<OutcomeBase> { new DepositPointsOutcome() }
                }
            },
            "n2",
            "root",
            null,
            null);

        var root = new JourneyNode(
            "root",
            new List<RuleSet>
            {
                new RuleSet
                {
                    Name = "rs1",
                    Outcomes = new List<OutcomeBase> { new TagOutcome { Type = "a", EntityId = "e", Name = "n", Value = "v" } }
                }
            },
            "n1",
            "n1",
            null,
            new List<JourneyNode> { inner });

        var campaign = new Campaign(
            "ext",
            CampaignStatusStrings.Draft,
            "n",
            new List<string> { "model-a", "model-b" },
            DateTimeOffset.UtcNow,
            null,
            null,
            root,
            "t1",
            "cid-1");

        var digest = CampaignAssistantDigestBuilder.Build("t1", campaign, "etag-1");

        Assert.Equal(1, digest.SchemaVersion);
        Assert.Equal("t1", digest.TenantId);
        Assert.Equal("cid-1", digest.CampaignId);
        Assert.Equal("etag-1", digest.Etag);
        Assert.Equal(2, digest.EventModelBindings.Count);
        Assert.Equal("model-a", digest.EventModelBindings[0].ModelId);
        Assert.Equal("passed", digest.Validation.TierA);
        Assert.Equal(2, digest.JourneyDigest.JourneyNodeCount);
        Assert.Equal(2, digest.JourneyDigest.RuleSetCount);
        Assert.Equal(2, digest.JourneyDigest.OutcomeKindCounts.Count);
        Assert.True(digest.JourneyDigest.OutcomeKindCounts.ContainsKey(OutcomeKindDiscriminators.DepositPointsOutcome));
        Assert.True(digest.JourneyDigest.OutcomeKindCounts.ContainsKey(OutcomeKindDiscriminators.TagOutcome));
    }
}
