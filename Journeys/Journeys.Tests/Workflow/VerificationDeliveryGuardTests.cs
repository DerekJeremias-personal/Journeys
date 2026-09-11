using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Xunit;

namespace Journeys.Tests.Workflow;

public class VerificationDeliveryGuardTests
{
    [Fact]
    public void ShouldSurfaceVerificationCoach_when_Done_and_verify_incomplete_and_test_intent()
    {
        var s = EventDrivenCompleteUnverified();
        s.Phase = CampaignWorkflowPhase.Done;
        s.Artifacts.VerificationUserTestIntentThisTurn = true;

        Assert.True(VerificationDeliveryGuard.ShouldSurfaceVerificationCoach(s));
    }

    [Fact]
    public void ShouldContinueForVerification_when_test_intent_and_no_tools_yet()
    {
        var s = EventDrivenCompleteUnverified();
        s.Artifacts.VerificationUserTestIntentThisTurn = true;
        s.Artifacts.VerificationBlockedNoAllowlist = false;
        s.Artifacts.VerificationToolsInvokedThisSegment = false;

        Assert.True(VerificationDeliveryGuard.ShouldContinueForVerification(s, continuationHopsUsed: 0));
    }

    [Fact]
    public void ShouldContinueForVerification_false_when_blocked_no_allowlist()
    {
        var s = EventDrivenCompleteUnverified();
        s.Artifacts.VerificationUserTestIntentThisTurn = true;
        s.Artifacts.VerificationBlockedNoAllowlist = true;

        Assert.False(VerificationDeliveryGuard.ShouldContinueForVerification(s, 0));
    }

    [Fact]
    public void IsVerificationSatisfiedForWorkflowComplete_event_driven_requires_exit_criteria()
    {
        var s = EventDrivenCompleteUnverified();
        Assert.False(VerificationDeliveryGuard.IsVerificationSatisfiedForWorkflowComplete(s));
    }

    [Fact]
    public void IsVerificationSatisfiedForWorkflowComplete_tag_first_uses_journey_exit()
    {
        var s = EventDrivenCompleteUnverified();
        s.CampaignKind = CampaignWorkflowKind.TagFirst;
        s.Artifacts.CreationSnapshot = """
            {"schemaVersion":1,"creationComplete":true,"campaignId":"c1","journeyRuleSetCount":2}
            """;

        Assert.True(VerificationDeliveryGuard.IsVerificationSatisfiedForWorkflowComplete(s));
    }

    private static CampaignWorkflowState EventDrivenCompleteUnverified()
    {
        var s = CampaignWorkflowState.CreateDefault("primo", "u", "c");
        s.CampaignKind = CampaignWorkflowKind.EventDriven;
        s.Artifacts.CampaignDesignBriefApproved = """{"objective":"x"}""";
        s.Artifacts.CreationSnapshot = """
            {"schemaVersion":1,"creationComplete":true,"campaignId":"camp-1","journeyRuleSetCount":3}
            """;
        return s;
    }
}
