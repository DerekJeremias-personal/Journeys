using Journeys.Core.Models;
using Journeys.DTO.Models;

namespace Journeys.Core.Utility;

public sealed class EventModelsReadinessResult
{
    public bool IsReady { get; init; }

    public IReadOnlyList<string> Blockers { get; init; } = [];

    public string? CoachMessage { get; init; }
}

/// <summary>
/// Single source of truth for EventModels phase completion and campaign-mutator unlock.
/// </summary>
public static class EventModelsReadiness
{
    public static EventModelsReadinessResult Evaluate(CampaignWorkflowState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.UserSkippedEventModels || state.CampaignKind == CampaignWorkflowKind.TagFirst)
            return Ready();

        if (state.Artifacts.AwaitingApproval == CampaignWorkflowApprovalKind.EventModelSelection)
        {
            return NotReady(
                ["event_model_selection_pending"],
                BuildEventModelSelectionCoachMessage(state));
        }

        var blockers = new List<string>();
        var allResolved = EventModelContractsAccumulator.ReadResolved(state);
        var eventResolved = allResolved.Where(d => !IsWrapperDigest(d)).ToList();

        if (allResolved.Count > 0 && eventResolved.Count == 0)
            blockers.Add("wrapper_in_resolved_list");

        EvaluateSelection(state, eventResolved, blockers);
        EvaluateWrapperContracts(state, eventResolved, blockers);

