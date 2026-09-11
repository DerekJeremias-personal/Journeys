using Journeys.DTO.Models;
using Journeys.DTO.Requests;
using Journeys.DTO.Responses;

namespace Journeys.Core.Caching;

/// <summary>
/// Cache service for Campaigns with hybrid memory + distributed caching.
/// Supports cache bypass for UI/testing scenarios.
/// </summary>
public interface ICampaignCache
{
    /// <summary>
    /// Gets a campaign by ID, with optional cache bypass.
    /// </summary>
    Task<CampaignDto?> GetCampaignAsync(
        string tenantId,
        string campaignId,
        string status,
        bool bypassCache = false);

    /// <summary>
    /// Gets multiple campaigns by IDs.
    /// </summary>
    Task<List<CampaignDto>> GetManyCampaignsAsync(
        string tenantId,
        List<string> ids,
        string status,
        bool bypassCache = false);

    /// <summary>
    /// Gets campaigns by status.
    /// </summary>
    Task<PagedResultSetResponse<CampaignDto>> GetCampaignsByStatusAsync(
        string tenantId,
        string status,
        int pageSize,
        string? continuationToken = null,
        bool bypassCache = false);

    /// <summary>
    /// Gets campaigns by filters.
    /// </summary>
    Task<PagedResultSetResponse<CampaignDto>> GetCampaignsByFiltersAsync(
        string tenantId,
        GetCampaignsByFilterRequest request,
        bool bypassCache = false);

    /// <summary>
    /// Invalidates a specific campaign from cache.
    /// </summary>
    Task InvalidateCampaignAsync(string tenantId, string campaignId, string? status = null);

    /// <summary>
    /// Invalidates all campaigns for a tenant.
    /// </summary>
    Task InvalidateTenantCampaignsAsync(string tenantId);
}

