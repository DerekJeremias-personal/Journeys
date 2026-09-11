using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Xunit;

namespace Journeys.Tests.Workflow;

public class CampaignWorkflowChecklistTests
{
    [Fact]
    public void IsWorkflowComplete_false_when_journey_done_but_verify_incomplete_event_driven()
    {
        var s = EventDrivenCreationComplete();
        Assert.False(CampaignWorkflowChecklist.IsWorkflowComplete(s));
    }

    [Fact]
    public void IsWorkflowComplete_true_when_verify_met_event_driven()
    {
        var s = EventDrivenCreationComplete();
        s.Artifacts.VerificationRecord = """{"evaluatedCampaigns":["c1"],"status":"processed"}""";
        Assert.True(CampaignWorkflowChecklist.IsWorkflowComplete(s));
    }

    [Fact]
    public void IsWorkflowComplete_true_for_tag_first_when_journey_complete()
    {
        var s = EventDrivenCreationComplete();
        s.CampaignKind = CampaignWorkflowKind.TagFirst;
        Assert.True(CampaignWorkflowChecklist.IsWorkflowComplete(s));
    }

    private static CampaignWorkflowState EventDrivenCreationComplete()
    {
        var s = CampaignWorkflowState.CreateDefault("primo", "u", "c");
        s.CampaignKind = CampaignWorkflowKind.EventDriven;
        s.UserSkippedEventModels = true;
        s.Artifacts.CampaignDesignBriefApproved = """{"objective":"x"}""";
        s.Artifacts.CampaignDesignBriefProposed = s.Artifacts.CampaignDesignBriefApproved;
        s.Artifacts.CampaignShellRef = """{"campaignId":"camp-1"}""";
        s.Artifacts.PointAccountManifest = """{"items":[{"id":"pat-1","role":"spendable"}]}""";
        s.Artifacts.CreationSnapshot = """
            {"schemaVersion":1,"creationComplete":true,"campaignId":"camp-1","journeyRuleSetCount":3,"pointAccountTypes":[{"id":"pat-1"}]}
            """;
        return s;
    }
}
