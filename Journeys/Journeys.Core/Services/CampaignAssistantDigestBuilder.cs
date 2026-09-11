using System;
using System.Collections.Generic;
using System.Linq;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.Utility;
using Journeys.DTO.Models;

namespace Journeys.Core.Services;

/// <summary>
/// Builds the compact post-upsert digest from an in-memory <see cref="Campaign"/> (no I/O).
/// </summary>
public static class CampaignAssistantDigestBuilder
{
    public static CampaignUpsertAssistantDigestDto Build(string tenantId, Campaign campaign, string? etag = null)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        var (nodeCount, ruleSetCount, outcomeKinds) = WalkJourney(campaign.Journey);
        var bindings = new List<EventModelBindingDigestDto>();
        foreach (var id in campaign.Events ?? Enumerable.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(id))
                continue;
            bindings.Add(new EventModelBindingDigestDto { ModelId = id.Trim(), ResolvedModelType = null });
        }

        return new CampaignUpsertAssistantDigestDto
        {
            TenantId = tenantId,
            CampaignId = campaign.Id ?? string.Empty,
            Status = campaign.Status ?? string.Empty,
            ExtCampaignId = campaign.ExtCampaignId,
            Etag = etag,
            EventModelBindings = bindings,
            JourneyDigest = new JourneyDigestDto
            {
                JourneyNodeCount = nodeCount,
                RuleSetCount = ruleSetCount,
                OutcomeKindCounts = outcomeKinds
            },
            Validation = new ValidationStampDto { TierA = "passed" }
        };
    }

    private static (int Nodes, int RuleSets, Dictionary<string, int> OutcomeKinds) WalkJourney(JourneyNode? root)
    {
        var walk = JourneyDigestWalkUtility.Walk(root);
        return (walk.JourneyNodeCount, walk.RuleSetCount, walk.OutcomeKindCounts);
    }
}
