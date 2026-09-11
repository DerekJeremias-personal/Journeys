using Journeys.Core.Models;
using Journeys.DTO.Models;

namespace Journeys.Core.Utility;

public static class EventModelResolutionCoach
{
    public static string? TryGetHint(CampaignWorkflowState state, IReadOnlyList<string> blockers)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!blockers.Contains("no_resolved_event_model"))
            return null;

        var expectedId = EventModelExpectedIdResolver.Resolve(state);
        var discovered = EventModelContractsAccumulator.ReadDiscovered(state)
            .Where(d => !EventModelsReadiness.IsWrapperDigest(d))
            .ToList();

        if (discovered.Count > 0 && !string.IsNullOrWhiteSpace(expectedId))
        {
            var top = discovered[0];
            if (!string.Equals(top.EventModelId, expectedId, StringComparison.OrdinalIgnoreCase))
            {
                var discoveredLabel = top.EventModelName ?? top.EventModelId ?? "unknown";
                return $"Coach: loaded event model '{discoveredLabel}' ({top.EventModelId}) but campaign needs "
                       + $"'{expectedId}'. Call get_model({expectedId}) before campaign tools unlock.";
            }

            if (top.IsProcessEventEligible && !IsCompleteForAutoPromote(top))
            {
                var label = top.EventModelName ?? expectedId;
                return $"Coach: event model '{label}' ({expectedId}) is incomplete — "
                       + "save_model the event payload and wrapper before campaign tools unlock.";
            }
        }

        if (!string.IsNullOrWhiteSpace(expectedId))
        {
            return $"Coach: call get_model({expectedId}) to resolve the order event model "
                   + "before campaign tools unlock.";
        }

        return null;
    }

    private static bool IsCompleteForAutoPromote(EventProcessingContractDigest d) =>
        d.IsProcessEventEligible
        && !string.IsNullOrWhiteSpace(d.WrapperModelId)
        && !string.IsNullOrWhiteSpace(d.AccountLink?.SymbolPath)
        && d.NaturalKey?.Symbols is { Count: > 0 };
}
