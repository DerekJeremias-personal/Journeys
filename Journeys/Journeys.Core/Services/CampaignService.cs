using Journeys.DTO.Models;
using Journeys.DTO.Requests;
using Journeys.Core.Models;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Journeys.DTO.Responses;
using System.IO;
using Microsoft.Extensions.Logging;
using Journeys.Core.Extensions;
using Journeys.DTO.Exceptions;

namespace Journeys.Core.Services
{
    public class CampaignService : ICampaignService
    {
        public static readonly string DEFAULT_TYPE = "99999999-9999-9999-9999-999999999999";

        private readonly ICampaignAdapter _campaignAdapter;
        private readonly IPointAccountTypeAdapter _pointAccountTypeAdapter;
        private readonly IPointAccountTypeCache _cache;
        private readonly ILogger<CampaignService> _logger;
        private readonly CampaignDefinitionValidator _campaignDefinitionValidator;
        private readonly CampaignValidationOrchestrator _campaignValidationOrchestrator;

        public CampaignService(
            ICampaignAdapter campaignAdapter,
            IPointAccountTypeAdapter adapter,
            IPointAccountTypeCache cache,
            ILogger<CampaignService> logger,
            CampaignDefinitionValidator campaignDefinitionValidator,
            CampaignValidationOrchestrator campaignValidationOrchestrator)
        {
            _campaignAdapter = campaignAdapter;
            _pointAccountTypeAdapter = adapter;
            _cache = cache;
            _logger = logger;
            _campaignDefinitionValidator = campaignDefinitionValidator;
            _campaignValidationOrchestrator = campaignValidationOrchestrator;
        }

        public Task<CampaignValidationResultDto> ValidateCampaignAsync(
            string tenantId,
            CampaignDto campaign,
            CancellationToken cancellationToken = default) =>
            _campaignValidationOrchestrator.ValidateAsync(tenantId, campaign, cancellationToken);

