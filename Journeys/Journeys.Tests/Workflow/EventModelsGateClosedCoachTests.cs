using Journeys.API.CampaignAgent.Workflow;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Xunit;

namespace Journeys.Tests.Workflow;

public class EventModelsGateClosedCoachTests
{
    [Fact]
    public void TryGetHint_gate_closed_mentions_filtered_not_missing()
    {
        var state = InEventModelsGateClosed();

        var hint = EventModelsGateClosedCoach.TryGetHint(state);

        Assert.NotNull(hint);
        Assert.Contains("CLOSED", hint!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("filtered", hint!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not missing", hint!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("save_model", hint!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no_resolved_event_model", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryGetHint_gate_closed_does_not_suggest_http_pat_creation()
    {
        var state = InEventModelsGateClosed();

        var hint = EventModelsGateClosedCoach.TryGetHint(state);

        Assert.NotNull(hint);
        Assert.Contains("Do NOT ask the user to create PATs via HTTP", hint!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("POST /api", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryGetHint_absent_when_gate_open()
    {
        var state = InEventModelsGateClosed();
        state.Artifacts.ResolvedEventModelContracts =
            """[{"schemaVersion":1,"eventModelId":"evt-1","eventModelName":"Order","eventModelType":"loyalty","isProcessEventEligible":true}]""";

        Assert.Null(EventModelsGateClosedCoach.TryGetHint(state));
    }

    [Fact]
    public void TryGetHint_defers_to_readiness_for_wrapper_errors()
    {
        var state = InEventModelsGateClosed();
        state.Artifacts.ResolvedEventModelContracts =
            """[{"schemaVersion":1,"eventModelId":"evt-1","eventModelName":"Review","eventModelType":"loyalty","wrapperModelId":"w1","isProcessEventEligible":true}]""";
        state.Artifacts.WrapperContractValidation =
            """[{"wrapperModelId":"w1","wrapperModelName":"ReviewAndRuleState","errors":["Missing required wrapper attribute 'accountid'."],"warnings":[]}]""";

        Assert.Null(EventModelsGateClosedCoach.TryGetHint(state));
    }

    [Fact]
    public void TryGetHint_appends_correction_when_prior_deferral_summary()
    {
        var state = InEventModelsGateClosed();
        state.Artifacts.LastToolRemediationSummary =
            "UpsertPointAccountType is not surfaced in this session.";

        var hint = EventModelsGateClosedCoach.TryGetHint(state);

        Assert.NotNull(hint);
        Assert.Contains("filtered by the Events gate", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetCoachHint_wins_over_journey_and_validation_when_gate_closed()
    {
        var state = InEventModelsGateClosed();
        state.Phase = CampaignWorkflowPhase.CampaignJourney;
        state.Artifacts.CampaignShellRef = """{"campaignId":"camp-1"}""";
        state.Artifacts.LastToolRemediationSummary =
            "validate_campaign reported hard errors — fix validation.errors and follow nextSteps before upsert_campaign.";

        var hint = CampaignWorkflowStepManager.GetCoachHint(state);

        Assert.NotNull(hint);
        Assert.Contains("Events gate is CLOSED", hint!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("journey.ruleSetCount is 0", hint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetCoachHint_still_surfaces_wrapper_readiness_over_gate_closed()
    {
        var state = InEventModelsGateClosed();
        state.Artifacts.ResolvedEventModelContracts =
            """[{"schemaVersion":1,"eventModelId":"evt-1","eventModelName":"Review","eventModelType":"loyalty","wrapperModelId":"w1","isProcessEventEligible":true}]""";
        state.Artifacts.WrapperContractValidation =
            """[{"wrapperModelId":"w1","wrapperModelName":"ReviewAndRuleState","errors":["Missing required wrapper attribute 'accountid'."],"warnings":[]}]""";

        var hint = CampaignWorkflowStepManager.GetCoachHint(state);

        Assert.NotNull(hint);
        Assert.Contains("wrapper invalid", hint!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Events gate is CLOSED", hint!, StringComparison.OrdinalIgnoreCase);
    }

    private static CampaignWorkflowState InEventModelsGateClosed()
    {
        var state = CampaignWorkflowState.CreateDefault("t", "u", "c");
        state.Phase = CampaignWorkflowPhase.EventModels;
        state.CampaignKind = CampaignWorkflowKind.EventDriven;
        state.Artifacts.CampaignDesignBriefProposed = """{"objective":"tier loyalty"}""";
        state.Artifacts.CampaignDesignBriefApproved = state.Artifacts.CampaignDesignBriefProposed;
        Assert.True(CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(state));
        return state;
    }
}
