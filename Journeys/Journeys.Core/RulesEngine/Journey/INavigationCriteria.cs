using Journeys.Core.RulesEngine.Providers.Historical;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Journey.Enums;
using Journeys.Core.RulesEngine.Outcomes;
using System.Text.Json.Serialization;

namespace Journeys.Core.RulesEngine.Journey
{
    [JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
    [JsonDerivedType(typeof(SimpleNavigationCriteria), typeDiscriminator: "SimpleNavigationCriteria")]
    public interface INavigationCriteria
    {
        NavigationType NavigationType { get; }
        //string? SerializedNavConstraint { get; set; }
        //string? SerializedOutcomes { get; set; }
        Task<(bool shouldNavigate, INavigationCriteria nav)> ShouldNavigateAsync(RulesEngineState enginePayload, CancellationToken token);
        Task<List<OutcomeResult>> NavigateAndAwardNavigationOutcomesAsync(RulesEngineState engineState, JourneyNode parent, CancellationToken token);

        //Task<string> SerializeConstraintAsync();
        //Task<INavigationCriteria> DeserializeConstraintAsync();
    }
}
