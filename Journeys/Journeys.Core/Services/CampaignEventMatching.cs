using System.Collections.Generic;
using System.Linq;

namespace Journeys.Core.Services;

/// <summary>
/// Centralizes how <see cref="Models.Campaign.Events"/> links processed events to campaigns.
/// <see cref="EventService"/> loads Live campaigns whose Events list contains the event payload model id (GUID string).
/// That id is the same value as <see cref="Journeys.Core.RulesEngine.Engine.RulesServiceRequest.PayloadModelId"/> and ProcessedEventModelId on the response.
/// </summary>
public static class CampaignEventMatching
{
    /// <summary>
    /// Returns true when the campaign's Events list explicitly includes the event payload model id.
    /// </summary>
    public static bool MatchesEventPayloadModel(IEnumerable<string>? campaignEvents, string eventPayloadModelId)
    {
        return !string.IsNullOrEmpty(eventPayloadModelId)
               && campaignEvents != null
               && campaignEvents.Contains(eventPayloadModelId);
    }
}
