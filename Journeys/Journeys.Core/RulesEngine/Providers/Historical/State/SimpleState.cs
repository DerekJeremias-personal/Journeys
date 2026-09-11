using Journeys.Core.RulesEngine.Rules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.Core.RulesEngine.Providers.Historical.State
{
    public class SimpleState : HistoricalStateBase
    {
        [JsonConstructor]
        public SimpleState(string providerId, int count, decimal value, DateTimeOffset? firstOccurrence,
                List<StateTTL>? contributorTTLsList = null) : base(providerId)
        {
            Count = count;
            Value = value;
            FirstOccurrence = firstOccurrence;
            ContributorTTLsList = contributorTTLsList;
        }

        public override string Kind => HistoricalLoyaltyAccountStateKindDiscriminators.SimpleState;

        public int Count { get; set; }
        public decimal Value { get; set; }
        public DateTimeOffset? FirstOccurrence { get; set; }

        public List<StateTTL>? ContributorTTLsList { get; set; }

        private Dictionary<string, DateTimeOffset>? _contributorTTLs;
        [JsonIgnore]
        public Dictionary<string, DateTimeOffset>? ContributorTTLs
        {
            get
            {
                if (ContributorTTLsList?.Count > 0)
                    return ContributorTTLsList
                        .GroupBy(state => state.DecayingEntityId)
                        .ToDictionary(group => group.Key, group => group.Min(state => state.TTL));
                if (_contributorTTLs == null)
                    _contributorTTLs = new Dictionary<string, DateTimeOffset>();
                return _contributorTTLs;
            }
            set
            {
                _contributorTTLs = value ?? new Dictionary<string, DateTimeOffset>();
                ContributorTTLsList = _contributorTTLs
                    .Select(t => new StateTTL(t.Key, t.Value))
                    .ToList();
            }
        }

    }

    public class StateTTL
    {
        [JsonConstructor]
        public StateTTL(string decayingEntityId, DateTimeOffset ttl, decimal? contributionValue = null)
        {
            DecayingEntityId = decayingEntityId;
            TTL = ttl;
            ContributionValue = contributionValue;
        }

        public string DecayingEntityId { get; set; }
        public DateTimeOffset TTL { get; set; }
        /// <summary>Optional; used for Sum/Avg so we can subtract on TimeToLive expiry.</summary>
        public decimal? ContributionValue { get; set; }
    }
}
