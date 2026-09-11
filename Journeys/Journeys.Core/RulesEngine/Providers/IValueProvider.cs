using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Providers.Historical;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.Core.RulesEngine.Providers
{
    [JsonDerivedType(typeof(SimpleCalculationProvider), typeDiscriminator: nameof(SimpleCalculationProvider))]
    [JsonDerivedType(typeof(AggregateValueProvider), typeDiscriminator: nameof(AggregateValueProvider))]
    [JsonDerivedType(typeof(ConstantValueProvider), typeDiscriminator: nameof(ConstantValueProvider))]
    [JsonDerivedType(typeof(PathValueProvider), typeDiscriminator: nameof(PathValueProvider))]
    [JsonDerivedType(typeof(PointBalanceProvider), typeDiscriminator: nameof(PointBalanceProvider))]
    public interface IValueProvider
    {
        Task<T?> GetValue<T>(RulesEngineState entity, CancellationToken token);
    }
}
