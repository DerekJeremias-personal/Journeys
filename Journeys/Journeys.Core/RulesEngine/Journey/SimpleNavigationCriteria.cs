using Journeys.Core.JsonConverters;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Journey.Enums;
using Journeys.Core.RulesEngine.Outcomes;
using Journeys.Core.RulesEngine.Rules;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Journeys.Core.RulesEngine.Journey
{
    public class SimpleNavigationCriteria : INavigationCriteria
    {
        [JsonConstructor]
        public SimpleNavigationCriteria(string name, NavigationType navigationType, RuleBase navConstraint, List<OutcomeBase>? outcomes)
        {
            Name = name;
            NavigationType = navigationType;
            NavConstraint = navConstraint;
            Outcomes = outcomes;
        }

        public SimpleNavigationCriteria() { }

        public string Name { get; set; }

        public RuleBase NavConstraint { get; set; }

        public List<OutcomeBase>? Outcomes { get; set; }

        public NavigationType NavigationType { get; set; }

        public async Task<List<OutcomeResult>> NavigateAndAwardNavigationOutcomesAsync(RulesEngineState engineState, JourneyNode parent, CancellationToken token)
        {
            // NOTE: State mutations have been moved to JourneyStateManager.
            // This method now only calculates navigation outcomes without mutating state.
            // The caller is responsible for applying state changes via JourneyStateManager.

            var outcomes = new List<OutcomeResult>();
            Outcomes ??= new List<OutcomeBase>();
            
            foreach (var o in Outcomes)
            {
                var result = await o.CalculateOutcomeAsync(engineState, token);
                if (result != null)
                {
                    outcomes.Add(result);
                }
            }

            return outcomes;
        }

        public async Task<(bool shouldNavigate, INavigationCriteria nav)> ShouldNavigateAsync(RulesEngineState enginePayload, CancellationToken token)
        {
            var result = await NavConstraint.Evaluate(enginePayload, token);
            return new(result, this);
        }

    }
}
