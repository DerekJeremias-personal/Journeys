using System;
using System.Text.Json.Serialization;
using Journeys.Core.Utility;

namespace Journeys.Core.Models
{
    /// <summary>
    /// One record per contributing event. Partition = LoyaltyAccountId|StateKey; Id = eventtype|eventid (lowercase, see <see cref="EventKeyUtility.ToEventKey"/>).
    /// </summary>
    public class HistoricalRuleEventTTL : TenantedModelBase
    {
        /// <summary>Partition key: LoyaltyAccountId|StateKey.</summary>
        //public static string GetPartitionKey(string loyaltyAccountId, string stateKey) => $"{loyaltyAccountId}|{stateKey}";

        public string LoyaltyAccountId { get; set; }
        public string StateKey { get; set; }
        public string EventType { get; set; }
        public string EventId { get; set; }
        public DateTimeOffset TimeToLive { get; set; }
        public decimal? ContributionValue { get; set; }

        //[JsonIgnore]
        ///// <summary>Partition key for the store (LoyaltyAccountId|StateKey). Used for routing and query.</summary>
        //public string PartitionKey => GetPartitionKey(LoyaltyAccountId, StateKey);

        //public HistoricalRuleEventTTL() : base("", null) { }

        public HistoricalRuleEventTTL(string tenantId, string loyaltyAccountId, string stateKey,
            string eventType, string eventId, DateTimeOffset timeToLive, decimal? contributionValue)
            : base(tenantId, EventKeyUtility.ToEventKey(eventType, eventId))
        {
            LoyaltyAccountId = loyaltyAccountId;
            StateKey = stateKey;
            EventType = eventType;
            EventId = eventId;
            TimeToLive = timeToLive;
            ContributionValue = contributionValue;
        }
    }
}
