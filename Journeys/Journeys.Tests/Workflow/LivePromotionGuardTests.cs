using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Workflow;

public class LivePromotionGuardTests
{
    [Fact]
    public void IsLivePromotionAttempt_detects_status_live_on_draft_snapshot()
    {
        var json = """{"status":"Live","id":"c1","name":"Test"}""";
        var state = DraftComplete();
        Assert.True(LivePromotionGuard.IsLivePromotionAttempt(state, json));
    }

    [Fact]
    public void IsLivePromotionAttempt_false_when_already_live()
    {
        var json = """{"status":"Live","id":"c1"}""";
        var state = DraftComplete();
        CreationSnapshotArtifact.MergeFromJourneyUpsert(state, "c1", CampaignStatusStrings.Live, 1, 1);
        Assert.False(LivePromotionGuard.IsLivePromotionAttempt(state, json));
    }

    [Fact]
    public void ShouldBlockLivePromotion_without_user_phrase()
    {
        var state = DraftComplete();
        var json = """{"status":"live","id":"c1"}""";
        Assert.True(LivePromotionGuard.ShouldBlockLivePromotion(state, json));
    }

    [Fact]
    public void ShouldBlockLivePromotion_false_after_user_promote_phrase()
    {
        var state = DraftComplete();
        LivePromotionGuard.ApplyUserMessage(state, "please promote to Live when ready");
        var json = """{"status":"Live","id":"c1"}""";
        Assert.False(LivePromotionGuard.ShouldBlockLivePromotion(state, json));
    }

    [Fact]
    public void ApplyBlock_sets_awaiting_approval_and_remediation()
    {
        var state = DraftComplete();
        LivePromotionGuard.ApplyBlock(state);
        Assert.Equal(CampaignWorkflowApprovalKind.LivePromotion, state.Artifacts.AwaitingApproval);
        Assert.Contains("process_event", state.Artifacts.LastToolRemediationSummary!, StringComparison.OrdinalIgnoreCase);
    }

    private static CampaignWorkflowState DraftComplete()
    {
        var s = CampaignWorkflowState.CreateDefault("primo", "u", "c");
        s.CampaignKind = CampaignWorkflowKind.EventDriven;
        s.Phase = CampaignWorkflowPhase.Verification;
        s.Artifacts.CampaignShellRef = """{"campaignId":"c1"}""";
        CreationSnapshotArtifact.MergeFromJourneyUpsert(s, "c1", CampaignStatusStrings.Draft, 2, 2);
        return s;
    }
}
