using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Journeys.Core.RulesEngine.Rules;

namespace Journeys.Core.JsonConverters
{
    public abstract class JsonTypeConverter<T> : JsonConverter<T>
    {
        // Override the Read method to handle deserialization
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return default;

            // Use JsonDocument to parse the JSON object
            using var jsonDocument = JsonDocument.ParseValue(ref reader);
            var jsonObject = EvaluationComparisonJsonNormalizer.Normalize(jsonDocument.RootElement);

            EnsureObjectRoot(jsonObject, typeToConvert);

            // Create target object based on JsonObject
            T target = Create(typeToConvert, jsonObject, options);

            // Populate the object properties
            PopulateTargetObject(jsonObject, target, options);

            return target;
        }

        public virtual T CreateFromMap(Type objectType, JsonElement jsonObject, JsonSerializerOptions options, string discriminatorPropertyName, Dictionary<string, Type> typeMap)
        {
            EnsureObjectRoot(jsonObject, objectType);

            if (!jsonObject.TryGetProperty(discriminatorPropertyName, out var discriminatorElement) || discriminatorElement.ValueKind == JsonValueKind.Undefined)
                if (!jsonObject.TryGetProperty(discriminatorPropertyName.ToLower(), out discriminatorElement) || discriminatorElement.ValueKind == JsonValueKind.Undefined)
                    throw new Exception($"Missing discriminator field '{discriminatorPropertyName}' for base type '{objectType.Name}'. Recognized values are: {string.Join(", ", typeMap.Keys)}.");

            var discriminator = discriminatorElement.GetString();
            if (discriminator == null)
                throw new Exception($"Discriminator field '{discriminatorPropertyName}' is null for base type '{objectType.Name}'.");

            if (typeMap.TryGetValue(discriminator, out Type discriminatedType))
                return (T)Activator.CreateInstance(discriminatedType);
            else if (typeMap.TryGetValue(discriminator.ToLower(), out Type discriminatedType2))
                return (T)Activator.CreateInstance(discriminatedType2);

            throw new Exception($"Unrecognized discriminator '{discriminator}' for base type '{objectType.Name}'. Recognized values are: {string.Join(", ", typeMap.Keys)}.");
        }


        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStartObject(); // Start the JSON object.

            // Get the properties of the object.
            var properties = value.GetType().GetProperties().Where(p => p.CanRead);
            foreach (var property in properties)
            {
                // Get the property name, applying the naming policy from the options if it exists.
                var propertyName = options.PropertyNamingPolicy?.ConvertName(property.Name) ?? property.Name;

                // Write the property name.
                writer.WritePropertyName(propertyName);

                // Get the value of the property.
                var propertyValue = property.GetValue(value);

                // Serialize the property value. This handles both simple and complex types.
                JsonSerializer.Serialize(writer, propertyValue, property.PropertyType, options);
            }

            writer.WriteEndObject(); // End the JSON object.
        }

        public override bool CanConvert(Type typeToConvert)
        {
            return typeof(T).IsAssignableFrom(typeToConvert);
        }

        protected abstract T Create(Type objectType, JsonElement jsonObject, JsonSerializerOptions options);

        protected virtual void PopulateTargetObject(JsonElement jsonObject, T target, JsonSerializerOptions options)
        {
            var targetType = target.GetType();
            foreach (var property in targetType.GetProperties())
            {
                if (!property.CanWrite)
                {
                    continue; // Skip non-settable properties
                }

                var jsonPropertyName = options.PropertyNamingPolicy?.ConvertName(property.Name) ?? property.Name;
                if (jsonObject.TryGetProperty(jsonPropertyName, out var jsonProperty))
                {
                    object propertyValue;

                    // Check if the property is a complex type
                    if (IsComplexType(property.PropertyType))
                    {
                        propertyValue = JsonSerializer.Deserialize(jsonProperty.GetRawText(), property.PropertyType, options);
                    }
                    else if (typeof(IDictionary).IsAssignableFrom(property.PropertyType) && property.PropertyType.IsGenericType)
                    {
                        // Handle deserialization of dictionaries
                        propertyValue = DeserializeDictionary(jsonProperty, property.PropertyType, options);
                    }
                    else if (typeof(IEnumerable).IsAssignableFrom(property.PropertyType) && property.PropertyType != typeof(string))
                    {
                        // Handle deserialization of arrays or collections
                        propertyValue = DeserializeArrayOrList(jsonProperty, property.PropertyType, options);
                    }
                    else
                    {
                        // Handle deserialization of simple types
                        propertyValue = jsonProperty.Deserialize(property.PropertyType, options);
                    }

                    property.SetValue(target, propertyValue);
                }
            }
        }

        private object DeserializeArrayOrList(JsonElement jsonElement, Type propertyType, JsonSerializerOptions options)
        {
            // Determine the element type of the array or list
            Type elementType = propertyType.IsArray ? propertyType.GetElementType() : propertyType.GenericTypeArguments[0];

            if (IsComplexType(elementType))
            {
                // Deserialize as a list of complex objects
                var listType = typeof(List<>).MakeGenericType(elementType);
                var list = (IList)Activator.CreateInstance(listType);

                foreach (var item in jsonElement.EnumerateArray())
                {
                    var element = JsonSerializer.Deserialize(item.GetRawText(), elementType, options);
                    list.Add(element);
                }

                // If the original property is an array, convert the list back to an array
                return propertyType.IsArray ? ConvertListToArray(list, elementType) : list;
            }
            else
            {
                // For non-complex element types, use the built-in deserialization
                return JsonSerializer.Deserialize(jsonElement.GetRawText(), propertyType, options);
            }
        }

        private Array ConvertListToArray(IList list, Type elementType)
        {
            var array = Array.CreateInstance(elementType, list.Count);
            list.CopyTo(array, 0);
            return array;
        }

        private object DeserializeDictionary(JsonElement jsonElement, Type propertyType, JsonSerializerOptions options)
        {
            var dictionaryTypes = propertyType.GetGenericArguments();
            var keyType = dictionaryTypes[0];
            var valueType = dictionaryTypes[1];
            var dictionaryType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
            var dictionary = Activator.CreateInstance(dictionaryType);

            foreach (var element in jsonElement.EnumerateObject())
            {
                var key = JsonSerializer.Deserialize(element.Name, keyType, options);

                object value;
                if (IsComplexType(valueType))
                {
                    value = JsonSerializer.Deserialize(element.Value.GetRawText(), valueType, options);
                }
                else
                {
                    // For non-complex types (primitives, strings, etc.)
                    value = element.Value.Deserialize(valueType, options);
                }

                dictionaryType.GetMethod("Add").Invoke(dictionary, new[] { key, value });
            }

            return dictionary;
        }

        private bool IsComplexType(Type type)
        {
            return !type.IsPrimitive && type != typeof(string) && type != typeof(DateTime) && !type.IsEnum;
        }

        private static void EnsureObjectRoot(JsonElement jsonObject, Type typeToConvert)
        {
            if (jsonObject.ValueKind == JsonValueKind.Object)
                return;

            var ruleHint = typeof(RuleBase).IsAssignableFrom(typeToConvert)
                ? jsonObject.ValueKind == JsonValueKind.String
                    ? " Rule JSON was stringified; embed the rule as a JSON object, not a quoted string."
                    : " Rule trees must be a single object with Kind; wrap multiple rules in AndRule.Children."
                : " Check the JSON shape for this polymorphic type.";

            throw new JsonException(
                $"Cannot deserialize {typeToConvert.Name}: expected a JSON object with discriminator property, " +
                $"but received {jsonObject.ValueKind}.{ruleHint}");
        }

    }

}
