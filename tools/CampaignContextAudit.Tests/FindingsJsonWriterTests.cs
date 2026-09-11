using CampaignContextAudit.Analysis;
using CampaignContextAudit.Models;
using CampaignContextAudit.Reporting;
using CampaignContextAudit.Transcript;
using Xunit;

namespace CampaignContextAudit.Tests;

public class FindingsJsonWriterTests
{
    [Fact]
    public void Round_trip_preserves_finding_codes()
    {
        var transcript = new LoadedTranscript(
            "conv.json",
            [new AgentMessageDoc { Sequence = 1, Role = "user", Content = "hi", ConversationId = "c1" }],
            []);
        var snapshot = new WorkflowSnapshot(
            CreationComplete: false,
            JourneyRuleSetCount: 0,
            WorkflowPhase: "Done",
            PatManifestCount: 0,
            VerificationPatCount: 0,
            UpsertFailedSinceValidate: true,
            LastRemediationPreview: null,
            UserRequestedNewEventModel: false,
            FetchFailed: false);
        var findings = new List<Finding>
        {
            new("CREATION_INCOMPLETE_AT_DONE", "blocking", "phase Done but creation incomplete", ["0"])
        };
        var turns = new List<TurnBudget>
        {
            new(1, "CampaignJourney", 9000, 45000, 12000, 66000, 1, 0, 0, 16500)
        };

        var report = FindingsJsonWriter.BuildReport(transcript, snapshot, findings, turns, null);
        var path = Path.Combine(Path.GetTempPath(), $"findings-{Guid.NewGuid():N}.json");
        try
        {
            FindingsJsonWriter.Write(path, report);
            var read = FindingsJsonWriter.Read(path);
            Assert.Equal("CREATION_INCOMPLETE_AT_DONE", read.Findings[0].Code);
            Assert.Equal(12000, read.BudgetByTurn[0].SessionChars);
            Assert.False(read.OutcomeSummary.CreationComplete);
            Assert.Equal(3, read.SchemaVersion);
            Assert.Empty(read.Rubric);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void SchemaV2_includes_rubric_when_provided()
    {
        var transcript = new LoadedTranscript(
            "conv.json",
            [new AgentMessageDoc { Sequence = 1, Role = "user", Content = "hi", ConversationId = "c1" }],
            []);
        var snapshot = new WorkflowSnapshot(
            CreationComplete: false,
            JourneyRuleSetCount: 0,
            WorkflowPhase: "CampaignJourney",
            PatManifestCount: 2,
            VerificationPatCount: 0,
            UpsertFailedSinceValidate: true,
            LastRemediationPreview: null,
            UserRequestedNewEventModel: false,
            FetchFailed: false);
        var findings = new List<Finding>
        {
            new("JOURNEY_EMPTY_AT_SUCCESS", "blocking", "journey empty", ["33"])
        };
        var turns = new List<TurnBudget>
        {
            new(1, "CampaignJourney", 9000, 45000, 12000, 66000, 1, 0, 0, 16500)
        };
        var rubric = RubricGrader.Grade(findings, snapshot, turns);
        var report = FindingsJsonWriter.BuildReport(transcript, snapshot, findings, turns, null, rubric);

        Assert.Equal(3, report.SchemaVersion);
        Assert.Equal(8, report.Rubric.Count);
        Assert.NotNull(report.OverallCompetency);
        Assert.NotNull(report.OverallEfficiency);
        Assert.False(string.IsNullOrWhiteSpace(report.OverallCompetency!.Grade));
    }

    [Fact]
    public void SchemaV3_includesDelivery()
    {
        var transcript = new LoadedTranscript(
            "conv.json",
            [new AgentMessageDoc { Sequence = 1, Role = "user", Content = "hi", ConversationId = "c1" }],
            []);
        var snapshot = new WorkflowSnapshot(
            CreationComplete: false,
            JourneyRuleSetCount: 0,
            WorkflowPhase: "EventModels",
            PatManifestCount: 3,
            VerificationPatCount: 0,
            UpsertFailedSinceValidate: true,
            LastRemediationPreview: null,
            UserRequestedNewEventModel: false,
            FetchFailed: false);
        var findings = new List<Finding>
        {
            new("CREATION_ABORTED", "blocking", "aborted", ["1"])
        };
        var turns = new List<TurnBudget>
        {
            new(1, "EventModels", 9000, 45000, 12000, 66000, 1, 0, 0, 16500)
        };
        var delivery = new DeliveryScorecard(
            "F", 0.25, "aborted", 480_000,
            CreationMilestoneIds.PointAccountTypes,
            CreationMilestoneIds.CampaignJourney,
            [new(CreationMilestoneIds.CampaignJourney, "Journey saved", false)]);
        var rubric = RubricGrader.Grade(findings, snapshot, turns, delivery.Grade);
        var report = FindingsJsonWriter.BuildReport(transcript, snapshot, findings, turns, null, rubric, delivery);

        Assert.Equal(3, report.SchemaVersion);
        Assert.NotNull(report.Delivery);
        Assert.Equal("F", report.Delivery!.Grade);
    }
}
