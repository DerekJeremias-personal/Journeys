using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using Journeys.Core.Models;

namespace Journeys.Infra.Backend
{
    public class PolymorphicJsonConverter<T> : JsonConverter<T> where T : ModelBase
    {
        public Dictionary<string, Type> TypeMapping { get; }
        public string PropertyName;

        public PolymorphicJsonConverter(Dictionary<string, Type> map, string propertyName)
        {
            TypeMapping = map ?? new Dictionary<string, Type>();
            PropertyName = propertyName;
        }

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using (JsonDocument jsonDoc = JsonDocument.ParseValue(ref reader))
            {
                JsonElement root = jsonDoc.RootElement;

                // Check for a type discriminator in the JSON
                if (TryGetCaseInsensitiveProperty(root, out JsonElement propertyValue))
                {
                    string typeName = propertyValue.GetString();
                    if (!string.IsNullOrEmpty(typeName) && TypeMapping.TryGetValue(typeName, out Type targetType))
                    {
                        // Deserialize JSON into the identified derived type
                        return (T)JsonSerializer.Deserialize(root.GetRawText(), targetType, options);
                    }
                    else
                    {
                        throw new JsonException($"Unknown type discriminator: {typeName}");
                    }
                }
                throw new JsonException("Type discriminator ('type') not found in JSON.");
            }
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            string typeName = value.GetType().Name;
            string json = JsonSerializer.Serialize(value, value.GetType(), options);

            using (JsonDocument jsonDoc = JsonDocument.Parse(json))
            {
                writer.WriteStartObject();
                writer.WriteString(PropertyName, typeName);
                foreach (var element in jsonDoc.RootElement.EnumerateObject())
                {
                    element.WriteTo(writer);
                }
                writer.WriteEndObject();
            }
        }

        private bool TryGetCaseInsensitiveProperty(JsonElement data, out JsonElement propertyValue)
        {
            propertyValue = default;

            foreach (var property in data.EnumerateObject())
            {
                if (string.Equals(property.Name, PropertyName, StringComparison.OrdinalIgnoreCase))
                {
                    propertyValue = property.Value;
                    return true;
                }
            }

            return false;
        }
    }
}
