using CampaignContextAudit.Analysis;
using CampaignContextAudit.Reporting;
using Xunit;

namespace CampaignContextAudit.Tests;

public class RubricGraderTests
{
    [Theory]
    [InlineData(4.0, "A")]
    [InlineData(3.67, "A")]
    [InlineData(3.66, "A-")]
    [InlineData(3.34, "A-")]
    [InlineData(3.0, "B+")]
    [InlineData(2.67, "B")]
    [InlineData(2.34, "B-")]
    [InlineData(2.0, "C+")]
    [InlineData(1.67, "C")]
    [InlineData(1.34, "C-")]
    [InlineData(1.0, "D+")]
    [InlineData(0.67, "D")]
    [InlineData(0.34, "D-")]
    [InlineData(0.0, "F")]
    public void LetterGradeConverter_maps_score_bands(double score, string expected) =>
        Assert.Equal(expected, LetterGradeConverter.ToLetter(score));

    [Fact]
    public void EmptyTrace_defaultsToB_allDimensions()
    {
        var scorecard = RubricGrader.Grade([], null, []);
        Assert.All(scorecard.Dimensions, d => Assert.Equal("B", d.Grade));
        Assert.Equal("B", scorecard.Competency.Grade);
        Assert.Equal("B", scorecard.Efficiency.Grade);
    }

    [Fact]
    public void JourneyEmptyAndBloat_lowersCompetencyAndEfficiency()
    {
        var findings = new List<Finding>
        {
            new("JOURNEY_EMPTY_AT_SUCCESS", "blocking", "journey empty", ["33"]),
            new("TOOL_RESULT_BLOAT", "degrading", "list_models result is 29,798 chars, persisted verbatim into history.", ["2"]),
            new("TOOL_RESULT_BLOAT", "degrading", "validate_campaign result is 16,017 chars, persisted verbatim into history.", ["8"]),
        };
        var turns = new List<TurnBudget>
        {
            new(1, "CampaignJourney", 11000, 690, 4000, 15690, 1, 0, 0, 3923)
        };

        var scorecard = RubricGrader.Grade(findings, null, turns);

        var dim1 = scorecard.Dimensions.Single(d => d.DimensionId == 1);
        var dim6 = scorecard.Dimensions.Single(d => d.DimensionId == 6);
        Assert.True(dim1.Score <= 1.67, $"dim1 expected <= C, got {dim1.Grade} ({dim1.Score})");
        Assert.True(dim6.Score <= 2.0, $"dim6 expected <= C+, got {dim6.Grade} ({dim6.Score})");
        Assert.True(scorecard.Efficiency.Score <= 2.34, $"expected efficiency <= B-, got {scorecard.Efficiency.Grade} ({scorecard.Efficiency.Score})");
    }

    [Fact]
    public void RediscoveryAfterCreation_capsSelfKnowledge()
    {
        var findings = new List<Finding>
        {
            new("REDISCOVERY_AFTER_CREATION", "intent-breaking", "re-discovery", ["30"]),
        };
        var turns = new List<TurnBudget>
        {
            new(1, "EventModels", 10000, 5000, 0, 15000, 1, 0, 0, 3750)
        };

        var scorecard = RubricGrader.Grade(findings, null, turns);
        var dim4 = scorecard.Dimensions.Single(d => d.DimensionId == 4);
        Assert.True(dim4.Score <= 2.0, $"dim4 expected <= C+, got {dim4.Grade} ({dim4.Score})");
    }

    [Fact]
    public void DeliveryF_capsCompetencyAtDPlus()
    {
        var findings = new List<Finding>
        {
            new("TOOL_RESULT_BLOAT", "degrading", "list_models result is 7,498 chars, persisted verbatim into history.", ["6"]),
        };
        var turns = new List<TurnBudget>
        {
            new(1, "EventModels", 11000, 643, 4657, 16348, 1, 0, 0, 4087)
        };

        var uncapped = RubricGrader.Grade(findings, null, turns);
        var capped = RubricGrader.Grade(findings, null, turns, deliveryGrade: "F");

        Assert.True(uncapped.Competency.Score > 1.0);
        Assert.True(capped.Competency.Score <= 1.0);
        Assert.Equal("D+", capped.Competency.Grade);
        Assert.NotNull(capped.UncappedCompetency);
    }

    [Fact]
    public void DeliveryF_skipsNoBlockingBonus()
    {
        var findings = new List<Finding>();
        var turns = new List<TurnBudget>
        {
            new(1, "EventModels", 11000, 643, 4657, 16348, 1, 0, 0, 4087)
        };

        var scorecard = RubricGrader.Grade(findings, null, turns, deliveryGrade: "F");
        var dim5 = scorecard.Dimensions.Single(d => d.DimensionId == 5);

        Assert.DoesNotContain("No blocking outcome findings", dim5.Rationale);
    }
}
