using System.Collections.Generic;
using Journeys.Core.Models;
using Journeys.Core.Services;
using Journeys.DTO.Exceptions;

namespace Journeys.Core.Utility;

public static class CampaignDraftVerificationResolver
{
    public static Campaign ValidateDraftForEvent(Campaign? draft, string eventPayloadModelId)
    {
        if (draft == null)
        {
            throw new APIErrorsException(new Dictionary<string, string>
            {
                { "campaignNotFound", "No campaign with given id in Draft partition." }
            });
        }

        if (!string.Equals(draft.Status, CampaignStatusStrings.Draft, System.StringComparison.OrdinalIgnoreCase))
        {
            throw new APIErrorsException(new Dictionary<string, string>
            {
                { "campaignNotDraft", "Target campaign must be in Draft status." }
            });
        }

        if (!CampaignEventMatching.MatchesEventPayloadModel(draft.Events, eventPayloadModelId))
        {
            throw new APIErrorsException(new Dictionary<string, string>
            {
                { "campaignEventMismatch", "Campaign does not subscribe to this event model." }
            });
        }

        return draft;
    }
}
