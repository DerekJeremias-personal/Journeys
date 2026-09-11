using System.Text.Json;
using Journeys.Core.Models;
using Journeys.DTO.Models;

namespace Journeys.Core.Utility;

/// <summary>Reads/writes the ranked <see cref="EventModelCandidateSet"/> on workflow state.</summary>
public static class EventModelCandidatesArtifact
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static void Write(CampaignWorkflowState state, EventModelCandidateSet set)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(set);
        state.Artifacts.EventModelCandidates = JsonSerializer.Serialize(set, JsonOpts);
    }

    public static EventModelCandidateSet? Read(CampaignWorkflowState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (string.IsNullOrWhiteSpace(state.Artifacts.EventModelCandidates))
            return null;
        try
        {
            return JsonSerializer.Deserialize<EventModelCandidateSet>(state.Artifacts.EventModelCandidates, JsonOpts);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool HasComputed(CampaignWorkflowState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return !string.IsNullOrWhiteSpace(state.Artifacts.EventModelCandidates);
    }

    public static void Clear(CampaignWorkflowState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.Artifacts.EventModelCandidates = null;
        state.Artifacts.SelectedEventModelId = null;
        state.Artifacts.EarningIntent = EarningIntent.Unknown;
    }
}
