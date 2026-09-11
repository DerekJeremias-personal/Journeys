using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using static System.Formats.Asn1.AsnWriter;

namespace Journeys.Core.Caching
{
    public class PointAccountTypeCache : IPointAccountTypeCache
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHybridCacheService _hybridCache;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<PointAccountTypeCache> _logger;
        private readonly ConcurrentDictionary<string, bool> _cacheKeys;
        private readonly ConcurrentDictionary<string, bool> _fullyLoadedCaches;

        private static readonly object _lock = new object();
        private static PointAccountTypeCache _instance;
        private static IServiceProvider? _staticServiceProvider;

        public static IPointAccountTypeCache Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            if (_staticServiceProvider == null)
                            {
                                throw new InvalidOperationException(
                                    "PointAccountTypeCache has not been initialized. " +
                                    "Please call Initialize with an IServiceProvider first.");
                            }
                            _instance = new PointAccountTypeCache(_staticServiceProvider);
                        }
                    }
                }
                return _instance;
            }
        }

        public static void Initialize(IServiceProvider serviceProvider)
        {
            lock (_lock)
            {
                _staticServiceProvider = serviceProvider;
                _ = Instance;
            }
        }

        private PointAccountTypeCache(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _hybridCache = serviceProvider.GetRequiredService<IHybridCacheService>();
            _memoryCache = serviceProvider.GetRequiredService<IMemoryCache>();
            _logger = serviceProvider.GetRequiredService<ILogger<PointAccountTypeCache>>();
            _cacheKeys = new ConcurrentDictionary<string, bool>();
            _fullyLoadedCaches = new ConcurrentDictionary<string, bool>();
        }

        public async Task<PointAccountType> GetPointAccountTypeAsync(string tenantId, string pointAccountTypeId)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(pointAccountTypeId)) throw new ArgumentNullException(nameof(pointAccountTypeId));

            var cacheKey = $"pat:{tenantId}:{pointAccountTypeId}";
            var cachedValue = await _hybridCache.GetOrCreateAsync(
                key: cacheKey,
                factory: async () =>
                {
                    _logger.LogInformation("Cache miss for key {CacheKey}. Fetching from adapter.", cacheKey);

                    using (var scope = _serviceProvider!.CreateScope())
                    {
                        var adapter = scope.ServiceProvider.GetRequiredService<IPointAccountTypeAdapter>();
                        var value = await adapter.FetchPointAccountTypeAsync(tenantId, pointAccountTypeId);

                        if (value == null ||
                            (string.IsNullOrWhiteSpace(value.ExpiresToPointAccountTypeId) && value.LedgerType != null &&
                            value.LedgerType.Equals(PointLedgerTypeStrings.SPENDABLE)))
                        {
                            _logger.LogWarning("PointAccountType fetch failed to find key {CacheKey}, or the spendable point account type has no expiration set.", cacheKey);

                            var lst = await adapter.GetAllPointAccountTypesAsync(tenantId, 100);
                            var pats = lst.Entities ?? new List<PointAccountType>();
                            value = pats.FirstOrDefault(x => x.Id.Equals(pointAccountTypeId, StringComparison.InvariantCultureIgnoreCase));
                        }

                        if (value == null)
                        {
                            _logger.LogError("PointAccountType not found for key {CacheKey}.", cacheKey);
                        }

                        return value;
                    }
                },
                memoryExpiration: TimeSpan.FromMinutes(10),
                distributedExpiration: TimeSpan.FromHours(2));

            if (cachedValue != null)
            {
                _cacheKeys[cacheKey] = true;
            }

            return cachedValue;
        }

        public static Task<PointAccountType> GetPointAccountType(string tenantId, string pointAccountTypeId)
        {
            if (Instance == null)
            {
                throw new InvalidOperationException("PointAccountTypeCache instance is not initialized.");
            }

            return Instance.GetPointAccountTypeAsync(tenantId, pointAccountTypeId);
        }

        public async Task<bool> EnsurePATsLoaded(string tenantId)
        {
            using (var scope = _serviceProvider!.CreateScope())
            {
                var adapter = scope.ServiceProvider.GetRequiredService<IPointAccountTypeAdapter>();
                var lst = await adapter.GetAllPointAccountTypesAsync(tenantId, 100);
                var pats = lst.Entities ?? new List<PointAccountType>();

                foreach (var pat in pats)
                {
                    var cacheKey = $"pat:{tenantId}:{pat.Id}";
                    await _hybridCache.SetAsync(
                        cacheKey,
                        pat,
                        memoryExpiration: TimeSpan.FromMinutes(10),
                        distributedExpiration: TimeSpan.FromHours(2));

                    _cacheKeys[cacheKey] = true;
                    _logger.LogInformation("Cached PointAccountType with key {CacheKey}.", cacheKey);
                }

                if (pats.Count > 0)
                {
                    _fullyLoadedCaches.TryAdd(tenantId, true);
                    return true;
                }
            }
            return false;
        }

        public async Task<List<PointAccountType>> GetAllPointAccountTypes(string tenantId)
        {
            var lst = this.GetAllCachedValues();
            if (!_fullyLoadedCaches.ContainsKey(tenantId) || (_fullyLoadedCaches.ContainsKey(tenantId) &&
                (!lst?.Where(x => x.TenantId == tenantId)?.ToList()?.Any() ?? true)))
            {
                bool bCached = await EnsurePATsLoaded(tenantId);
                if (!bCached)
                {
                    await EnsurePATsLoaded(tenantId);
                }
                lst = this.GetAllCachedValues();
            }

            return lst?.Where(x => x.TenantId == tenantId).ToList() ?? new List<PointAccountType>();
        }

        private PointAccountType ResolvePAT(string tenantId, string type, decimal? ttl = null)
        {
            var allValues = GetAllCachedValues();
            return ResolvePAT(tenantId, allValues, type, ttl);
        }

        private PointAccountType ResolvePAT(string tenantId, List<PointAccountType> allValues, string type, decimal? ttl = null)
        {
            return allValues.FirstOrDefault(x => x.TenantId == tenantId &&
                            string.IsNullOrEmpty(type) || x.LedgerType.Equals(type, StringComparison.InvariantCultureIgnoreCase) &&
                            ttl == null | (ttl != null && x.PointsLifespanDays == ttl));
        }

        public bool CachePointAccountType(string tenantId, PointAccountType pointAccountType)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (pointAccountType == null) throw new ArgumentNullException(nameof(pointAccountType));
            if (string.IsNullOrWhiteSpace(pointAccountType.Id)) throw new ArgumentNullException(nameof(pointAccountType.Id));

            var cacheKey = $"pat:{tenantId}:{pointAccountType.Id}";
            _hybridCache.SetAsync(
                cacheKey,
                pointAccountType,
                memoryExpiration: TimeSpan.FromMinutes(10),
                distributedExpiration: TimeSpan.FromHours(2)).Wait();

            _cacheKeys[cacheKey] = true;
            return true;
        }

        public async Task InvalidatePointAccountTypeAsync(string tenantId, string pointAccountTypeId)
        {
            var cacheKey = $"pat:{tenantId}:{pointAccountTypeId}";
            await _hybridCache.InvalidateAsync(cacheKey);
            _cacheKeys.TryRemove(cacheKey, out _);
        }

        public async Task InvalidateTenantPointAccountTypesAsync(string tenantId)
        {
            var pattern = $"pat:{tenantId}:*";
            await _hybridCache.InvalidatePatternAsync(pattern);
            var keysToRemove = _cacheKeys.Keys.Where(k => k.StartsWith($"pat:{tenantId}:")).ToList();
            foreach (var key in keysToRemove)
            {
                _cacheKeys.TryRemove(key, out _);
            }
            _fullyLoadedCaches.TryRemove(tenantId, out _);
        }

        internal List<PointAccountType> GetAllCachedValues()
        {
            var allValues = new List<PointAccountType>();
            foreach (var key in _cacheKeys.Keys)
            {
                if (_memoryCache.TryGetValue(key, out PointAccountType value))
                {
                    allValues.Add(value);
                }
            }
            return allValues;
        }
    }
}
