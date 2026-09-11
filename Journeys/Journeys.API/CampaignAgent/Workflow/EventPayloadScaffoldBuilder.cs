using Journeys.Core.Models;
using Journeys.Core.Utility;

namespace Journeys.API.CampaignAgent.Workflow;

public static class EventPayloadScaffoldBuilder
{
    public static string? Build(CampaignWorkflowState state)
    {
        var resolved = EventModelContractsAccumulator.ReadResolved(state);
        if (resolved.Count == 0)
            return null;

        var contract = resolved[0];
        var accountSymbol = contract.AccountLink.SymbolPath ?? "profileid";
        var naturalKey = contract.NaturalKey.Symbols.FirstOrDefault() ?? "orderid";

        var campaignId = CreationSnapshotArtifact.Read(state)?.CampaignId;
        var campaignPart = string.IsNullOrWhiteSpace(campaignId) ? "" : $"; campaignId={campaignId}";

        return $"{{ \"{accountSymbol}\": \"<account>\", \"{naturalKey}\": \"<key>\", \"discounts\": [], \"items\": [{{ \"price\": 0, \"quantity\": 1, \"sku\": {{ \"extId\": \"SKU-1\" }} }}]{campaignPart} }}";
    }
}
