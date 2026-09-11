using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Journeys.Tests.Stubs
{
    public class StubHistoricalRuleStateTTLAdapter : IHistoricalRuleStateTTLAdapter
    {
        public Task<List<HistoricalRuleEventTTL>> GetExpiredAsync(string tenantId, string loyaltyAccountId, string stateKey, DateTimeOffset asOfUtc)
            => Task.FromResult(new List<HistoricalRuleEventTTL>());

        public Task DeleteManyAsync(string tenantId, string loyaltyAccountId, string stateKey, IReadOnlyList<HistoricalRuleEventTTL> records)
            => Task.CompletedTask;

        public Task UpsertEventTtlAsync(string tenantId, string loyaltyAccountId, string stateKey, string eventType, string eventId, DateTimeOffset ttl, decimal? contributionValue)
            => Task.CompletedTask;

        public Task<HistoricalRuleEventTTL?> GetByEventAsync(string tenantId, string loyaltyAccountId, string stateKey, string eventType, string eventId)
            => Task.FromResult<HistoricalRuleEventTTL?>(null);
    }

    /// <summary>
    /// TimeToLive adapter that stores TTLs and can return them as "expired" for decay tests.
    /// Set <see cref="ReturnAllStoredAsExpiredOnNextGet"/> to true; on the next HydrateState run,
    /// GetExpiredAsync returns all stored TTLs for each key, then DeleteManyAsync removes them.
    /// </summary>
    public class StatefulStubHistoricalRuleStateTTLAdapter : IHistoricalRuleStateTTLAdapter
    {
        private readonly object _lock = new object();
        private readonly Dictionary<string, List<HistoricalRuleEventTTL>> _store = new Dictionary<string, List<HistoricalRuleEventTTL>>();

        /// <summary>When true, next GetExpiredAsync for each key returns all stored TTLs for that key (so decay runs and DeleteManyAsync is called).</summary>
        public bool ReturnAllStoredAsExpiredOnNextGet { get; set; }

        /// <summary>When non-empty, GetExpiredAsync returns only stored TTLs whose EventId is in this set (for partial decay tests).</summary>
        public HashSet<string> ReturnAsExpiredEventIds { get; set; } = new HashSet<string>();

        public List<(string tenantId, string loyaltyAccountId, string stateKey, IReadOnlyList<HistoricalRuleEventTTL> records)> DeleteManyCalls { get; } = new();

        private static string Key(string tenantId, string loyaltyAccountId, string stateKey)
            => $"{tenantId}|{loyaltyAccountId}|{stateKey}";

        public Task<List<HistoricalRuleEventTTL>> GetExpiredAsync(string tenantId, string loyaltyAccountId, string stateKey, DateTimeOffset asOfUtc)
        {
            var key = Key(tenantId, loyaltyAccountId, stateKey);
            lock (_lock)
            {
                if (!_store.TryGetValue(key, out var list) || list == null)
                    return Task.FromResult(new List<HistoricalRuleEventTTL>());
                if (ReturnAllStoredAsExpiredOnNextGet)
                    return Task.FromResult(list.ToList());
                if (ReturnAsExpiredEventIds != null && ReturnAsExpiredEventIds.Count > 0)
                    return Task.FromResult(list.Where(t => ReturnAsExpiredEventIds.Contains(t.EventId)).ToList());
                var expired = list.Where(t => t.TimeToLive < asOfUtc).ToList();
                return Task.FromResult(expired);
            }
        }

        public Task DeleteManyAsync(string tenantId, string loyaltyAccountId, string stateKey, IReadOnlyList<HistoricalRuleEventTTL> records)
        {
            DeleteManyCalls.Add((tenantId, loyaltyAccountId, stateKey, records));
            var key = Key(tenantId, loyaltyAccountId, stateKey);
            lock (_lock)
            {
                if (_store.TryGetValue(key, out var list) && list != null)
                {
                    var ids = new HashSet<string>(records.Select(r => r.Id));
                    list.RemoveAll(t => ids.Contains(t.Id));
                }
            }
            return Task.CompletedTask;
        }

        public Task UpsertEventTtlAsync(string tenantId, string loyaltyAccountId, string stateKey, string eventType, string eventId, DateTimeOffset ttl, decimal? contributionValue)
        {
            var record = new HistoricalRuleEventTTL(tenantId, loyaltyAccountId, stateKey, eventType, eventId, ttl, contributionValue);
            var key = Key(tenantId, loyaltyAccountId, stateKey);
            lock (_lock)
            {
                if (!_store.TryGetValue(key, out var list))
                {
                    list = new List<HistoricalRuleEventTTL>();
                    _store[key] = list;
                }
                var existing = list.FirstOrDefault(t => t.EventType == eventType && t.EventId == eventId);
                if (existing != null)
                    list.Remove(existing);
                list.Add(record);
            }
            return Task.CompletedTask;
        }

        public Task<HistoricalRuleEventTTL?> GetByEventAsync(string tenantId, string loyaltyAccountId, string stateKey, string eventType, string eventId)
        {
            var key = Key(tenantId, loyaltyAccountId, stateKey);
            lock (_lock)
            {
                if (_store.TryGetValue(key, out var list) && list != null)
                {
                    var existing = list.FirstOrDefault(t => t.EventType == eventType && t.EventId == eventId);
                    if (existing != null)
                        return Task.FromResult<HistoricalRuleEventTTL?>(existing);
                }
            }
            return Task.FromResult<HistoricalRuleEventTTL?>(null);
        }
    }

    /// <summary>
    /// Records GetExpiredAsync and UpsertEventTtlAsync calls for unit test assertions.
    /// </summary>
    public class RecordingHistoricalRuleStateTTLAdapter : IHistoricalRuleStateTTLAdapter
    {
        public List<(string tenantId, string loyaltyAccountId, string stateKey, DateTimeOffset asOfUtc)> GetExpiredCalls { get; } = new();
        public List<(string tenantId, string loyaltyAccountId, string stateKey, string eventType, string eventId, DateTimeOffset ttl, decimal? contributionValue)> UpsertEventTtlCalls { get; } = new();
        public List<(string tenantId, string loyaltyAccountId, string stateKey, IReadOnlyList<HistoricalRuleEventTTL> records)> DeleteManyCalls { get; } = new();

        public Task<List<HistoricalRuleEventTTL>> GetExpiredAsync(string tenantId, string loyaltyAccountId, string stateKey, DateTimeOffset asOfUtc)
        {
            GetExpiredCalls.Add((tenantId, loyaltyAccountId, stateKey, asOfUtc));
            return Task.FromResult(new List<HistoricalRuleEventTTL>());
        }

        public Task DeleteManyAsync(string tenantId, string loyaltyAccountId, string stateKey, IReadOnlyList<HistoricalRuleEventTTL> records)
        {
            DeleteManyCalls.Add((tenantId, loyaltyAccountId, stateKey, records));
            return Task.CompletedTask;
        }

        public Task UpsertEventTtlAsync(string tenantId, string loyaltyAccountId, string stateKey, string eventType, string eventId, DateTimeOffset ttl, decimal? contributionValue)
        {
            UpsertEventTtlCalls.Add((tenantId, loyaltyAccountId, stateKey, eventType, eventId, ttl, contributionValue));
            return Task.CompletedTask;
        }

        public Task<HistoricalRuleEventTTL?> GetByEventAsync(string tenantId, string loyaltyAccountId, string stateKey, string eventType, string eventId)
            => Task.FromResult<HistoricalRuleEventTTL?>(null);
    }
}
