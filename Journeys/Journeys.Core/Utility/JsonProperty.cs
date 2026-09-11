using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using Journeys.Core.JsonConverters;

namespace Journeys.Core.Utility
{
    public class JsonProperty<T>
    {
        private JsonElement? _serialized;
        private T? _deserialized;
        private readonly JsonSerializerOptions _serializerOptions;

        public JsonProperty(JsonSerializerOptions? serializerOptions = null)
        {
            _serializerOptions = serializerOptions ?? new JsonSerializerOptions
            {
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                PropertyNameCaseInsensitive = true,
                WriteIndented = true,
                Converters =
            {
                new CustomDateTimeConverter(),
                new CustomDateOnlyConverter(),
                new JsonStringEnumConverter()
            }
            };
        }

        public JsonElement? Serialized
        {
            get
            {
                if (_serialized == null && _deserialized != null)
                {
                    var jsonString = JsonSerializer.Serialize(_deserialized, _serializerOptions);
                    _serialized = JsonSerializer.Deserialize<JsonElement>(jsonString);
                }
                return _serialized;
            }
            set
            {
                _serialized = value;
                _deserialized = default;
            }
        }

        public T? Deserialized
        {
            get
            {
                if (_deserialized == null && _serialized != null)
                {
                    var normalized = EvaluationComparisonJsonNormalizer.Normalize(_serialized.Value);
                    var jsonString = normalized.GetRawText();
                    _deserialized = JsonSerializer.Deserialize<T>(jsonString, _serializerOptions);
                }
                return _deserialized;
            }
            set
            {
                _deserialized = value;
                _serialized = null;
            }
        }
    }
}
