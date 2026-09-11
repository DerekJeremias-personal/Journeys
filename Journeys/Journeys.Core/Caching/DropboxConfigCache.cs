using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Journeys.Core.Caching;

/// <summary>
/// Cache service for DropboxConfig with hybrid memory + distributed caching.
/// Supports cache bypass for UI/testing scenarios.
/// </summary>
public class DropboxConfigCache : IDropboxConfigCache
{
    private readonly IHybridCacheService _hybridCache;
    private readonly IDropboxConfigAdapter _dropboxConfigAdapter;
    private readonly ILogger<DropboxConfigCache> _logger;

    private static readonly TimeSpan MemoryExpiration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan DistributedExpiration = TimeSpan.FromHours(2);

    public DropboxConfigCache(
        IHybridCacheService hybridCache,
        IDropboxConfigAdapter dropboxConfigAdapter,
        ILogger<DropboxConfigCache> logger)
    {
        _hybridCache = hybridCache;
        _dropboxConfigAdapter = dropboxConfigAdapter;
        _logger = logger;
    }

    public async Task<DropboxConfig?> GetDropboxConfigForBatchFileAsync(BatchFile batchFile, bool bypassCache = false)
    {
        if (batchFile == null)
            throw new ArgumentNullException(nameof(batchFile));

        // Create cache key from batch file properties
        var modelName = batchFile.ModelName?.ToLowerInvariant() ?? string.Empty;
        var fileType = System.IO.Path.GetExtension(batchFile.FileName)?[1..]?.ToLowerInvariant() ?? string.Empty;
        var directory = batchFile.JobDirectory?.ToLowerInvariant() ?? string.Empty;
        var cacheKey = $"dropboxconfig:batchfile:{batchFile.TenantId}:{modelName}:{fileType}:{directory}";

        return await _hybridCache.GetOrCreateAsync(
            cacheKey,
            async () =>
            {
                _logger.LogDebug("Cache miss for DropboxConfig batch file lookup: {CacheKey}", cacheKey);
                return await _dropboxConfigAdapter.FetchDropboxConfigForBatchFileAsync(batchFile);
            },
            MemoryExpiration,
            DistributedExpiration,
            bypassCache);
    }

    public async Task<DropboxConfig?> GetDropboxConfigForDirectoryAsync(string tenantId, string directory, bool bypassCache = false)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentNullException(nameof(tenantId));
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentNullException(nameof(directory));

        var normalizedDirectory = directory.ToLowerInvariant();
        var cacheKey = $"dropboxconfig:directory:{tenantId}:{normalizedDirectory}";

        return await _hybridCache.GetOrCreateAsync(
            cacheKey,
            async () =>
            {
                _logger.LogDebug("Cache miss for DropboxConfig directory lookup: {CacheKey}", cacheKey);
                return await _dropboxConfigAdapter.FetchDropboxConfigForDirectoryAsync(tenantId, directory);
            },
            MemoryExpiration,
            DistributedExpiration,
            bypassCache);
    }

    public async Task<List<DropboxConfig>?> GetAllDropboxConfigsAsync(string tenantId, bool bypassCache = false)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentNullException(nameof(tenantId));

        var cacheKey = $"dropboxconfigs:all:{tenantId}";

        return await _hybridCache.GetOrCreateAsync(
            cacheKey,
            async () =>
            {
                _logger.LogDebug("Cache miss for all DropboxConfigs: {CacheKey}", cacheKey);
                return await _dropboxConfigAdapter.FetchAllDropboxConfigsAsync(tenantId);
            },
            MemoryExpiration,
            DistributedExpiration,
            bypassCache);
    }

    public async Task InvalidateDropboxConfigAsync(string tenantId, string? dropboxConfigId = null)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentNullException(nameof(tenantId));

        // Invalidate all tenant-specific keys (simple approach as requested)
        await InvalidateTenantDropboxConfigsAsync(tenantId);
        
        _logger.LogInformation("Invalidated DropboxConfig cache for tenant {TenantId}", tenantId);
    }

    public async Task InvalidateTenantDropboxConfigsAsync(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentNullException(nameof(tenantId));

        // Invalidate all patterns for this tenant
        // Note: Pattern matching is limited, but we'll invalidate the known patterns
        await _hybridCache.InvalidatePatternAsync($"dropboxconfig:batchfile:{tenantId}:*");
        await _hybridCache.InvalidatePatternAsync($"dropboxconfig:directory:{tenantId}:*");
        await _hybridCache.InvalidateAsync($"dropboxconfigs:all:{tenantId}");
        
        _logger.LogInformation("Invalidated all DropboxConfig caches for tenant {TenantId}", tenantId);
    }
}

