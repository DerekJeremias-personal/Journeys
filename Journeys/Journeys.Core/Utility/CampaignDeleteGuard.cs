using System.Collections.Generic;
using Journeys.Core.Models;
using Journeys.DTO.Exceptions;

namespace Journeys.Core.Utility;

public static class CampaignDeleteGuard
{
    public static void ValidateCanHardDelete(Campaign? draft)
    {
        if (draft == null)
        {
            throw new APIErrorsException(new Dictionary<string, string>
            {
                { "campaignNotFound", "Draft campaign not found." }
            });
        }

        if (!string.Equals(draft.Status, CampaignStatusStrings.Draft, System.StringComparison.OrdinalIgnoreCase))
        {
            throw new APIErrorsException(new Dictionary<string, string>
            {
                { "deleteNotPermitted", "Only Draft campaigns may be deleted. Archive Live campaigns instead." }
            });
        }

        if (draft.DeployedDate != null)
        {
            throw new APIErrorsException(new Dictionary<string, string>
            {
                { "deleteNotPermitted", "Cannot delete a campaign that was previously deployed." }
            });
        }
    }
}
