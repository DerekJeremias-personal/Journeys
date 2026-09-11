using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Workflow;

public class PatMutatorDeferralCoachTests
{
    [Fact]
    public void TryGetHint_gate_open_empty_manifest_deferred_retry()
    {
        var state = GateOpenEmptyManifest();
        state.Artifacts.DeferredMutatorRetry = true;

        var hint = PatMutatorDeferralCoach.TryGetHint(state);

        Assert.NotNull(hint);
        Assert.Contains("OPEN", hint!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("filtered", hint!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do NOT ask the user to create PATs via HTTP", hint!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("upsert_point_account_type", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryGetHint_absent_when_gate_closed()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.CampaignKind = CampaignWorkflowKind.EventDriven;
        state.Artifacts.CampaignDesignBriefApproved = """{"objective":"x"}""";
        state.Artifacts.DeferredMutatorRetry = true;
        Assert.True(CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(state));

        Assert.Null(PatMutatorDeferralCoach.TryGetHint(state));
    }

    [Fact]
    public void GetCoachHint_pat_deferral_beats_journey_when_shell_exists()
    {
        var state = GateOpenEmptyManifest();
        state.Phase = CampaignWorkflowPhase.CampaignJourney;
        state.Artifacts.CampaignShellRef = """{"campaignId":"camp-1"}""";
        state.Artifacts.DeferredMutatorRetry = true;

        var hint = CampaignWorkflowStepManager.GetCoachHint(state);

        Assert.NotNull(hint);
        Assert.Contains("upsert_point_account_type", hint!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("journey.ruleSetCount is 0", hint!, StringComparison.OrdinalIgnoreCase);
    }

    private static CampaignWorkflowState GateOpenEmptyManifest()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.CampaignKind = CampaignWorkflowKind.EventDriven;
        state.Artifacts.CampaignDesignBriefProposed = """{"objective":"tier loyalty"}""";
        state.Artifacts.CampaignDesignBriefApproved = state.Artifacts.CampaignDesignBriefProposed;
        state.Artifacts.ResolvedEventModelContracts =
            """[{"schemaVersion":1,"eventModelId":"evt-1","eventModelName":"Order","isProcessEventEligible":true}]""";
        Assert.False(CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(state));
        return state;
    }
}
