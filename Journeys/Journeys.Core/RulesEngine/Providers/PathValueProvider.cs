using Backend.Dto.Dynamic;
using Backend.Dto.Interfaces;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Rules;
using System.Reflection;
using System.Text.Json;

namespace Journeys.Core.RulesEngine.Providers
{
    public class PathValueProvider : ProviderBase, IValueProvider
    {
        public override string Kind => ProviderKindDiscriminators.PathValueProvider;

        public string PropertyPath { get; set; }
        public PathValueProvider()
        {
            PropertyPath = string.Empty;
        }
        public PathValueProvider(string propertyPath)
        {
            PropertyPath = propertyPath;
        }

        private T? UnboxType<T>(object? value)
        {
            if (value is null)
            {
                return default(T?);
            }

            if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Undefined)
            {
                return default(T?);
            }

            var valueType = value.GetType();
            var targetType = typeof(T);
            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            var convertableType = typeof(IConvertible);

            if (typeof(T?).IsAssignableFrom(valueType))
            {
                return (T?)value;
            }
            else if (valueType.IsAssignableTo(convertableType) && underlyingType.IsAssignableTo(convertableType))
            {
                return (T?)Convert.ChangeType(value, underlyingType);
            }
            else if (typeof(IDynamicEntity).IsAssignableFrom(targetType))
            {
                var node = DynamicHelper.Import(value);
                return (T?)node;
            }
            else if (typeof(T).IsAssignableFrom(typeof(DateOnly)))
            {
                if (value is DateTime dt)
                {
                    return (T?)(object)DateOnly.FromDateTime(dt);
                }
                else if (DateOnly.TryParse(value.ToString(), out DateOnly date))
                {
                    return (T?)(object)date;
                }
                else
                {
                    throw new InvalidCastException($"Unable to convert {valueType.FullName} to System.DateOnly");
                }
            }
            else if (typeof(T).IsAssignableFrom(typeof(DateTimeOffset)))
            {
                if (value is DateTime dt)
                {
                    // Treat DateTime as UTC (offset +00:00) instead of local timezone
                    return (T?)(object)new DateTimeOffset(dt);
                }
                else if (DateTimeOffset.TryParse(value.ToString(), out DateTimeOffset dto))
                {
                    return (T?)(object)dto;
                }
                else
                {
                    throw new InvalidCastException($"Unable to convert {valueType.FullName} to System.DateTimeOffset with case DateTimeOffset specialized conversions.");
                }
            }
            else
            {
                throw new InvalidCastException($"Cannot convert {valueType.FullName} to {typeof(T).FullName}");
            }
        }

        public virtual T? SetValue<T>(ref IDynamicEntity obj, T value)
        {
            //var dynObj = DynamicHelper.Import(obj);
            var updatedValue = DynamicHelper.WriteNodeValue(obj, value, PropertyPath, true);
            //obj = dynObj;
            return value;
        }

        public virtual T? GetValue<T>(JsonElement obj) 
        {
            var dynNode = DynamicHelper.Import(obj);
            return GetValue<T>(dynNode);
        }

        public virtual T? GetValue<T>(IDynamicEntity obj)
        {
            var value = DynamicHelper.ReadNodeValue(obj, PropertyPath.ToLower());
            if (value == DynamicValue.Undefined)
            {
                return default(T?);
            }
            return UnboxType<T>(value);
        }

        public virtual async Task<T?> GetValue<T>(RulesEngineState entity, CancellationToken token)
        {
            var value = DynamicHelper.ReadNodeValue(entity, PropertyPath.ToLower());
            if (value == DynamicValue.Undefined)
            {
                return default(T?);
            }
            else
            {
                return UnboxType<T>(value);
            }
        }
    }
}
