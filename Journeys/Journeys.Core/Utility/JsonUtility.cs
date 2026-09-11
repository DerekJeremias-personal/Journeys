using Backend.Dto.Utilities;
using Journeys.Core.JsonConverters;
using JsonCons.JsonPath;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Journeys.Core.Utility
{
    public static class JsonUtility
    {
        private static JsonSerializerOptions GetOptions()
        {
            var options = new JsonSerializerOptions();
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

            return options;
        }

        public static JsonSerializerOptions GetDefaultOptions()
        {
            var options = new JsonSerializerOptions
            {
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                PropertyNameCaseInsensitive = true,
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                // Configure property naming to handle case sensitivity issues
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                Converters =
                {
                    new CustomDateTimeConverter(),
                    new CustomDateOnlyConverter(),
                    new CustomTimeSpanConverter(),
                    new JsonStringEnumConverter(),
                    new DynamicEntityJsonConverter()
                }
            };

            return options;
        }

        /// <summary>
        /// Persist <see cref="Journeys.Core.Models.WrappedEventPayload"/> to Backend.
        /// Nested engine DTOs (journey/outcome/provider state) must use lowercase symbols
        /// so List attributes are not bound as Object. Dictionary keys (journey ids) stay unchanged.
        /// </summary>
        public static JsonSerializerOptions GetWrapperPersistOptions()
        {
            var options = new JsonSerializerOptions
            {
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                PropertyNameCaseInsensitive = true,
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = LowerInvariantJsonNamingPolicy.Instance,
                Converters =
                {
                    new CustomDateTimeConverter(),
                    new CustomDateOnlyConverter(),
                    new CustomTimeSpanConverter(),
                    new JsonStringEnumConverter(),
                    new DynamicEntityJsonConverter()
                }
            };

            return options;
        }

        public static JsonElement ToJsonElement(object obj)
        {
            string json = JsonSerializer.Serialize(obj, GetOptions());
            using JsonDocument doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }

        public static string Serialize<T>(T obj, ILogger logger)
        {
            try
            {
                var json = JsonSerializer.Serialize(obj, GetOptions());
                return json;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in Journeys.Core JsonUtility Serialize");
                throw;
            }
        }

        public static T Deserialize<T>(string json, ILogger logger)
        {
            try
            {
                var obj = JsonSerializer.Deserialize<T>(json, GetOptions());
                return obj;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in Journeys.Core JsonUtility Deserialize");
                throw;
            }
        }
    }

    //public static class JsonUtility
    //{
    //    private static JsonSerializerOptions GetSerializerOptions(bool useSnakeCase)
    //    {
    //        var options = new JsonSerializerOptions();
    //        if (useSnakeCase)
    //        {
    //            options.PropertyNamingPolicy = new SnakeCaseNamingPolicy();
    //        }
    //        else
    //        {
    //            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    //        }

    //        return options;
    //    }


    //    public static string SerializeObject(object obj, bool useSnakeCase)
    //    {
    //        return JsonSerializer.Serialize(obj, GetSerializerOptions(useSnakeCase));
    //    }

    //    public static T DeserializeObject<T>(string value, bool useSnakeCase)
    //    {
    //        return JsonSerializer.Deserialize<T>(value, GetSerializerOptions(useSnakeCase));
    //    }

    //    public static object DeserializeObject(string value, Type type, bool useSnakeCase)
    //    {
    //        return JsonSerializer.Deserialize(value, type, GetSerializerOptions(useSnakeCase));
    //    }

    //    public static T DeserializeObject<T>(StreamReader reader, bool useSnakeCase)
    //    {
    //        using (reader)
    //        {
    //            return JsonSerializer.Deserialize<T>(reader.ReadToEnd(), GetSerializerOptions(useSnakeCase));
    //        }
    //    }

    //    public static bool CanPathSelectTokens(string json, string jsonPath)
    //    {
    //        try
    //        {
    //            using var doc = JsonDocument.Parse(json);
    //            var tokens = JsonSelector.Select(doc.RootElement, jsonPath);
    //            return tokens.Any();
    //        }
    //        catch
    //        {
    //            // Catch any exceptions that may occur during parsing or selecting tokens
    //            return false;
    //        }
    //    }

    //    public static bool IsValidJsonPathSyntax(string jsonPath)
    //    {
    //        if (string.IsNullOrWhiteSpace(jsonPath))
    //        {
    //            return false;
    //        }

    //        try
    //        {
    //            using var doc = JsonDocument.Parse("{}");
    //            JsonSelector.Select(doc.RootElement, jsonPath);
    //            return true;
    //        }
    //        catch
    //        {
    //            // Catch any exceptions that may occur during JSONPath parsing
    //            return false;
    //        }
    //    }

    //    private static Regex _bracketsCapture = new Regex(@"(?<=\[).*?(?=\])");

    //    public static bool HasNegativeArrayIndexLiteral(string jsonPath)
    //    {
    //        if (string.IsNullOrWhiteSpace(jsonPath))
    //        {
    //            return false;
    //        }
    //        foreach (Match match in _bracketsCapture.Matches(jsonPath))
    //        {
    //            if (int.TryParse(match.Value, out int number) && number < 0)
    //            {
    //                return true;
    //            }
    //        }
    //        return false;
    //    }
    //}

    public sealed class LowerInvariantJsonNamingPolicy : JsonNamingPolicy
    {
        public static LowerInvariantJsonNamingPolicy Instance { get; } = new();

        public override string ConvertName(string name) =>
            string.IsNullOrEmpty(name) ? name : name.ToLowerInvariant();
    }

    //public class SnakeCaseNamingPolicy : JsonNamingPolicy
    //{
    //    public override string ConvertName(string name)
    //    {
    //        // Start by adding an underscore before each uppercase letter, then lowercasing all letters
    //        var snakeCaseName = Regex.Replace(name, @"([A-Z])", "_$1").ToLower();

    //        // Remove any leading underscores (in case the original name started with an uppercase letter)
    //        if (snakeCaseName.StartsWith('_'))
    //        {
    //            snakeCaseName = snakeCaseName.Substring(1);
    //        }

    //        return snakeCaseName;
    //    }
    //}
}
