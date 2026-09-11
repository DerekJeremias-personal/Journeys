using Journeys.Core.Interfaces.Entities;
using Journeys.Core.RulesEngine.Providers.Historical.State;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.Core.Models
{
    public class LoyaltyAccountRuleState : TenantedModelBase, IAccountBasedEvent
    {
        private static string GenerateId(string loyaltyAccountId, string modelId)
        {
            return $"{loyaltyAccountId}|{modelId}";
        }

        public void SetLoyaltyAccount(LoyaltyAccount account)
        {
            LoyaltyAccountId = account.Id;
        }

         [JsonPropertyName("accountid")]
        public string LoyaltyAccountId { get; set; }
        [JsonPropertyName("event")]
        public LoyaltyEvent Event { get; set; }
        public string EventModelId { get; set; }

        public List<HistoricalStateBase> StateList { get; set; }


        private Dictionary<string, HistoricalStateBase> _state;
        [JsonIgnore]
        public Dictionary<string, HistoricalStateBase> State // { get; set; }
        {
            get
            {
                if (_state == null && StateList?.Count > 0)
                {
                    _state = StateList
                                ?.GroupBy(state => state.ProviderId)
                                ?.ToDictionary(
                                    group => group.Key,
                                    group => group.FirstOrDefault(state => state != null)
                                ) ?? new Dictionary<string, HistoricalStateBase>();
                }
                return _state;
            }
            set
            {
                _state = value;
                StateList = _state
                                ?.Select(state => state.Value)
                                ?.ToList() ?? new List<HistoricalStateBase>();
            }
        }

        [JsonConstructor]
        public LoyaltyAccountRuleState(string tenantId, string loyaltyAccountId, string modelId, List<HistoricalStateBase> stateList) : base(tenantId, GenerateId(loyaltyAccountId, modelId))
        {
            LoyaltyAccountId = loyaltyAccountId;
            EventModelId = modelId;
            StateList = stateList;
        }

        public class LoyaltyEvent
        {
            [JsonPropertyName("dealercompanyname")]
            public string DealerCompanyName { get; set; }
            public bool IsDisabled { get; set; }

        }

    }
}
