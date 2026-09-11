using System.Collections.Generic;
using System.Linq;
using Journeys.Core.Models;
using Journeys.DTO.Exceptions;

namespace Journeys.Core.Services;

/// <summary>
/// Campaign shell field validation shared by upsert and validate-only flows.
/// </summary>
public static class CampaignShellValidator
{
    public static void ValidateStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new APIErrorsException(new Dictionary<string, string>
            {
                { "status", "Status is required." }
            });
        }

        var validStatuses = new[]
        {
            CampaignStatusStrings.Live,
            CampaignStatusStrings.Draft,
            CampaignStatusStrings.Archive,
            CampaignStatusStrings.Pause
        };

        if (!validStatuses.Any(s => s.Equals(status, StringComparison.OrdinalIgnoreCase)))
        {
            throw new APIErrorsException(new Dictionary<string, string>
            {
                {
                    "status",
                    $"Invalid status value. Must be one of: {string.Join(", ", validStatuses)}. Provided: '{status}'"
                }
            });
        }
    }

    public static void ValidateRequiredFields(Campaign campaign)
    {
        var errors = new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(campaign.ExtCampaignId) && string.IsNullOrWhiteSpace(campaign.Name))
            errors.Add("name", "Name or ExtCampaignId is required.");

        if (campaign.StartDate == DateTimeOffset.MinValue)
            errors.Add("startDate", "Start date is required.");

        if (errors.Count > 0)
            throw new APIErrorsException(errors);
    }

    public static void ValidateDateRange(Campaign campaign)
    {
        if (campaign.EndDate.HasValue && campaign.StartDate != DateTimeOffset.MinValue
            && campaign.EndDate.Value < campaign.StartDate)
        {
            throw new APIErrorsException(new Dictionary<string, string>
            {
                { "endDate", "End date must be after start date." }
            });
        }
    }
}
