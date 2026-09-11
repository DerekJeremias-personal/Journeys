using Journeys.Core.RulesEngine.Comparitors;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Providers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.Core.RulesEngine.Rules
{
    public class TemporalConstraintRule : RuleBase
    {
        public override string Kind => RuleKindDiscriminators.TemporalConstraintRule;
        public PathValueProvider TimeOfOccurrenceProvider { get; set; }
        public TemporalEvaluation TemporalEvaluation { get; set; }
        public TimeSpan Comparison { get; set; }

        [JsonConstructor]
        public TemporalConstraintRule(PathValueProvider timeOfOccurrenceProvider, TemporalEvaluation temporalEvaluation, TimeSpan comparison)
        {
            TimeOfOccurrenceProvider = timeOfOccurrenceProvider;
            TemporalEvaluation = temporalEvaluation;
            Comparison = comparison;
        }

        public async override Task<bool> Evaluate(RulesEngineState state, CancellationToken token)
        {
            var timeOfOccurrence = await TimeOfOccurrenceProvider.GetValue<DateTimeOffset>(state, token);
            return TemporalEvaluation.EvaluateTemporalInclusion(timeOfOccurrence, Comparison);
        }

        public async Task<DateTimeOffset> CalculateDecayDate(RulesEngineState state, CancellationToken token)
        {
            var timeOfOccurrence = await TimeOfOccurrenceProvider.GetValue<DateTimeOffset>(state, token);
            var decayDate = TemporalEvaluation.CalculateDecayDate(timeOfOccurrence, Comparison);
            return decayDate;
        }
    }
}
