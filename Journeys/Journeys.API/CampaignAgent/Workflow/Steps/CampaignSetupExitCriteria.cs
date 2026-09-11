using System.Text.Json;
using Journeys.Core.Models;
using Journeys.Core.Utility;

namespace Journeys.API.CampaignAgent.Workflow.Steps;

public sealed class CampaignSetupExitCriteria : IWorkflowStepExitCriteria
{
    public CampaignWorkflowPhase Phase => CampaignWorkflowPhase.CampaignSetup;

    public bool IsMet(CampaignWorkflowState state)
    {
        if (string.IsNullOrWhiteSpace(state.Artifacts.CampaignShellRef))
            return false;

        var shellIds = ParseShellEventModelIds(state.Artifacts.CampaignShellRef);
        var resolvedIds = EventModelContractsAccumulator.ReadResolved(state)
            .Select(c => c.EventModelId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var id in shellIds)
        {
            if (!resolvedIds.Contains(id))
                return false;
        }

        return true;
    }

    internal static IReadOnlyList<string> ParseShellEventModelIds(string shellRef)
    {
        try
        {
            using var doc = JsonDocument.Parse(shellRef);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return [];

            if (root.TryGetProperty("eventModelIds", out var ids) && ids.ValueKind == JsonValueKind.Array)
                return ReadStringArray(ids);

            if (root.TryGetProperty("events", out var events) && events.ValueKind == JsonValueKind.Array)
                return ReadStringArray(events);
        }
        catch (JsonException)
        {
        }

        return [];
    }

    private static List<string> ReadStringArray(JsonElement array) =>
        array.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString()!)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
}
