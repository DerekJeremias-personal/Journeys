using Journeys.Core.Models;

namespace Journeys.Core.Utility;

/// <summary>
/// Detects narrative/state drift: discovered event contracts present but host gate still closed.
/// </summary>
public static class EventModelWorkflowDriftCoach
{
    public static string? TryGetHint(CampaignWorkflowState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (string.IsNullOrWhiteSpace(state.Artifacts.CampaignDesignBriefProposed))
            return null;

        if (EventModelsReadiness.Evaluate(state).IsReady)
            return null;

        var eventResolved = EventModelContractsAccumulator.ReadResolved(state)
            .Where(d => !EventModelsReadiness.IsWrapperDigest(d))
            .ToList();
        if (eventResolved.Any(d => d.IsProcessEventEligible))
            return null;

        var discovered = EventModelContractsAccumulator.ReadDiscovered(state)
            .Where(d => !EventModelsReadiness.IsWrapperDigest(d) && d.IsProcessEventEligible)
            .ToList();
        if (discovered.Count == 0)
            return null;

        var top = discovered[0];
        var name = top.EventModelName ?? "event model";
        var id = top.EventModelId ?? "unknown";

        if (state.Artifacts.AwaitingApproval == CampaignWorkflowApprovalKind.EventModelSelection)
        {
            return $"Coach: host gate still closed — '{name}' ({id}) is discovered but not in resolved "
                   + "contracts. Confirm event model selection, then retry upsert_campaign / "
                   + "upsert_point_account_type once mutators unlock.";
        }

        return $"Coach: host gate still closed — '{name}' ({id}) is only in discovered contracts. "
               + "Do not claim event resolution is complete or dump implementation specs; resolved "
               + "event model contracts are required before campaign mutators unlock.";
    }
}
