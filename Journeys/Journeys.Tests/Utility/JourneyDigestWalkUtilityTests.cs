using Journeys.Core.RulesEngine;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.RulesEngine.Outcomes;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Utility;

public class JourneyDigestWalkUtilityTests
{
    [Fact]
    public void Walk_collects_node_rule_outcome_and_pat_ids()
    {
        const string patId = "829eff38-6591-4c39-bfbd-db52c3256b53";
        var root = new JourneyNode(
            "root",
            new List<RuleSet>
            {
                new RuleSet
                {
                    Name = "rs1",
                    Outcomes = new List<OutcomeBase>
                    {
                        new DepositPointsOutcome
                        {
                            AffectedPointAccountTypeIds = new List<string> { patId }
                        }
                    }
                }
            },
            "n1",
            "n1",
            null,
            null);

        var result = JourneyDigestWalkUtility.Walk(root);

        Assert.Equal(1, result.JourneyNodeCount);
        Assert.Equal(1, result.RuleSetCount);
        Assert.Single(result.OutcomeKindCounts);
        Assert.Equal(1, result.OutcomeKindCounts[OutcomeKindDiscriminators.DepositPointsOutcome]);
        Assert.True(result.ReferencedPatIds.ContainsKey(patId));
        Assert.Contains(
            OutcomeKindDiscriminators.DepositPointsOutcome,
            result.ReferencedPatIds[patId]);
    }

    [Fact]
    public void Walk_null_journey_returns_zeros()
    {
        var result = JourneyDigestWalkUtility.Walk(null);

        Assert.Equal(0, result.JourneyNodeCount);
        Assert.Equal(0, result.RuleSetCount);
        Assert.Empty(result.OutcomeKindCounts);
        Assert.Empty(result.ReferencedPatIds);
    }
}
