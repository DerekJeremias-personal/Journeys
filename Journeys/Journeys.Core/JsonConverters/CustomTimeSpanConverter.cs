using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Journeys.Core.JsonConverters
{
    public class CustomTimeSpanConverter : JsonConverter<TimeSpan?>
    {
        public override TimeSpan? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType == JsonTokenType.String)
            {
                var timeString = reader.GetString();
                if (string.IsNullOrEmpty(timeString))
                    return null;

                // First try to parse as TimeSpan directly
                if (TimeSpan.TryParse(timeString, out TimeSpan timeValue))
                {
                    return timeValue;
                }

                // If that fails, try to parse as DateTime and extract the time portion
                if (DateTime.TryParse(timeString, out DateTime dateTimeValue))
                {
                    return dateTimeValue.TimeOfDay;
                }

                // If that fails, try to extract time from ISO format strings
                if (timeString.Contains('T') && timeString.Contains(':'))
                {
                    // Try to extract time portion from ISO format like "2025-08-13T09:00:00.000"
                    var timePart = timeString.Split('T')[1];
                    if (timePart.Contains('.'))
                    {
                        timePart = timePart.Split('.')[0]; // Remove milliseconds
                    }
                    if (TimeSpan.TryParse(timePart, out TimeSpan extractedTime))
                    {
                        return extractedTime;
                    }
                }
            }

            throw new JsonException($"Unable to parse '{reader.GetString()}' as a TimeSpan.");
        }

        public override void Write(Utf8JsonWriter writer, TimeSpan? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                writer.WriteStringValue(value.Value.ToString(@"hh\:mm\:ss"));
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}