        var coachMessage = BuildCoachMessage(state, blockers, eventResolved);
        return new EventModelsReadinessResult
        {
            IsReady = blockers.Count == 0,
            Blockers = blockers,
            CoachMessage = coachMessage
        };
    }

    internal static bool IsWrapperDigest(EventProcessingContractDigest digest) =>
        !string.IsNullOrWhiteSpace(digest.EventModelName)
        && digest.EventModelName.EndsWith("AndRuleState", StringComparison.OrdinalIgnoreCase);

    private static EventModelsReadinessResult Ready() =>
        new() { IsReady = true };

    private static EventModelsReadinessResult NotReady(IReadOnlyList<string> blockers, string coachMessage) =>
        new() { IsReady = false, Blockers = blockers, CoachMessage = coachMessage };

    private static void EvaluateSelection(
        CampaignWorkflowState state,
        IReadOnlyList<EventProcessingContractDigest> eventResolved,
        List<string> blockers)
    {
        var pending = PendingEventModelSpecArtifact.Read(state);

        if (eventResolved.Count == 0)
        {
            if (EventModelReuseHelper.IsReuseConfirmPending(state))
                blockers.Add("event_model_reuse_confirm_pending");
            else if (pending != null && !string.IsNullOrWhiteSpace(pending.Name))
                blockers.Add("pending_event_model_unsatisfied");
            else if (state.Artifacts.UserRequestedNewEventModel)
                blockers.Add("pending_event_model_unsatisfied");
            else
                blockers.Add("no_resolved_event_model");
            return;
        }

        if (pending != null && !string.IsNullOrWhiteSpace(pending.Name))
        {
            var match = eventResolved.FirstOrDefault(d =>
                string.Equals(d.EventModelName, pending.Name, StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                if (EventModelReuseHelper.IsReuseConfirmPending(state))
                    blockers.Add("event_model_reuse_confirm_pending");
                else
                    blockers.Add("pending_event_model_unsatisfied");
                return;
            }

            if (!match.IsProcessEventEligible)
                blockers.Add("event_model_not_process_eligible");
            return;
        }

        var plannedIds = EventModelContractsAccumulator.GetPlannedEventModelIds(state);
        if (plannedIds.Count > 0)
        {
            if (!plannedIds.All(id => eventResolved.Any(d =>
                    string.Equals(d.EventModelId, id, StringComparison.OrdinalIgnoreCase)
                    && d.IsProcessEventEligible)))
                blockers.Add("planned_event_models_incomplete");
            return;
        }

        if (!string.IsNullOrWhiteSpace(state.Artifacts.SelectedEventModelId))
        {
            var match = eventResolved.FirstOrDefault(d =>
                string.Equals(d.EventModelId, state.Artifacts.SelectedEventModelId, StringComparison.OrdinalIgnoreCase));
            if (match == null || !match.IsProcessEventEligible)
                blockers.Add("no_resolved_event_model");
            return;
        }

        if (state.Artifacts.UserRequestedNewEventModel)
        {
            if (EventModelReuseHelper.IsReuseConfirmPending(state))
                blockers.Add("event_model_reuse_confirm_pending");
            else
                blockers.Add("pending_event_model_unsatisfied");
            return;
        }

        if (!eventResolved.Any(d => d.IsProcessEventEligible))
            blockers.Add("event_model_not_process_eligible");
    }

    private static void EvaluateWrapperContracts(
        CampaignWorkflowState state,
        IReadOnlyList<EventProcessingContractDigest> eventResolved,
        List<string> blockers)
    {
        var validationEntries = WrapperContractArtifact.Read(state);
        foreach (var contract in eventResolved)
        {
            if (string.IsNullOrWhiteSpace(contract.WrapperModelId))
                continue;

            var entry = validationEntries.FirstOrDefault(e =>
                string.Equals(e.WrapperModelId, contract.WrapperModelId, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                blockers.Add("wrapper_validation_missing");
                continue;
            }

            if (entry.Errors.Count > 0)
                blockers.Add("wrapper_contract_invalid");
        }
    }

    private static string? BuildCoachMessage(
        CampaignWorkflowState state,
        IReadOnlyList<string> blockers,
        IReadOnlyList<EventProcessingContractDigest> eventResolved)
    {
        if (blockers.Count == 0)
            return null;

        var wrapperEntry = WrapperContractArtifact.Read(state).FirstOrDefault(e => e.Errors.Count > 0);
        if (wrapperEntry != null)
        {
            var errors = wrapperEntry.Errors.Take(3);
            var summary = string.Join("; ", errors);
            var name = wrapperEntry.WrapperModelName ?? "wrapper";
            var linkedEvent = eventResolved.FirstOrDefault(d =>
                !string.IsNullOrWhiteSpace(d.WrapperModelId)
                && string.Equals(d.WrapperModelId, wrapperEntry.WrapperModelId, StringComparison.OrdinalIgnoreCase));
            var eventModelId = linkedEvent?.EventModelId ?? "unknown";
            var wrapperModelHint = string.IsNullOrWhiteSpace(wrapperEntry.WrapperModelId)
                ? string.Empty
                : $" and wrapperModelId '{wrapperEntry.WrapperModelId}'";
            return
                $"Coach: {name} wrapper invalid ({summary}). Repair via build_event_wrapper for eventModelId '{eventModelId}'{wrapperModelHint} when repairing in place, then save_model before campaign tools unlock.";
        }

        if (blockers.Contains("event_model_reuse_confirm_pending"))
        {
            var match = EventModelReuseHelper.GetEligibleDiscoveredMatch(state);
            var pendingSpec = PendingEventModelSpecArtifact.Read(state);
            var name = match?.EventModelName ?? pendingSpec?.Name ?? "event model";
            var id = match?.EventModelId ?? "unknown";
            return $"Coach: existing event model '{name}' ({id}) matches your spec. Confirm reuse with the user, or save_model to create a new event payload. Do not use save_model for point account types or campaigns — use upsert tools after Events gate clears.";
        }

        if (blockers.Contains("pending_event_model_unsatisfied"))
        {
            var pending = PendingEventModelSpecArtifact.Read(state);
            var name = pending?.Name ?? "event model";
            if (state.Artifacts.EventModelSaveFailureCount > 0)
            {
                return $"Coach: persist eventable event model '{name}' via save_model ({state.Artifacts.EventModelSaveFailureCount} failed save attempts). Do not use save_model for point account types or campaigns.";
            }

            return $"Coach: persist eventable event model '{name}' via save_model before campaign tools unlock. Do not use save_model for point account types or campaigns.";
        }

        if (blockers.Contains("event_model_not_process_eligible"))
        {
            var pending = PendingEventModelSpecArtifact.Read(state);
            var name = pending?.Name
                       ?? eventResolved.FirstOrDefault()?.EventModelName
                       ?? "event model";
            return $"Coach: event model '{name}' must be tag eventable with modelMetaData before campaign tools unlock.";
        }

        if (blockers.Contains("wrapper_in_resolved_list"))
            return "Coach: resolved contracts list wrapper-only state; save the event payload model (not ReviewAndRuleState) via save_model.";

        if (blockers.Contains("event_model_selection_pending"))
            return BuildEventModelSelectionCoachMessage(state);

        if (blockers.Contains("wrapper_validation_missing") || blockers.Contains("wrapper_contract_invalid"))
            return "Coach: load or save the linked wrapper model and fix wrapperContractValidation errors before campaign tools unlock.";

        if (blockers.Contains("planned_event_models_incomplete"))
            return "Coach: resolve all planned event models from the brief before campaign tools unlock.";

        if (blockers.Contains("no_resolved_event_model"))
        {
            return EventModelResolutionCoach.TryGetHint(state, blockers)
                   ?? "Coach: resolve an event model (save_model or get_model) before campaign tools unlock.";
        }

        return null;
    }

    private static string BuildEventModelSelectionCoachMessage(CampaignWorkflowState state)
    {
        var set = EventModelCandidatesArtifact.Read(state);
        if (set != null && !string.IsNullOrWhiteSpace(set.RecommendedDefaultId))
        {
            var rec = set.Candidates.FirstOrDefault(c =>
                string.Equals(c.EventModelId, set.RecommendedDefaultId, StringComparison.OrdinalIgnoreCase));
            if (rec is { IsStrongMatch: true })
            {
                var label = rec.Name ?? rec.DisplayName ?? "event model";
                return $"Coach: recommended event model '{label}' ({rec.EventModelId}) — ask one-line "
                       + "confirmation if needed, then get_model to resolve before campaign tools unlock.";
            }
        }

        return "Coach: confirm event model selection before campaign tools unlock.";
    }
}
