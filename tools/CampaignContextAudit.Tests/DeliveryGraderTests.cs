using CampaignContextAudit.Analysis;
using CampaignContextAudit.Models;
using Xunit;

namespace CampaignContextAudit.Tests;

public class DeliveryGraderTests
{
    [Fact]
    public void Creation_complete_without_verification_caps_at_B()
    {
        var snapshot = new WorkflowSnapshot(
            CreationComplete: true,
            JourneyRuleSetCount: 3,
            WorkflowPhase: "Done",
            PatManifestCount: 2,
            VerificationPatCount: 0,
            UpsertFailedSinceValidate: null,
            LastRemediationPreview: null,
            UserRequestedNewEventModel: null,
            FetchFailed: null);
        var milestones = new CreationMilestoneEvaluation(
            [
                new(CreationMilestoneIds.CampaignJourney, "Journey saved", true),
                new(CreationMilestoneIds.Verification, "Verification", false),
            ],
            CreationMilestoneIds.CampaignJourney,
            CreationMilestoneIds.Verification,
            HasMilestoneGap: true);

        var delivery = DeliveryGrader.Grade([], snapshot, milestones, 120_000, [], abortMs: 300_000);

        Assert.Equal("B", delivery.Grade);
    }

    [Fact]
    public void SeventyNineAc7b_band_scores_F()
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
        var milestones = new CreationMilestoneEvaluation(
            [
                new(CreationMilestoneIds.PointAccountTypes, "Point account types", true),
                new(CreationMilestoneIds.CampaignJourney, "Journey saved", false),
            ],
            CreationMilestoneIds.PointAccountTypes,
            CreationMilestoneIds.CampaignJourney,
            HasMilestoneGap: true);
        var findings = new List<Finding>
        {
            new("CREATION_ABORTED", "blocking", "aborted", ["1"]),
            new("VALIDATION_UPSERT_LOOP", "blocking", "loop", ["13", "41"]),
        };

        var delivery = DeliveryGrader.Grade(findings, snapshot, milestones, 480_000, [], abortMs: 300_000);

        Assert.Equal("F", delivery.Grade);
    }
}