        public async Task<CampaignDto> FetchCampaignAsync(string tenantId, string campaignId, string status)
        {
            CampaignDto campaign = null;
            try
            {
                var res = await _campaignAdapter.FetchCampaignAsync(tenantId, campaignId, status);
                campaign = res.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return campaign;
        }

        public async Task<PagedResultSetResponse<CampaignDto>> GetCampaignsByStatusAsync(string tenantId, string status, int pageSize, string? continuationToken = null)
        {
            PagedResultSetResponse<CampaignDto> campaigns = new PagedResultSetResponse<CampaignDto>();
            try
            {
                var res = await _campaignAdapter.GetCampaignsByStatusAsync(tenantId, status, pageSize, continuationToken);
                campaigns.Count = res.Count;
                campaigns.ContinuationToken = res.ContinuationToken;
                campaigns.Entities = res.Entities.Select(x => x.ToDto()).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return campaigns;
        }

        public async Task<PagedResultSetResponse<CampaignDto>> GetAllCampaignsAsync(string tenantId, int pageSize, string continutationToken = null)
        {
            PagedResultSetResponse<CampaignDto> lstCampaigns = new PagedResultSetResponse<CampaignDto>();
            try
            {
                var res = await _campaignAdapter.GetAllCampaignsAsync(tenantId, pageSize, continutationToken);
                lstCampaigns.Count = res.Count;
                lstCampaigns.ContinuationToken = res.ContinuationToken;
                lstCampaigns.Entities = res.Entities.Select(x => x.ToDto()).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return lstCampaigns;
        }

        public async Task<PagedResultSetResponse<CampaignDto>> GetCampaignsByFiltersAsync(string tenantId, GetCampaignsByFilterRequest req)
        {
            PagedResultSetResponse<CampaignDto> campaigns = new PagedResultSetResponse<CampaignDto>();
            try
            {
                var res = await _campaignAdapter.GetCampaignsByFiltersAsync(tenantId, req);
                campaigns.Count = res.Count;
                campaigns.ContinuationToken = res.ContinuationToken;
                campaigns.Entities = res.Entities.Select(x => x.ToDto()).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return campaigns;
        }

        public async Task<List<CampaignDto>> GetManyCampaignsAsync(string tenantId, List<string> ids, string status = null)
        {
            List<CampaignDto> campaigns = default;
            status ??= CampaignStatusStrings.Live;
            try
            {
                var res = await _campaignAdapter.GetCampaignsAsync(tenantId, ids, status);

                campaigns = res?.Select(c => c.ToDto()).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return campaigns;
        }

        public async Task<CampaignDto> UpsertCampaignAsync(string tenantId, CampaignDto campaign)
        {
            try
            {
                var campaignToStore = campaign.FromDto();

                CampaignShellValidator.ValidateStatus(campaignToStore.Status);
                CampaignShellValidator.ValidateRequiredFields(campaignToStore);
                CampaignShellValidator.ValidateDateRange(campaignToStore);

                await _campaignDefinitionValidator
                    .ValidateAsync(tenantId, campaignToStore, CancellationToken.None)
                    .ConfigureAwait(false);

                //We should get a Name, but if we get an ext id instead then we'll use that
                campaignToStore.Name = (string.IsNullOrWhiteSpace(campaignToStore.Name)) ? campaignToStore.ExtCampaignId.ToLower() : campaignToStore.Name.ToLower();

                //If we don't have (didn't get) and external id, just use the name. ToLower for case sensitivity in cosmos
                campaignToStore.ExtCampaignId = (string.IsNullOrWhiteSpace(campaignToStore.ExtCampaignId)) ? campaignToStore.Name.ToLower() : campaignToStore.ExtCampaignId.ToLower();

                // Normalize status to lowercase
                campaignToStore.Status = campaignToStore.Status.ToLower();

                var res = await _campaignAdapter.UpsertCampaignAsync(tenantId, campaignToStore);
                campaign = res.ToDto();
                if (campaign != null && res != null)
                    campaign.AssistantDigest = CampaignAssistantDigestBuilder.Build(tenantId, res, res.ETag);
            }
            catch (APIErrorsException)
            {
                // Re-throw APIErrorsException as-is (validation errors)
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return campaign;
        }

        public async Task<CampaignDto> CopyCampaignAsync(
            string tenantId,
            string campaignId,
            string status,
            string? name = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var source = await FetchCampaignAsync(tenantId, campaignId, status).ConfigureAwait(false);
            if (source == null)
                return null;

            var copy = CampaignCopyFactory.ForNewProgram(source, name);
            return await UpsertCampaignAsync(tenantId, copy).ConfigureAwait(false);
        }

        public async Task<CampaignDto> RestoreArchivedCampaignAsync(
            string tenantId,
            string campaignId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var archive = await FetchCampaignAsync(
                    tenantId,
                    campaignId,
                    CampaignStatusStrings.Archive.ToLowerInvariant())
                .ConfigureAwait(false);
            if (archive == null)
                return null;

            var draft = await GetDraftCampaignByExtIdAsync(tenantId, archive.ExtCampaignId)
                .ConfigureAwait(false);
            if (draft != null)
            {
                throw new APIErrorsException(new Dictionary<string, string>
                {
                    ["extCampaignId"] = "A draft already exists for this program."
                });
            }

            var restored = CampaignCopyFactory.ForRestoreFromArchive(archive);
            return await UpsertCampaignAsync(tenantId, restored).ConfigureAwait(false);
        }

        public async Task DeleteCampaignAsync(string tenantId, string campaignId, string status)
        {
            CampaignDto campaign = null;
            try
            {
                await _campaignAdapter.DeleteCampaignAsync(tenantId, campaignId, status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
        }

        public async Task DeleteCampaignAsync(string tenantId, CampaignDto campaignDto)
        {
            try
            {
                await _campaignAdapter.DeleteCampaignAsync(tenantId, campaignDto.FromDto());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
        }


        public async Task<PointAccountTypeDto> FetchPointAccountType(string tenantId, string id)
        {
            PointAccountTypeDto pat = null;
            try
            {
                var res = await _pointAccountTypeAdapter.FetchPointAccountTypeAsync(tenantId, id);
                pat = res.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return pat;
        }

        public async Task<PagedResultSetResponse<PointAccountTypeDto>> GetAllPointAccountTypesAsync(string tenantId, int pageSize, string continutationToken = null)
        {
            PagedResultSetResponse<PointAccountTypeDto> pat = new PagedResultSetResponse<PointAccountTypeDto>();
            try
            {
                var res = await _pointAccountTypeAdapter.GetAllPointAccountTypesAsync(tenantId, pageSize, continutationToken);
                pat.Count = res.Count;
                pat.ContinuationToken = res.ContinuationToken;
                pat.Entities = res.Entities.Select(x => x.ToDto()).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error");
                throw;
            }
            return pat;
        }

        public async Task<PointAccountTypeDto> UpsertPointAccountTypeAsync(string tenantId, PointAccountTypeDto pat)
        {
            PointAccountTypeValidator.Validate(pat);

            try
            {
                var res = await _pointAccountTypeAdapter.UpsertPointAccountTypeAsync(tenantId, pat.FromDto());
                pat = res.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return pat;
        }

        public async Task DeletePointAccountTypeAsync(string tenantId, string pointAccountTypeId)
        {
            if (string.IsNullOrWhiteSpace(pointAccountTypeId))
                throw new ArgumentException("Point account type id is required.", nameof(pointAccountTypeId));
            try
            {
                await _pointAccountTypeAdapter.DeletePointAccountTypeAsync(tenantId, pointAccountTypeId);
                await _cache.InvalidatePointAccountTypeAsync(tenantId, pointAccountTypeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeletePointAccountType failed for tenant {TenantId} pat {PatId}", tenantId, pointAccountTypeId);
                throw;
            }
        }

        public async Task<CampaignStatisticsDto> GetCampaignStatsAsync(string tenantId, string campaignId)
        {
            CampaignStatisticsDto stats = default;
            try
            {
                //var res = await _campaignAdapter.(tenantId, campaignId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
            }
            return stats;
        }

        public async Task<List<CampaignDto>> GetCampaignVersionsByExtIdAsync(string tenantId, string extCampaignId)
        {
            List<CampaignDto> campaigns = default;
            try
            {
                var res = await _campaignAdapter.GetCampaignVersionsByExtIdAsync(tenantId, extCampaignId);
                campaigns = res?.Select(c => c.ToDto()).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return campaigns;
        }

        public async Task<PagedResultSetResponse<CampaignDto>> GetArchivedCampaignsByDateRangeAsync(string tenantId, DateTimeOffset? fromDate, DateTimeOffset? toDate, int pageSize, string? continuationToken = null)
        {
            PagedResultSetResponse<CampaignDto> campaigns = new PagedResultSetResponse<CampaignDto>();
            try
            {
                var res = await _campaignAdapter.GetArchivedCampaignsByDateRangeAsync(tenantId, fromDate, toDate, pageSize, continuationToken);
                campaigns.Count = res.Count;
                campaigns.ContinuationToken = res.ContinuationToken;
                campaigns.Entities = res.Entities.Select(x => x.ToDto()).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return campaigns;
        }

        public async Task<CampaignDto> GetLiveCampaignByExtIdAsync(string tenantId, string extCampaignId)
        {
            CampaignDto campaign = null;
            try
            {
                var res = await _campaignAdapter.GetLiveCampaignByExtIdAsync(tenantId, extCampaignId);
                campaign = res?.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return campaign;
        }

        public async Task<CampaignDto> GetDraftCampaignByExtIdAsync(string tenantId, string extCampaignId)
        {
            CampaignDto campaign = null;
            try
            {
                var res = await _campaignAdapter.GetDraftCampaignByExtIdAsync(tenantId, extCampaignId);
                campaign = res?.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return campaign;
        }
    }
}
