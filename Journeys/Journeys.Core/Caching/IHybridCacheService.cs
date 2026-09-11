namespace Journeys.Core.Caching;

/// <summary>
/// Hybrid caching service that combines fast in-memory cache with shared distributed cache.
/// Provides best of both worlds: fast local access + shared state across servers.
/// </summary>
public interface IHybridCacheService
{
    /// <summary>
    /// Gets a value from cache or creates it using the factory if not found.
    /// Checks memory cache first (fastest), then distributed cache, then calls factory.
    /// </summary>
    /// <typeparam name="T">Type of the cached value</typeparam>
    /// <param name="key">Cache key</param>
    /// <param name="factory">Factory function to create value if not in cache</param>
    /// <param name="memoryExpiration">Expiration for memory cache (default: 5 minutes)</param>
    /// <param name="distributedExpiration">Expiration for distributed cache (default: 1 hour)</param>
    /// <param name="bypassCache">If true, bypasses all caches and calls factory directly</param>
    /// <returns>Cached or newly created value</returns>
    Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? memoryExpiration = null,
        TimeSpan? distributedExpiration = null,
        bool bypassCache = false);

    /// <summary>
    /// Gets a value from cache without creating it if missing.
    /// </summary>
    Task<T?> GetAsync<T>(string key, bool bypassCache = false);

    /// <summary>
    /// Sets a value in both memory and distributed cache.
    /// </summary>
    Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? memoryExpiration = null,
        TimeSpan? distributedExpiration = null);

    /// <summary>
    /// Removes a value from both memory and distributed cache.
    /// </summary>
    Task InvalidateAsync(string key);

    /// <summary>
    /// Removes values matching a pattern from both caches.
    /// Note: Pattern matching is best-effort and may not work for all scenarios.
    /// </summary>
    Task InvalidatePatternAsync(string pattern);

    /// <summary>
    /// Removes a value from memory cache only (forces refresh from distributed cache next time).
    /// </summary>
    void InvalidateMemoryCache(string key);
}

