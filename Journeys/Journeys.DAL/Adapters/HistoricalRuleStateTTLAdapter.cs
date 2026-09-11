using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.DTO.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Journeys.DAL.Adapters
{
    public class HistoricalRuleStateTTLAdapter : BaseAdapter<HistoricalRuleEventTTL>, IHistoricalRuleStateTTLAdapter
    {
        private const string MODEL_ID = "a7b83e21-9d4f-4c1a-b2e6-5f8a3d0c9e71";
        private const int PageSize = 500;

        public HistoricalRuleStateTTLAdapter(IDynamicDataAdapter dynAdapter) : base(dynAdapter) { }

        public async Task<List<HistoricalRuleEventTTL>> GetExpiredAsync(string tenantId, string loyaltyAccountId, string stateKey, DateTimeOffset asOfUtc)
        {
            //var partitionKey = HistoricalRuleEventTTL.GetPartitionKey(loyaltyAccountId, stateKey);
            var allExpired = new List<HistoricalRuleEventTTL>();
            string continuationToken = null;

            try
            {
                do
                {
                    var page = await FetchEntityByKeyAsync(tenantId, loyaltyAccountId, MODEL_ID, PageSize, stateKey, continuationToken);
                    if (page?.Entities == null || page.Entities.Count == 0)
                        break;

                    var expired = page.Entities.Where(e => e.TimeToLive < asOfUtc).ToList();
                    allExpired.AddRange(expired);
                    continuationToken = page.ContinuationToken;
                } while (!string.IsNullOrEmpty(continuationToken));
            }
            catch (APIErrorsException ex) when (ex.Errors?.ContainsKey("NOT_FOUND") == true)
            {
                // Empty partition
            }

            return allExpired;
        }

        public async Task DeleteManyAsync(string tenantId, string loyaltyAccountId, string stateKey, IReadOnlyList<HistoricalRuleEventTTL> records)
        {
            if (records == null || records.Count == 0) return;

            //var partitionKey = HistoricalRuleEventTTL.GetPartitionKey(loyaltyAccountId, stateKey);
            var pks = new Dictionary<string, string> { 
                { "loyaltyaccountid", loyaltyAccountId },
                { "statekey", stateKey }
            };

            foreach (var r in records)
            {
                await DeleteEntityAsync(tenantId, MODEL_ID, r.Id, pks);
            }
        }

        public async Task UpsertEventTtlAsync(string tenantId, string loyaltyAccountId, string stateKey, string eventType, string eventId, DateTimeOffset ttl, decimal? contributionValue)
        {
            var entity = new HistoricalRuleEventTTL(tenantId, loyaltyAccountId, stateKey, eventType, eventId, ttl, contributionValue);
            entity.Id = EventKeyUtility.ToEventKey(eventType, eventId);
            await UpsertEntityAsync(tenantId, MODEL_ID, entity);
        }

        public async Task<HistoricalRuleEventTTL?> GetByEventAsync(string tenantId, string loyaltyAccountId, string stateKey, string eventType, string eventId)
        {
            if (string.IsNullOrEmpty(eventType) || string.IsNullOrEmpty(eventId))
                return null;
            try
            {
                var id = EventKeyUtility.ToEventKey(eventType, eventId);
                return await FetchEntityAsync(tenantId, id, MODEL_ID, loyaltyAccountId, stateKey);
            }
            catch (APIErrorsException ex) when (ex.Errors?.ContainsKey("NOT_FOUND") == true)
            {
                return null;
            }
        }
    }
}
