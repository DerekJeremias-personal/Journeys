using Journeys.Core.Models;

namespace Journeys.Core.Utility;

public static class EventModelExpectedIdResolver
{
    public static string? Resolve(CampaignWorkflowState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!string.IsNullOrWhiteSpace(state.Artifacts.SelectedEventModelId))
            return state.Artifacts.SelectedEventModelId.Trim();

        var planned = EventModelContractsAccumulator.GetPlannedEventModelIds(state);
        if (planned.Count > 0)
            return planned[0];

        var set = EventModelCandidatesArtifact.Read(state);
        if (!string.IsNullOrWhiteSpace(set?.RecommendedDefaultId))
            return set.RecommendedDefaultId.Trim();

        return null;
    }
}
