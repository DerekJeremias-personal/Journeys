using Journeys.Core.RulesEngine.Providers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Journeys.Core.RulesEngine.Providers.Historical;
using Journeys.Core.RulesEngine.Providers.Historical.State;

namespace Journeys.Core.JsonConverters
{
    public class LoyaltyAccountRuleStateJsonConverter : JsonTypeConverter<HistoricalStateBase>
    {
        private static readonly Dictionary<string, Type> _typeMap = new Dictionary<string, Type>
        {
            {new SimpleCalculationProvider().Kind, typeof(SimpleCalculationProvider)}
        };

        protected override HistoricalStateBase Create(Type objectType, JsonElement jsonObject, JsonSerializerOptions options)
        {
            return CreateFromMap(objectType, jsonObject, options, nameof(HistoricalStateBase.Kind), _typeMap);
        }
    }
}
