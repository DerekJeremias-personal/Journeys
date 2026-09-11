using Journeys.Core.Models;
using Journeys.Core.Utility;

namespace Journeys.API.CampaignAgent.Workflow;

/// <summary>
/// When the Events gate filters campaign mutators, steers the agent away from HTTP PAT deferral
/// and "tool not surfaced" narratives toward event-model resolution via save_model/get_model.
/// </summary>
public static class EventModelsGateClosedCoach
{
    private static readonly HashSet<string> SpecificReadinessBlockers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "wrapper_in_resolved_list",
            "wrapper_validation_missing",
            "wrapper_contract_invalid",
            "pending_event_model_unsatisfied",
            "event_model_reuse_confirm_pending",
            "event_model_not_process_eligible",
            "event_model_selection_pending"
        };

    public static string? TryGetHint(CampaignWorkflowState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!ShouldApply(state))
            return null;

        var readiness = EventModelsReadiness.Evaluate(state);
        if (readiness.IsReady)
            return null;

        if (readiness.Blockers.Any(SpecificReadinessBlockers.Contains))
            return null;

        return BuildMessage(readiness.Blockers, state);
    }

    internal static bool ShouldApply(CampaignWorkflowState state) =>
        CampaignWorkflowChecklist.IsBriefCaptured(state)
        && state.CampaignKind == CampaignWorkflowKind.EventDriven
        && !state.UserSkippedEventModels
        && state.Phase is not CampaignWorkflowPhase.Verification and not CampaignWorkflowPhase.Done
        && CampaignWorkflowToolFilter.EventModelsGateBlocksCampaignMutators(state);

    private static string BuildMessage(IReadOnlyList<string> blockers, CampaignWorkflowState state)
    {
        var blockerText = blockers.Count > 0
            ? string.Join(", ", blockers)
            : "event_models_incomplete";

        var message =
            $"Coach: Events gate is CLOSED (blockers: {blockerText}). "
            + "upsert_point_account_type and upsert_campaign are filtered from this turn — not missing from MCP. "
            + "Do NOT ask the user to create PATs via HTTP or paste GUIDs. "
            + "Next: resolve event model (get_model/save_model on order event id from brief) until WORKFLOW shows Events gate: open.";

        if (MutatorSurfaceDeferralPatterns.LooksLikeDeferral(state.Artifacts.LastToolRemediationSummary))
        {
            message += " Prior turn claimed mutators were unavailable — they were filtered by the Events gate, not absent from MCP.";
        }

        return message;
    }
}
