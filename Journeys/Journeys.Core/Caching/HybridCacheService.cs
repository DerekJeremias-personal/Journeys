using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Journeys.Core.Caching;

/// <summary>
/// Hybrid caching service that combines fast in-memory cache with shared distributed cache.
/// Provides best of both worlds: fast local access + shared state across servers.
/// </summary>
public class HybridCacheService : IHybridCacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<HybridCacheService> _logger;
    
    // Per-key locks to prevent cache stampede
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new();

    // Default expiration times
    private static readonly TimeSpan DefaultMemoryExpiration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DefaultDistributedExpiration = TimeSpan.FromHours(1);

    public HybridCacheService(
        IMemoryCache memoryCache,
        IDistributedCache distributedCache,
        ILogger<HybridCacheService> logger)
    {
        _memoryCache = memoryCache;
        _distributedCache = distributedCache;
        _logger = logger;
    }

    public async Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? memoryExpiration = null,
        TimeSpan? distributedExpiration = null,
        bool bypassCache = false)
    {
        if (bypassCache)
        {
            _logger.LogDebug("Bypassing cache for key {Key}", key);
            var value = await factory();
            if (value != null)
            {
                // Still populate cache for next time
                await SetAsync(key, value, memoryExpiration, distributedExpiration);
            }
            return value;
        }

        // Step 1: Check memory cache (fastest - ~1 microsecond)
        if (_memoryCache.TryGetValue(key, out T? memoryValue))
        {
            _logger.LogDebug("Memory cache hit for key {Key}", key);
            return memoryValue;
        }

        // Step 2: Check distributed cache (slower - ~1 millisecond)
        var distributedValue = await GetFromDistributedCacheAsync<T>(key);
        if (distributedValue != null)
        {
            _logger.LogDebug("Distributed cache hit for key {Key}", key);
            
            // Populate memory cache for next time (fast access)
            var memExp = memoryExpiration ?? DefaultMemoryExpiration;
            _memoryCache.Set(key, distributedValue, memExp);
            
            return distributedValue;
        }

        // Step 3: Cache miss - get from source (slowest)
        // Use per-key lock to prevent multiple threads/servers from hitting source simultaneously
        var keyLock = _keyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        
        await keyLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock (another thread might have populated it)
            if (_memoryCache.TryGetValue(key, out memoryValue))
            {
                return memoryValue;
            }
            
            distributedValue = await GetFromDistributedCacheAsync<T>(key);
            if (distributedValue != null)
            {
                var memExp = memoryExpiration ?? DefaultMemoryExpiration;
                _memoryCache.Set(key, distributedValue, memExp);
                return distributedValue;
            }
            
            // Actually fetch from source
            _logger.LogDebug("Cache miss for key {Key}, fetching from source", key);
            var newValue = await factory();
            
            if (newValue != null)
            {
                // Store in both caches
                await SetAsync(key, newValue, memoryExpiration, distributedExpiration);
            }
            
            return newValue;
        }
        finally
        {
            keyLock.Release();
            // Clean up lock if no longer needed (optional optimization)
            if (keyLock.CurrentCount == 1 && _keyLocks.TryRemove(key, out _))
            {
                keyLock.Dispose();
            }
        }
    }

    public async Task<T?> GetAsync<T>(string key, bool bypassCache = false)
    {
        if (bypassCache)
        {
            return default;
        }

        // Check memory cache first
        if (_memoryCache.TryGetValue(key, out T? memoryValue))
        {
            return memoryValue;
        }

        // Check distributed cache
        return await GetFromDistributedCacheAsync<T>(key);
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? memoryExpiration = null,
        TimeSpan? distributedExpiration = null)
    {
        // Set in memory cache
        var memExp = memoryExpiration ?? DefaultMemoryExpiration;
        _memoryCache.Set(key, value, memExp);

        // Set in distributed cache
        await SetInDistributedCacheAsync(key, value, distributedExpiration ?? DefaultDistributedExpiration);
    }

    public async Task InvalidateAsync(string key)
    {
        // Remove from both caches
        _memoryCache.Remove(key);
        await _distributedCache.RemoveAsync(key);
        _logger.LogDebug("Invalidated cache for key {Key}", key);
    }

    public async Task InvalidatePatternAsync(string pattern)
    {
        // Note: Pattern matching is limited - we can't efficiently scan all keys
        // This is a best-effort implementation
        // For production, consider using Redis SCAN or maintaining a key registry
        
        _logger.LogWarning("Pattern invalidation requested for {Pattern}, but full pattern matching is not supported. Consider using specific keys.", pattern);
        
        // If pattern is a prefix, we could maintain a registry of keys
        // For now, log a warning
        await Task.CompletedTask;
    }

    public void InvalidateMemoryCache(string key)
    {
        _memoryCache.Remove(key);
        _logger.LogDebug("Invalidated memory cache for key {Key}", key);
    }

    private async Task<T?> GetFromDistributedCacheAsync<T>(string key)
    {
        try
        {
            var json = await _distributedCache.GetStringAsync(key);
            if (string.IsNullOrEmpty(json))
                return default;

            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            // Don't fail if distributed cache is down - just log and continue
            _logger.LogWarning(ex, "Failed to read from distributed cache for key {Key}", key);
            return default;
        }
    }

    private async Task SetInDistributedCacheAsync<T>(string key, T value, TimeSpan expiration)
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            };

            await _distributedCache.SetStringAsync(key, json, options);
        }
        catch (Exception ex)
        {
            // Don't fail if distributed cache is down - just log
            _logger.LogWarning(ex, "Failed to write to distributed cache for key {Key}", key);
        }
    }
}

