using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Backend.Dto.Structures.Tenant;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.Infra.Backend;
using Microsoft.Extensions.Caching.Memory;

namespace Journeys.API.CampaignAgent;

public sealed class TenantVerificationContextLoader : ITenantVerificationContextLoader
{
    private readonly ITenantDataAdapter _tenantDataAdapter;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantVerificationContextLoader> _logger;

    public TenantVerificationContextLoader(
        ITenantDataAdapter tenantDataAdapter,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<TenantVerificationContextLoader> logger)
    {
        _tenantDataAdapter = tenantDataAdapter;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<TenantVerificationContext> LoadAsync(string routeTenantId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(routeTenantId))
            return Failed();

        var key = routeTenantId.Trim();
        var cacheKey = $"CampaignAgent:TenantVerification:{key}";
        if (_cache.TryGetValue(cacheKey, out CachedTenantVerification? cached) && cached != null)
            return cached.Context;

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
                    "Campaign agent: tenant verification allowlist load failed for {RouteTenantKey} (lookupByName={LookupByName}, backendCode={BackendCode})",
                    key, byName, backendEx.ErrorCode);
            }
            else
            {
                _logger.LogWarning(
                    ex,
                    "Campaign agent: tenant verification allowlist load failed for {RouteTenantKey} (lookupByName={LookupByName})",
                    key, byName);
            }

            return Failed();
        }

        if (dto == null)
        {
            _logger.LogDebug("Campaign agent: no tenant registry row for verification allowlist {RouteTenantKey}", key);
            return Failed();
        }

        var allowlist = CampaignTestAccountAllowlist.Normalize(dto.CampaignTestAccountExtIds);
        var version = BuildCacheVersion(dto, allowlist);
        var blocked = allowlist.Count == 0;
        var context = new TenantVerificationContext(
            LoadSucceeded: true,
            LoadFailed: false,
            BlockedNoAllowlist: blocked,
            Allowlist: allowlist);

        var ttlMinutes = Math.Clamp(_configuration.GetValue("CampaignAgent:TenantContextCacheMinutes", 3), 1, 60);
        _cache.Set(cacheKey, new CachedTenantVerification(version, context), new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(ttlMinutes)
        });

        return context;
    }

    public void ApplyToArtifacts(CampaignWorkflowState state, TenantVerificationContext context)
    {
        var artifacts = state.Artifacts;
        artifacts.TenantTestAccountsLoadFailed = context.LoadFailed;
        artifacts.VerificationBlockedNoAllowlist = context.LoadSucceeded && context.BlockedNoAllowlist;
        artifacts.TenantTestAccountAllowlistJson = context.LoadSucceeded
            ? JsonSerializer.Serialize(context.Allowlist)
            : null;
        artifacts.TenantTestAccountsLoadedAtUtc = context.LoadSucceeded
            ? DateTime.UtcNow.ToString("O")
            : null;
    }

    private static TenantVerificationContext Failed() =>
        new(LoadSucceeded: false, LoadFailed: true, BlockedNoAllowlist: false, Allowlist: []);

    private static string BuildCacheVersion(TenantDto dto, IReadOnlyList<string> allowlist)
    {
        var baseVersion = TenantDtoCampaignContextMapper.BuildCacheVersion(dto);
        var joined = string.Join("|", allowlist.Select(id => id.ToLowerInvariant()));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(joined)));
        return $"{baseVersion}:{hash}";
    }

    private sealed record CachedTenantVerification(string CacheVersion, TenantVerificationContext Context);
}
