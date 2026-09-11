using Journeys.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.DataStorage
{
    public interface IHistoricalRuleStateTTLAdapter
    {
        /// <summary>Returns TimeToLive records in the partition where TimeToLive &lt; asOfUtc (expired).</summary>
        Task<List<HistoricalRuleEventTTL>> GetExpiredAsync(string tenantId, string loyaltyAccountId, string stateKey, DateTimeOffset asOfUtc);

        /// <summary>Deletes the given TimeToLive records (e.g. after applying decay).</summary>
        Task DeleteManyAsync(string tenantId, string loyaltyAccountId, string stateKey, IReadOnlyList<HistoricalRuleEventTTL> records);

        /// <summary>Upserts one event TimeToLive record (idempotent for same EventType|EventId).</summary>
        Task UpsertEventTtlAsync(string tenantId, string loyaltyAccountId, string stateKey, string eventType, string eventId, DateTimeOffset ttl, decimal? contributionValue);

        /// <summary>Returns the TimeToLive record for this event in the given state key partition, or null if not found. Used for idempotent delta updates.</summary>
        Task<HistoricalRuleEventTTL?> GetByEventAsync(string tenantId, string loyaltyAccountId, string stateKey, string eventType, string eventId);
    }
}
