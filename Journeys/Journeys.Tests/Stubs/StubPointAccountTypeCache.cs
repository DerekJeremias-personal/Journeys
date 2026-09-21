using Journeys.Core.Caching;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.Tests;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Journeys.Tests.Stubs
{
    public class StubPointAccountTypeCache : IPointAccountTypeCache
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ConcurrentDictionary<string, bool> _cacheKeys = new ConcurrentDictionary<string, bool>();
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(10);
        private readonly PointAccountType _expPointAccountType;

        public static StubPointAccountTypeCache Instance { get; private set; }

        public StubPointAccountTypeCache()
        {
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            Instance ??= this;

            // Create a service collection with required services
            var services = new ServiceCollection();
            services.AddMemoryCache();
            services.AddLogging(builder => builder.AddConsole());
            services.AddSingleton<IPointAccountTypeCache>(this);
            services.AddSingleton<IHybridCacheService>(new StubHybridCacheService());
            services.AddSingleton<IPointAccountTypeAdapter>(new StubPointAccountTypeAdapter());
            PointAccountTypeCache.Initialize(services.BuildServiceProvider());

            _expPointAccountType = new PointAccountType(
                "", // name
                "Active", // status
                "SystemDefaultExpiration", // description
                "SourceOfPoints", // source
                PointLedgerTypeStrings.EXPIRED, // ledgerType
                30, // pointsLifespanDays
                null, // expirationPointAccountTypeId
                PointLedgerTypeStrings.ARCHIVE, // archiveType
                //730, // archiveAfterDays
                //null, // archivePointAccountTypeId
                false, // isSystemType
                "AwayFromZero", 0,
                "TestTenant",
                Guid.NewGuid().ToString() // Use a new ID for the test expiration type
            );
        }

        public virtual async Task<PointAccountType> GetPointAccountTypeAsync(string tenantId, string pointAccountTypeId)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrWhiteSpace(pointAccountTypeId)) throw new ArgumentNullException(nameof(pointAccountTypeId));

            var cacheKey = $"{tenantId}_{pointAccountTypeId}";

            if (_memoryCache.TryGetValue(cacheKey, out PointAccountType cachedValue))
            {
                return cachedValue;
            }

            // For testing, if not found in cache, return null
            return null;
        }

        public static Task<PointAccountType> GetPointAccountType(string tenantId, string pointAccountTypeId)
        {
            if (Instance == null)
            {
                throw new InvalidOperationException("PointAccountTypeCache instance is not initialized.");
            }

            return Instance.GetPointAccountTypeAsync(tenantId, pointAccountTypeId);
        }

        public async Task<PointAccountType> GetExpirationPointAccountTypeAsync(string tenantId, string type = null, decimal? ttl = null)
        {
            // First check if we already have a cached expiration type
            var expType = await GetPointAccountTypeAsync(tenantId, _expPointAccountType.Id);
            if (expType != null)
            {
                return expType;
            }

            // If not found, create and cache a new one
            var expirationPointType = new PointAccountType(
                "", // name
                "Active", // status
                "SystemDefaultExpiration", // description
                "SourceOfPoints", // source
                type ?? PointLedgerTypeStrings.EXPIRED, // ledgerType
                ttl ?? 30, // pointsLifespanDays
                null, // expirationPointAccountTypeId
                PointLedgerTypeStrings.ARCHIVE, // archiveType
                //730, // archiveAfterDays
                //null, // archivePointAccountTypeId
                false, // isSystemType
                "AwayFromZero", 0,
                tenantId,
                _expPointAccountType.Id // Use the same ID as our test expiration type
            );

            // Cache the expiration type
            CachePointAccountType(tenantId, expirationPointType);

            return expirationPointType;
        }

        public bool CachePointAccountType(string tenantId, PointAccountType pointAccountType)
        {
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (pointAccountType == null) throw new ArgumentNullException(nameof(pointAccountType));
            if (string.IsNullOrWhiteSpace(pointAccountType.Id)) throw new ArgumentNullException(nameof(pointAccountType.Id));

            var cacheKey = $"{tenantId}_{pointAccountType.Id}";

            _memoryCache.Set(
                cacheKey,
                pointAccountType,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _cacheDuration
                });

            _cacheKeys[cacheKey] = true;
            return true;
        }

        /// <summary>Registers TenantA and TenantB point account types for rules engine tests that use TestCampaignFactory campaigns.</summary>
        public void RegisterTestCampaignFactoryPointAccountTypes()
        {
            var tenantA = TestCampaignFactory.TenantA;
            var tenantB = TestCampaignFactory.TenantB;
            var tqpA = new PointAccountType(null, "Active", "TQP TenantA", null, "TQP", 365, null, null, false, "AwayFromZero", 0, tenantA, TestCampaignFactory.TqpPointAccountIdTenantA);
            var spendA = new PointAccountType(null, "Active", "Spendable TenantA", null, PointLedgerTypeStrings.SPENDABLE, 30, null, null, true, "AwayFromZero", 0, tenantA, TestCampaignFactory.SpendablePointAccountIdTenantA);
            var tqpB = new PointAccountType(null, "Active", "TQP TenantB", null, "TQP", 365, null, null, false, "AwayFromZero", 0, tenantB, TestCampaignFactory.TqpPointAccountIdTenantB);
            var spendB = new PointAccountType(null, "Active", "Spendable TenantB", null, PointLedgerTypeStrings.SPENDABLE, 30, null, null, true, "AwayFromZero", 0, tenantB, TestCampaignFactory.SpendablePointAccountIdTenantB);
            CachePointAccountType(tenantA, tqpA);
            CachePointAccountType(tenantA, spendA);
            CachePointAccountType(tenantB, tqpB);
            CachePointAccountType(tenantB, spendB);
        }

        public Task<bool> EnsurePATsLoaded(string tenantId)
        {
            throw new NotImplementedException();
        }

        public Task<List<PointAccountType>> GetAllPointAccountTypes(string tenantId)
        {
            throw new NotImplementedException();
        }

        public Task InvalidatePointAccountTypeAsync(string tenantId, string pointAccountTypeId)
        {
            var cacheKey = $"{tenantId}_{pointAccountTypeId}";
            _memoryCache.Remove(cacheKey);
            _cacheKeys.TryRemove(cacheKey, out _);
            return Task.CompletedTask;
        }

        public Task InvalidateTenantPointAccountTypesAsync(string tenantId)
        {
            var prefix = $"{tenantId}_";
            foreach (var key in _cacheKeys.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
            {
                _memoryCache.Remove(key);
                _cacheKeys.TryRemove(key, out _);
            }
            return Task.CompletedTask;
        }
    }
}
