using Journeys.Core.Interfaces.DataStorage;
using Journeys.DTO.Models;
using Journeys.DTO.Requests;
using Journeys.DTO.Responses;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Journeys.Core.Extensions;

namespace Journeys.Core.Caching;

/// <summary>
/// Cache service for Campaigns with hybrid memory + distributed caching.
/// Supports cache bypass for UI/testing scenarios.
/// </summary>
public class CampaignCache : ICampaignCache
{
    private readonly IHybridCacheService _hybridCache;
    private readonly ICampaignAdapter _campaignAdapter;
    private readonly ILogger<CampaignCache> _logger;

    // Cache expiration times (campaigns change rarely, but need quick UI updates)
    private static readonly TimeSpan MemoryExpiration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DistributedExpiration = TimeSpan.FromHours(1);

    public CampaignCache(
        IHybridCacheService hybridCache,
        ICampaignAdapter campaignAdapter,
        ILogger<CampaignCache> logger)
    {
        _hybridCache = hybridCache;
        _campaignAdapter = campaignAdapter;
        _logger = logger;
    }

    public async Task<CampaignDto?> GetCampaignAsync(
        string tenantId,
        string campaignId,
        string status,
        bool bypassCache = false)
    {
        var cacheKey = $"campaign:{tenantId}:{campaignId}:{status.ToLower()}";

        return await _hybridCache.GetOrCreateAsync(
            key: cacheKey,
            factory: async () =>
            {
                var campaign = await _campaignAdapter.FetchCampaignAsync(tenantId, campaignId, status);
                return campaign?.ToDto();
            },
            memoryExpiration: MemoryExpiration,
            distributedExpiration: DistributedExpiration,
            bypassCache: bypassCache);
    }

    public async Task<List<CampaignDto>> GetManyCampaignsAsync(
        string tenantId,
        List<string> ids,
        string status,
        bool bypassCache = false)
    {
        if (ids == null || ids.Count == 0)
            return new List<CampaignDto>();

        // For multiple campaigns, we could cache individually and combine, or cache as a batch
        // For now, let's cache individually for better granularity
        var tasks = ids.Select(id => GetCampaignAsync(tenantId, id, status, bypassCache));
        var results = await Task.WhenAll(tasks);
        
        return results.Where(c => c != null).ToList()!;
    }

    public async Task<PagedResultSetResponse<CampaignDto>> GetCampaignsByStatusAsync(
        string tenantId,
        string status,
        int pageSize,
        string? continuationToken = null,
        bool bypassCache = false)
    {
        var cacheKey = $"campaigns:status:{tenantId}:{status.ToLower()}:{pageSize}:{continuationToken ?? "first"}";

        return await _hybridCache.GetOrCreateAsync(
            key: cacheKey,
            factory: async () =>
            {
                var result = await _campaignAdapter.GetCampaignsByStatusAsync(tenantId, status, pageSize, continuationToken);
                return new PagedResultSetResponse<CampaignDto>
                {
                    Count = result.Count,
                    ContinuationToken = result.ContinuationToken,
                    Entities = result.Entities?.Select(x => x.ToDto()).ToList() ?? new List<CampaignDto>()
                };
            },
            memoryExpiration: MemoryExpiration,
            distributedExpiration: DistributedExpiration,
            bypassCache: bypassCache);
    }

    public async Task<PagedResultSetResponse<CampaignDto>> GetCampaignsByFiltersAsync(
        string tenantId,
        GetCampaignsByFilterRequest request,
        bool bypassCache = false)
    {
        // Create a hash of the filter request for cache key
        var filterHash = ComputeFilterHash(request);
        var cacheKey = $"campaigns:filter:{tenantId}:{filterHash}";

        return await _hybridCache.GetOrCreateAsync(
            key: cacheKey,
            factory: async () =>
            {
                var result = await _campaignAdapter.GetCampaignsByFiltersAsync(tenantId, request);
                return new PagedResultSetResponse<CampaignDto>
                {
                    Count = result.Count,
                    ContinuationToken = result.ContinuationToken,
                    Entities = result.Entities?.Select(x => x.ToDto()).ToList() ?? new List<CampaignDto>()
                };
            },
            memoryExpiration: MemoryExpiration,
            distributedExpiration: DistributedExpiration,
            bypassCache: bypassCache);
    }

    public async Task InvalidateCampaignAsync(string tenantId, string campaignId, string? status = null)
    {
        if (string.IsNullOrEmpty(status))
        {
            // Invalidate for all statuses (we don't know which one was cached)
            // This is a limitation - we'd need to track which statuses were cached
            await _hybridCache.InvalidatePatternAsync($"campaign:{tenantId}:{campaignId}:*");
        }
        else
        {
            var cacheKey = $"campaign:{tenantId}:{campaignId}:{status.ToLower()}";
            await _hybridCache.InvalidateAsync(cacheKey);
        }

        // Also invalidate list caches that might contain this campaign
        await _hybridCache.InvalidatePatternAsync($"campaigns:*:{tenantId}:*");
        
        _logger.LogInformation("Invalidated cache for campaign {CampaignId} in tenant {TenantId}", campaignId, tenantId);
    }

    public async Task InvalidateTenantCampaignsAsync(string tenantId)
    {
        await _hybridCache.InvalidatePatternAsync($"campaign:{tenantId}:*");
        await _hybridCache.InvalidatePatternAsync($"campaigns:*:{tenantId}:*");
        
        _logger.LogInformation("Invalidated all campaign caches for tenant {TenantId}", tenantId);
    }

    private static string ComputeFilterHash(GetCampaignsByFilterRequest request)
    {
        // Create a deterministic hash of the filter request
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = false });
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
        return Convert.ToBase64String(hashBytes).Replace("/", "_").Replace("+", "-").Substring(0, 16);
    }
}

