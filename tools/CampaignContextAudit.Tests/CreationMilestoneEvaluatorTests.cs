using CampaignContextAudit.Analysis;
using CampaignContextAudit.Models;
using Xunit;

namespace CampaignContextAudit.Tests;

public class CreationMilestoneEvaluatorTests
{
    [Fact]
    public void PatManifest_reachesPointAccountTypes()
    {
        var snapshot = new WorkflowSnapshot(
            CreationComplete: false,
            JourneyRuleSetCount: 0,
            WorkflowPhase: "EventModels",
            PatManifestCount: 3,
            VerificationPatCount: 0,
            UpsertFailedSinceValidate: null,
            LastRemediationPreview: null,
            UserRequestedNewEventModel: null,
            FetchFailed: null);
        var timeline = new List<ToolEvent>();

        var eval = CreationMilestoneEvaluator.Evaluate(snapshot, [], timeline, null);

        var pat = eval.Milestones.Single(m => m.Id == CreationMilestoneIds.PointAccountTypes);
        Assert.True(pat.Reached);
        Assert.Equal(CreationMilestoneIds.PointAccountTypes, eval.HighestReached);
    }

    [Fact]
    public void ValidateTools_expectsCampaignJourney()
    {
        var snapshot = new WorkflowSnapshot(
            CreationComplete: false,
            JourneyRuleSetCount: 0,
            WorkflowPhase: "EventModels",
            PatManifestCount: 0,
            VerificationPatCount: 0,
            UpsertFailedSinceValidate: null,
            LastRemediationPreview: null,
            UserRequestedNewEventModel: null,
            FetchFailed: null);
        var timeline = new List<ToolEvent>
        {
            new(13, "validate_campaign", "c1", 1000),
            new(41, "validate_campaign", "c2", 1000),
        };

        var eval = CreationMilestoneEvaluator.Evaluate(snapshot, [], timeline, null);

        Assert.Equal(CreationMilestoneIds.CampaignJourney, eval.ExpectedMinimum);
        Assert.True(eval.HasMilestoneGap);
    }
}
