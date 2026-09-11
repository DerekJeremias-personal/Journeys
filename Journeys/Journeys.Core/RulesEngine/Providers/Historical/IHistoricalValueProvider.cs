using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Providers.Historical.State;
using System.Text.Json.Serialization;

namespace Journeys.Core.RulesEngine.Providers.Historical
{
    [JsonDerivedType(typeof(SimpleCalculationProvider), typeDiscriminator: nameof(SimpleCalculationProvider))]
    public interface IHistoricalValueProvider : IValueProvider
    {
        string Id { get; set; }
        Task<bool> ShouldCalculateAsync(RulesEngineState entity, CancellationToken token);
        Task<HistoricalStateBase?> LoadStateAsync(RulesEngineState state, CancellationToken token);
    }
}
