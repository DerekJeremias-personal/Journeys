using Journeys.Core.JsonConverters;
using Journeys.Core.Models;
using System.Text.Json.Serialization;

namespace Journeys.Core.RulesEngine.Providers.Historical.State
{
    [JsonConverter(typeof(LoyaltyAccountRuleStateJsonConverter))]
    public abstract class HistoricalStateBase : ProviderStateBase
    {
        [JsonIgnore]
        public string ModelId { get; set; }

        [JsonConstructor]
        protected HistoricalStateBase(string providerId) : base(providerId) { }
    }

    public static class HistoricalLoyaltyAccountStateKindDiscriminators
    {
        public const string SimpleState = "SimpleState";
    }
}
