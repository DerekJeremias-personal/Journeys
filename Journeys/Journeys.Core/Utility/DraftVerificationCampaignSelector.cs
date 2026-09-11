using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Backend.Dto.Structures.Tenant;
using Journeys.Core.Extensions;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.DTO.Exceptions;

namespace Journeys.Core.Utility;

public static class DraftVerificationCampaignSelector
{
    public static async Task<List<Campaign>> ResolveExclusiveDraftCampaignsAsync(
        string tenantId,
        string draftVerificationCampaignId,
        string accountExtId,
        string eventPayloadModelId,
        ICampaignService campaignService,
        ITenantDataAdapter tenantDataAdapter,
        CancellationToken cancellationToken)
    {
        TenantDto? tenant = await tenantDataAdapter.GetTenantByNameAsync(
            tenantId,
            includeDeleted: false,
            cancellationToken: cancellationToken);

        if (!CampaignTestAccountAllowlist.IsAllowlisted(tenant?.CampaignTestAccountExtIds, accountExtId))
        {
            throw new APIErrorsException(new Dictionary<string, string>
            {
                { "draftTestingNotPermitted", "Account not on tenant campaign test allowlist." }
            });
        }

        var draftDto = await campaignService.FetchCampaignAsync(
            tenantId,
            draftVerificationCampaignId,
            CampaignStatusStrings.Draft);

        var draft = CampaignDraftVerificationResolver.ValidateDraftForEvent(
            draftDto?.FromDto(),
            eventPayloadModelId);

        return new List<Campaign> { draft };
    }
}
