using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using Journeys.Core.Extensions;

namespace Journeys.Core.JsonConverters
{
    public class CustomDateTimeConverter : JsonConverter<DateTimeOffset>
    {
        private readonly string[] formats =
        {
            // Essential ISO 8601 formats
            "yyyy-MM-ddTHH:mm:ss",           // 2024-01-15T14:30:45
            "yyyy-MM-ddTHH:mm:ss.fff",       // 2024-01-15T14:30:45.123
            "yyyy-MM-ddTHH:mm:sszzz",        // 2024-01-15T14:30:45+00:00
            "yyyy-MM-ddTHH:mm:ss.fffzzz",    // 2024-01-15T14:30:45.123+00:00
            "yyyy-MM-ddTHH:mm:ssZ",          // 2024-01-15T14:30:45Z
            "yyyy-MM-ddTHH:mm:ss.fffZ",      // 2024-01-15T14:30:45.123Z
    
            // Common US formats
            "M/d/yyyy h:mm:ss tt",           // 1/15/2024 2:30:45 PM
            "MM/dd/yyyy HH:mm:ss",           // 01/15/2024 14:30:45
    
            // Date-only
            "yyyy-MM-dd",                    // 2024-01-15
            "MM/dd/yyyy",                    // 01/15/2024
    
            // RFC 1123 (HTTP)
            "ddd, dd MMM yyyy HH:mm:ss 'GMT'", // Mon, 15 Jan 2024 14:30:45 GMT
        };

        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Handle JSON null token
            if (reader.TokenType == JsonTokenType.Null)
            {
                return default(DateTimeOffset);
            }

            var dateTimeString = reader.GetString();
            
            // Handle null or empty strings
            if (string.IsNullOrEmpty(dateTimeString))
            {
                return default(DateTimeOffset);
            }

            foreach (var format in formats)
            {
                if (DateTimeOffset.TryParseExact(dateTimeString, format, null, System.Globalization.DateTimeStyles.None, out DateTimeOffset date))
                {
                    return date;
                }
                // Also try parsing as DateTime if downstream API sends DateTime
                if (DateTime.TryParseExact(dateTimeString, format, null, System.Globalization.DateTimeStyles.None, out DateTime dt))
                {
                    if (dt.IsMinDate())
                    {
                        return default;
                    }
                    else
                    {
                        // Treat DateTime as UTC (offset +00:00) instead of local timezone
                        return new DateTimeOffset(dt, TimeSpan.Zero);
                    }
                }
            }

            // Fallback: Try flexible parsing for ISO 8601 formats with variable fractional seconds
            // This handles formats like "2026-01-07T20:30:04.5111047+00:00" (7 fractional digits)
            // and other valid ISO 8601 variations that weren't matched by exact formats
            if (DateTimeOffset.TryParse(dateTimeString, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTimeOffset flexibleDate))
            {
                return flexibleDate;
            }

            throw new JsonException($"Unable to parse '{dateTimeString}' as a DateTimeOffset.");
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        {
            // Serialize as UTC without offset
            writer.WriteStringValue(value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss"));
        }
    }

    public class CustomDateOnlyConverter : JsonConverter<DateOnly>
    {
        private readonly string[] formats = { "yyyy-MM-dd", "M/d/yyyy", "yyyy-MM-ddTHH:mm:ss" };

        public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var dateString = reader.GetString();
            foreach (var format in formats)
            {
                if (DateOnly.TryParseExact(dateString, format, out DateOnly date))
                {
                    return date;
                }
            }
            throw new JsonException($"Unable to parse '{dateString}' as a DateOnly.");
        }

        public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString("yyyy-MM-dd"));
        }
    }


}
