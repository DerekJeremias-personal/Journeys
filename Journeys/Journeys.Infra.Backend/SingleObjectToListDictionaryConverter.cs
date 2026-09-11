using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;

namespace Journeys.Infra.Backend
{
    public class SingleObjectToListDictionaryConverter<TValue> : JsonConverter<Dictionary<string, List<TValue>>>
    {
        public override Dictionary<string, List<TValue>> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var result = new Dictionary<string, List<TValue>>();

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected start of object");
            }

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException("Expected property name");
                }

                string key = reader.GetString()!;
                reader.Read();

                // Handle both single object and array cases
                if (reader.TokenType == JsonTokenType.StartArray)
                {
                    // If it's already an array, deserialize directly to List<TValue>
                    var list = JsonSerializer.Deserialize<List<TValue>>(ref reader, options);
                    result[key] = list ?? new List<TValue>();
                }
                else
                {
                    // If it's a single object, wrap it in a list
                    var value = JsonSerializer.Deserialize<TValue>(ref reader, options);
                    result[key] = value != null ? new List<TValue> { value } : new List<TValue>();
                }
            }

            return result;
        }

        public override void Write(Utf8JsonWriter writer, Dictionary<string, List<TValue>> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            foreach (var kvp in value)
            {
                writer.WritePropertyName(kvp.Key);

                // If the list has exactly one item, write it as a single object
                if (kvp.Value.Count == 1)
                {
                    JsonSerializer.Serialize(writer, kvp.Value[0], options);
                }
                else
                {
                    // Otherwise write as array
                    JsonSerializer.Serialize(writer, kvp.Value, options);
                }
            }

            writer.WriteEndObject();
        }
    }
}
