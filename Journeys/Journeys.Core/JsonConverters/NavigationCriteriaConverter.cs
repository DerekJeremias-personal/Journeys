using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using Journeys.Core.RulesEngine.Journey;

namespace Journeys.Core.JsonConverters
{
    public class NavigationCriteriaConverter : JsonConverter<INavigationCriteria>
    {
        public NavigationCriteriaConverter() : base() { }

        public override INavigationCriteria Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {

            using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
            {
                JsonElement root = doc.RootElement;

                // Determine the type discriminator and deserialize accordingly
                if (root.TryGetProperty("typeDiscriminator", out JsonElement typeDiscriminator))
                {
                    switch (typeDiscriminator.GetString())
                    {
                        case "simple":
                            return JsonSerializer.Deserialize<SimpleNavigationCriteria>(root.GetRawText(), options);
                        default:
                            throw new NotSupportedException($"Type discriminator '{typeDiscriminator.GetString()}' is not supported.");
                    }
                }
                throw new JsonException("Missing type discriminator.");
            }
        }

        public override void Write(Utf8JsonWriter writer, INavigationCriteria value, JsonSerializerOptions options)
        {
            // Write the type discriminator and the object
            writer.WriteStartObject();
            writer.WriteString("typeDiscriminator", value.GetType().Name);
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
            writer.WriteEndObject();
        }

        //public override void Write(Utf8JsonWriter writer, INavigationCriteria value, JsonSerializerOptions options)
        //{
        //    JsonSerializer.Serialize(writer, (object)value, options);
        //}
    }

}
