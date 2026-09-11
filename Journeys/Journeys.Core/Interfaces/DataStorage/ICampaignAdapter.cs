using Journeys.Core.Models;
using Journeys.DTO.Requests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.DataStorage
{
    public interface ICampaignAdapter
    {
        Task<Campaign> FetchCampaignAsync(string tenantId, string campaignId, string status);

        Task<PagedResultSet<Campaign>> GetCampaignsByFiltersAsync(string tenantId, GetCampaignsByFilterRequest req);
        Task<List<Campaign>> GetCampaignsAsync(string tenantId, List<string> ids, string status);
        Task<PagedResultSet<Campaign>> GetCampaignsByStatusAsync(string tenantId, string status, int pageSize, string? continuationToken = null);
        Task<PagedResultSet<Campaign>> GetAllCampaignsAsync(string tenantId, int pageSize, string continuationToken = null);

        Task<Campaign> UpsertCampaignAsync(string tenantId, Campaign campaign);

        Task DeleteCampaignAsync(string tenantId, string campaignId, string status);

        Task DeleteCampaignAsync(string tenantId, Campaign Campaign);

        // New methods for version history
        Task<List<Campaign>> GetCampaignVersionsByExtIdAsync(string tenantId, string extCampaignId);
        Task<PagedResultSet<Campaign>> GetArchivedCampaignsByDateRangeAsync(string tenantId, DateTimeOffset? fromDate, DateTimeOffset? toDate, int pageSize, string? continuationToken = null);
        Task<Campaign> GetLiveCampaignByExtIdAsync(string tenantId, string extCampaignId);
        Task<Campaign> GetDraftCampaignByExtIdAsync(string tenantId, string extCampaignId);
    }
}
