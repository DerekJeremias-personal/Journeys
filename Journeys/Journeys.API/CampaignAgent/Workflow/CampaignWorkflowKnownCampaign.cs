using System.Text.Json;
using Journeys.Core.Models;
using Journeys.Core.Utility;

namespace Journeys.API.CampaignAgent.Workflow;

public static class CampaignWorkflowKnownCampaign
{
    public static bool HasKnownCampaignId(CampaignWorkflowState state)
    {
        var snap = CreationSnapshotArtifact.Read(state);
        if (!string.IsNullOrWhiteSpace(snap?.CampaignId))
            return true;

        return !string.IsNullOrWhiteSpace(TryReadShellCampaignId(state.Artifacts.CampaignShellRef));
    }

    private static string? TryReadShellCampaignId(string? shellRefJson)
    {
        if (string.IsNullOrWhiteSpace(shellRefJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(shellRefJson);
            if (doc.RootElement.TryGetProperty("campaignId", out var id))
                return id.GetString();
            if (doc.RootElement.TryGetProperty("CampaignId", out id))
                return id.GetString();
        }
        catch (JsonException)
        {
            // ignore malformed shell ref
        }

        return null;
    }
}
