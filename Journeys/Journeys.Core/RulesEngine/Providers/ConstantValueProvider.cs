using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Rules;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Journeys.Core.RulesEngine.Providers
{
    public class ConstantValueProvider : ProviderBase, IValueProvider
    {
        public override string Kind => ProviderKindDiscriminators.ConstantValueProvider;

        public object? Value { get; set; }
        public ConstantValueProvider()
        {
            Value = null;
        }

        public ConstantValueProvider(object value)
        {
            Value = value;
        }

        public async Task<T?> GetValue<T>(RulesEngineState entity, CancellationToken token)
        {
            if (Value is JsonElement)
            {
                if (((JsonElement)Value).ValueKind == JsonValueKind.Number)
                {
                    Value = ((JsonElement)Value).GetDecimal();
                }
                else
                {
                    Value = ((JsonElement)Value).Deserialize<T>();
                }
            }

            return Value != null ? (T?)Value : default;
        }
    }
}
