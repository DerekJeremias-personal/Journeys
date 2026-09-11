using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Xunit;

namespace Journeys.Tests.Workflow;

public class CampaignJourneyDeliveryGuardTests
{
    public CampaignJourneyDeliveryGuardTests()
    {
        CampaignJourneyDeliveryGuard.Enabled = true;
    }

    [Fact]
    public void ShouldBlockContinuation_when_journey_approval_pending()
    {
        var s = ReadyForJourney();
        s.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.Journey;
        Assert.True(CampaignJourneyDeliveryGuard.ShouldBlockContinuation(s));
    }

    [Fact]
    public void ShouldBlockContinuation_when_validation_stalled()
    {
        var s = ReadyForJourney();
        s.Artifacts.ValidationStalled = true;
        Assert.True(CampaignJourneyDeliveryGuard.ShouldBlockContinuation(s));
    }

    [Fact]
    public void BlocksJourneyMutators_when_checkpoint_or_stall()
    {
        var s = ReadyForJourney();
        s.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.Journey;
        Assert.True(CampaignJourneyDeliveryGuard.BlocksJourneyMutators(s));

        s.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.None;
        s.Artifacts.ValidationStalled = true;
        Assert.True(CampaignJourneyDeliveryGuard.BlocksJourneyMutators(s));
    }

    [Fact]
    public void TrySetJourneyEntryCheckpoint_sets_gate_when_prerequisites_met()
    {
        var s = ReadyForJourney();
        CampaignJourneyDeliveryGuard.TrySetJourneyEntryCheckpoint(s, userMessageThisTurn: null);
        Assert.Equal(CampaignWorkflowApprovalKind.Journey, s.Artifacts.AwaitingApproval);
    }

    [Fact]
    public void TrySetJourneyEntryCheckpoint_skips_when_proceed_intent()
    {
        var s = ReadyForJourney();
        CampaignJourneyDeliveryGuard.TrySetJourneyEntryCheckpoint(s, "continue to the journey");
        Assert.Equal(CampaignWorkflowApprovalKind.None, s.Artifacts.AwaitingApproval);
    }

    [Fact]
    public void TrySetJourneyEntryCheckpoint_skips_when_journey_already_saved()
    {
        var s = ReadyForJourney();
        s.Artifacts.CreationSnapshot = """{"journeyRuleSetCount":2,"campaignId":"c1"}""";
        CampaignJourneyDeliveryGuard.TrySetJourneyEntryCheckpoint(s, null);
        Assert.Equal(CampaignWorkflowApprovalKind.None, s.Artifacts.AwaitingApproval);
    }

    [Fact]
    public void ClearUserTurnGuards_clears_journey_gate_and_stall()
    {
        var s = ReadyForJourney();
        s.Artifacts.AwaitingApproval = CampaignWorkflowApprovalKind.Journey;
        s.Artifacts.ValidationStalled = true;
        s.Artifacts.ValidationStallCycleCount = 2;
        CampaignJourneyDeliveryGuard.ClearUserTurnGuards(s);
        Assert.Equal(CampaignWorkflowApprovalKind.None, s.Artifacts.AwaitingApproval);
        Assert.False(s.Artifacts.ValidationStalled);
        Assert.Equal(0, s.Artifacts.ValidationStallCycleCount);
    }

    private static CampaignWorkflowState ReadyForJourney()
    {
        var s = CampaignWorkflowState.CreateDefault("t", "u", "c");
        s.Phase = CampaignWorkflowPhase.CampaignJourney;
        s.ModelGatePassed = true;
        s.Artifacts.CampaignDesignBriefApproved = """{"objective":"tier loyalty"}""";
        s.Artifacts.CampaignShellRef = """{"campaignId":"tier-loyalty-program"}""";
        s.Artifacts.PointAccountManifest = """[{"id":"p1","name":"tqp"}]""";
        return s;
    }
}
