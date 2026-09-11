using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.RulesEngine.Providers.Historical;
using Journeys.Core.RulesEngine.Rules.Composite;
using Journeys.Core.RulesEngine.Outcomes;

namespace Journeys.Core.JsonConverters
{
    public class RuleBaseJsonConverter : JsonTypeConverter<RuleBase>
    {
        private static readonly Dictionary<string, Type> _typeMap = new Dictionary<string, Type>
        {
            {new AndRule().Kind, typeof(AndRule)},
            {new OrRule().Kind, typeof(OrRule)},
            {new NotRule().Kind, typeof(NotRule)},
            {new SimpleRule<object>().Kind, typeof(SimpleRule<object>)},
            {new DatePropertyRule().Kind, typeof(DatePropertyRule)},
            {new HistoricalRule().Kind, typeof(HistoricalRule)},
            {new NumericPropertyRule().Kind, typeof(NumericPropertyRule)},
            {new StringPropertyRule().Kind, typeof(StringPropertyRule)},
            {new TaxonomicRule().Kind, typeof(TaxonomicRule)}
        };

        protected override RuleBase Create(Type objectType, JsonElement jsonObject, JsonSerializerOptions options)
        {
            return CreateFromMap(objectType, jsonObject, options, nameof(RuleBase.Kind), _typeMap);
        }
    }

    public class ProviderBaseJsonConverter : JsonTypeConverter<ProviderBase>
    {
        private static readonly Dictionary<string, Type> _typeMap = new Dictionary<string, Type>
        {
            {new SimpleCalculationProvider().Kind, typeof(SimpleCalculationProvider)},
            {new AggregateValueProvider().Kind, typeof(AggregateValueProvider)},
            {new ConstantValueProvider().Kind, typeof(ConstantValueProvider)},
            {new LineageValueProvider().Kind, typeof(LineageValueProvider)},
            {new PathValueProvider().Kind, typeof(PathValueProvider)}
        };

        protected override ProviderBase Create(Type objectType, JsonElement jsonObject, JsonSerializerOptions options)
        {
            return CreateFromMap(objectType, jsonObject, options, nameof(ProviderBase.Kind), _typeMap);
        }
    }

    public class OutcomeBaseJsonConverter : JsonTypeConverter<OutcomeBase>
    {
        private static readonly Dictionary<string, Type> _typeMap = new Dictionary<string, Type>
        {
            {new DepositPointsOutcome().Kind, typeof(DepositPointsOutcome)},
            {new SpendPointsOutcome().Kind, typeof(SpendPointsOutcome)},
            {new ExpirePointsOutcome().Kind, typeof(ExpirePointsOutcome)},
            {new TagOutcome().Kind, typeof(TagOutcome)},
            {new NotificationOutcome().Kind, typeof(NotificationOutcome)},
            {new WorkflowOutcome().Kind, typeof(WorkflowOutcome)}
        };

        protected override OutcomeBase Create(Type objectType, JsonElement jsonObject, JsonSerializerOptions options)
        {
            return CreateFromMap(objectType, jsonObject, options, nameof(OutcomeBase.Kind), _typeMap);
        }
    }
}
