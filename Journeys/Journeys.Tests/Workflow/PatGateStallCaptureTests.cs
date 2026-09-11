using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Xunit;

namespace Journeys.Tests.Workflow;

public class PatGateStallCaptureTests
{
    [Fact]
    public void TryCapture_gate_closed_deferral_prose_arms_pending()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.CampaignDesignBriefProposed = "{}";
        s.Artifacts.CampaignDesignBriefProposed = "{}";
        s.Artifacts.CampaignDesignBriefApproved = "{}";
        s.CampaignKind = CampaignWorkflowKind.EventDriven;
        var text = "upsert_point_account_type is not on my tool surface.";

        Assert.True(PatGateStallCapture.TryCaptureFromAssistantText(s, text));
        Assert.True(s.Artifacts.PatUpsertPending);
        Assert.True(s.Artifacts.PatHttpDeferralShown);
    }

    [Fact]
    public void TryCapture_gate_open_does_not_arm()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.CampaignDesignBriefApproved = "{}";
        s.UserSkippedEventModels = true;
        var text = "upsert_point_account_type not on my tool surface";

        Assert.False(PatGateStallCapture.TryCaptureFromAssistantText(s, text));
        Assert.False(s.Artifacts.PatUpsertPending);
    }

    [Fact]
    public void TryCapture_without_pat_mention_does_not_arm()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Artifacts.CampaignDesignBriefProposed = "{}";
        s.Artifacts.CampaignDesignBriefApproved = "{}";
        s.CampaignKind = CampaignWorkflowKind.EventDriven;
        var text = "upsert_campaign is not on my tool surface";

        Assert.False(PatGateStallCapture.TryCaptureFromAssistantText(s, text));
        Assert.False(s.Artifacts.PatUpsertPending);
    }
}
