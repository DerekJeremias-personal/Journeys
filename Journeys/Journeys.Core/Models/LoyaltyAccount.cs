using Journeys.Core.Interfaces.Entities;
using Journeys.Core.JsonConverters;
using Journeys.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Journeys.Core.Models
{
    public class LoyaltyAccount : TenantedModelBase, IReferenceable
    {
        [JsonConstructor]
        public LoyaltyAccount(string extAccountId, string type, string status, string? lockLeaseKey, DateTimeOffset? lockLeaseExpiration, 
            List<Tag>? tags, List<LoyaltyAccountJourney>? journeys, List<string>? knownExternalIds,
            string tenantId, string id) : base(tenantId, id)
        {
            ExtAccountId = extAccountId;
            Type = type;
            Status = status;
            Tags = tags;
            Journeys = journeys;
            KnownExternalIds = knownExternalIds;
            Id = id;
            TenantId = tenantId;
            LockLeaseKey = lockLeaseKey;
            LockLeaseExpiration = lockLeaseExpiration;
        }

        /// <summary>
        /// In support of IReferenceable
        /// </summary>
        public string ExternalId
        {
            get
            {
                return ExtAccountId;
            }
        }

        /// <summary>
        /// In support of IReferenceable
        /// </summary>
        public string ExternalIdType
        {
            get
            {
                return LoyaltyAccountService.EXT_ID_TYPE;
            }
        }


        public string? ExtAccountId { get; set; }
        public string? Type { get; set; }
        public string Status { get; set; }

        public string? LockLeaseKey { get; set; }
        public DateTimeOffset? LockLeaseExpiration { get; set; }


        public List<string>? KnownExternalIds { get; set; } 

        public List<LoyaltyAccountJourney> Journeys { get; set; } = new List<LoyaltyAccountJourney>();

        [JsonIgnore]
        public object? AccountDetails { get; set; }

        [JsonIgnore]
        public List<Tag>? Tags { get; set; }

        [JsonIgnore]
        public List<Campaign> Campaigns { get; set; } = new List<Campaign>();

        private List<PointLedger> _pointLedgers;
        [JsonIgnore]
        public List<PointLedger> PointLedgers
        {
            get
            {
                return _pointLedgers;
            }
            set { _pointLedgers = value; }
        }

        [JsonIgnore]
        public bool ResettleASAP { get; set; }

        /// <summary>
        /// Aggregated state for historical rules. Key = RuleBase.GetHistoricalStateKey(campaignId, ruleId).
        /// </summary>
        public Dictionary<string, HistoricalRuleState> RuleState { get; set; } = new Dictionary<string, HistoricalRuleState>();
    }
}
