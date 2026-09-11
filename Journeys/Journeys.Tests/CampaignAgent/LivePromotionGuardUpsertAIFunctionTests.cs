using System.Threading;
using System.Threading.Tasks;
using Journeys.API.CampaignAgent;
using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Microsoft.Extensions.AI;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public class LivePromotionGuardUpsertAIFunctionTests
{
    private sealed class SpyUpsertFunction : AIFunction
    {
        public bool Invoked { get; private set; }

        public SpyUpsertFunction() => Name = "upsert_campaign";

        public override string Name { get; }

        protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            Invoked = true;
            return ValueTask.FromResult<object?>("""{"campaign":{"id":"c1","status":"Live"}}""");
        }
    }

    [Fact]
    public async Task Invoke_blocks_live_upsert_without_user_approval()
    {
        var inner = new SpyUpsertFunction();
        var state = DraftComplete();
        var guard = new LivePromotionGuardUpsertAIFunction(inner, () => state);

        var args = new AIFunctionArguments(new Dictionary<string, object?>
        {
            ["tenantId"] = "primo",
            ["campaignJson"] = """{"status":"Live","id":"c1"}"""
        });

        var result = await guard.InvokeAsync(args);

        Assert.False(inner.Invoked);
        Assert.Equal(CampaignWorkflowApprovalKind.LivePromotion, state.Artifacts.AwaitingApproval);
        var text = Assert.IsType<string>(result);
        Assert.Contains("LIVE_PROMOTION_REQUIRES_APPROVAL", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invoke_delegates_when_user_approved_live_promotion()
    {
        var inner = new SpyUpsertFunction();
        var state = DraftComplete();
        LivePromotionGuard.ApplyUserMessage(state, "promote to Live");
        var guard = new LivePromotionGuardUpsertAIFunction(inner, () => state);

        var args = new AIFunctionArguments(new Dictionary<string, object?>
        {
            ["tenantId"] = "primo",
            ["campaignJson"] = """{"status":"Live","id":"c1"}"""
        });

        await guard.InvokeAsync(args);

        Assert.True(inner.Invoked);
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
