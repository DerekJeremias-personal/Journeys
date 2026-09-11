using Backend.Dto.Structures.Tenant;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Infra.Backend;
using Microsoft.Extensions.Caching.Memory;

namespace Journeys.API.CampaignAgent;

public class CampaignAgentTenantContextProvider : ICampaignAgentTenantContextProvider
{
    private readonly ITenantDataAdapter _tenantDataAdapter;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CampaignAgentTenantContextProvider> _logger;

    public CampaignAgentTenantContextProvider(
        ITenantDataAdapter tenantDataAdapter,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<CampaignAgentTenantContextProvider> logger)
    {
        _tenantDataAdapter = tenantDataAdapter;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<CampaignAgentTenantLlmSlice?> GetSliceAsync(string routeTenantId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(routeTenantId))
            return null;

        var key = routeTenantId.Trim();
        var cacheKey = $"CampaignAgent:TenantLlmContext:{key}";
        var byName = _configuration.GetValue("CampaignAgent:TenantLookupByName", true);

        TenantDto? dto;
        try
        {
            dto = byName
                ? await _tenantDataAdapter.GetTenantByNameAsync(key, false, cancellationToken).ConfigureAwait(false)
                : await _tenantDataAdapter.GetTenantAsync(key, false, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (ex is BackendSystemException backendEx)
            {
                _logger.LogWarning(
                    backendEx,
                    "Campaign agent: tenant registry request failed for route key {RouteTenantKey} (lookupByName={LookupByName}, backendCode={BackendCode})",
                    key, byName, backendEx.ErrorCode);
            }
            else
            {
                _logger.LogWarning(
                    ex,
                    "Campaign agent: tenant registry request failed for route key {RouteTenantKey} (lookupByName={LookupByName})",
                    key, byName);
            }
            return null;
        }

        if (dto == null)
        {
            _logger.LogDebug("Campaign agent: no tenant registry row for route key {RouteTenantKey}", key);
            return null;
        }

        var version = TenantDtoCampaignContextMapper.BuildCacheVersion(dto);
        if (_cache.TryGetValue(cacheKey, out CachedTenantLlm? cached) && cached != null && string.Equals(cached.CacheVersion, version, StringComparison.Ordinal))
            return cached.Slice;

        var maxMarketing = Math.Clamp(_configuration.GetValue("CampaignAgent:MaxMarketingContextChars", 8000), 256, 50000);
        var maxConsumer = Math.Clamp(_configuration.GetValue("CampaignAgent:MaxEndConsumerContextChars", 8000), 256, 50000);
        var slice = TenantDtoCampaignContextMapper.Map(dto, maxMarketing, maxConsumer, _logger);

        var ttlMinutes = Math.Clamp(_configuration.GetValue("CampaignAgent:TenantContextCacheMinutes", 3), 1, 60);
        _cache.Set(cacheKey, new CachedTenantLlm(version, slice), new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(ttlMinutes)
        });

        return slice;
    }

    private sealed record CachedTenantLlm(string CacheVersion, CampaignAgentTenantLlmSlice Slice);
}
