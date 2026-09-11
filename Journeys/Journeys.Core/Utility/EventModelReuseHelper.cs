using Journeys.Core.Models;
using Journeys.DTO.Models;

namespace Journeys.Core.Utility;

/// <summary>
/// Reuse-with-confirm path when a pending event model spec matches an eligible discovered contract.
/// </summary>
public static class EventModelReuseHelper
{
    public static EventProcessingContractDigest? GetEligibleDiscoveredMatch(CampaignWorkflowState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var spec = PendingEventModelSpecArtifact.Read(state);
        if (spec == null || string.IsNullOrWhiteSpace(spec.Name))
            return null;

        return EventModelContractsAccumulator.ReadDiscovered(state)
            .FirstOrDefault(d =>
                string.Equals(d.EventModelName, spec.Name, StringComparison.OrdinalIgnoreCase)
                && d.IsProcessEventEligible);
    }

    public static bool IsReuseConfirmPending(CampaignWorkflowState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.UserSkippedEventModels || state.CampaignKind == CampaignWorkflowKind.TagFirst)
            return false;

        var match = GetEligibleDiscoveredMatch(state);
        if (match == null)
            return false;

        if (PendingEventModelSpecArtifact.IsSatisfied(state))
            return false;

        return !EventModelContractsAccumulator.ReadResolved(state)
            .Any(d =>
                !EventModelsReadiness.IsWrapperDigest(d)
                && string.Equals(d.EventModelName, match.EventModelName, StringComparison.OrdinalIgnoreCase)
                && d.IsProcessEventEligible);
    }

    public static bool TryConfirmReuseFromUserMessage(CampaignWorkflowState state, string message)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (string.IsNullOrWhiteSpace(message))
            return false;

        var match = GetEligibleDiscoveredMatch(state);
        if (match == null)
            return false;

        if (!LooksLikeReuseConfirm(message))
            return false;

        state.Artifacts.UserRequestedNewEventModel = false;
        state.Artifacts.SelectedEventModelId = match.EventModelId;
        return EventModelContractsAccumulator.TryMergeResolved(state, match);
    }

    internal static bool LooksLikeReuseConfirm(string message)
    {
        if (WorkflowUserPhraseCatalog.ContainsAny(message, WorkflowUserPhraseCatalog.EventModelReusePhrases))
            return true;

        return WorkflowUserPhraseCatalog.ContainsAny(message, WorkflowUserPhraseCatalog.AbandonPendingModelPhrases);
    }
}
