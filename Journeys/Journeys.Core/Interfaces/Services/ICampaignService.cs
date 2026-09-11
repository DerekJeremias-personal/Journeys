using System;
using Journeys.DTO.Models;
using Journeys.DTO.Requests;
using Journeys.DTO.Responses;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.Services
{
    public interface ICampaignService
    {
        Task<CampaignDto> FetchCampaignAsync(string tenantId, string campaignId, string status);
        Task<PagedResultSetResponse<CampaignDto>> GetCampaignsByFiltersAsync(string tenantId, GetCampaignsByFilterRequest req);
        Task<List<CampaignDto>> GetManyCampaignsAsync(string tenantId, List<string> ids, string status = null);
        Task<PagedResultSetResponse<CampaignDto>> GetCampaignsByStatusAsync(string tenantId, string status, int pageSize, string? continuationToken = null);
        Task<PagedResultSetResponse<CampaignDto>> GetAllCampaignsAsync(string tenantId, int pageSize, string continutationToken = null);

        Task<CampaignDto> UpsertCampaignAsync(string tenantId, CampaignDto campaign);
        Task<CampaignValidationResultDto> ValidateCampaignAsync(
            string tenantId,
            CampaignDto campaign,
            CancellationToken cancellationToken = default);
        Task DeleteCampaignAsync(string tenantId, string campaignId, string status);
        Task DeleteCampaignAsync(string tenantId, CampaignDto campaign);

        Task<PointAccountTypeDto> FetchPointAccountType(string tenantId, string id);
        Task<PagedResultSetResponse<PointAccountTypeDto>> GetAllPointAccountTypesAsync(string tenantId, int pageSize, string continutationToken = null);
        Task<PointAccountTypeDto> UpsertPointAccountTypeAsync(string tenantId, PointAccountTypeDto pat);

        Task DeletePointAccountTypeAsync(string tenantId, string pointAccountTypeId);

        Task<CampaignStatisticsDto> GetCampaignStatsAsync(string tenantId, string campaignId);

        // New methods for version history
        Task<List<CampaignDto>> GetCampaignVersionsByExtIdAsync(string tenantId, string extCampaignId);
        Task<PagedResultSetResponse<CampaignDto>> GetArchivedCampaignsByDateRangeAsync(string tenantId, DateTimeOffset? fromDate, DateTimeOffset? toDate, int pageSize, string? continuationToken = null);
        Task<CampaignDto> GetLiveCampaignByExtIdAsync(string tenantId, string extCampaignId);
        Task<CampaignDto> GetDraftCampaignByExtIdAsync(string tenantId, string extCampaignId);
    }
}
