using System.Reflection;
using System.Text.Json;
using Journeys.Core.Models;
using Journeys.DTO.Models;
using Journeys.DTO.Models.RulesEngine;
using Journeys.DTO.Responses;
using Microsoft.Extensions.Logging;

namespace Journeys.Core.Services.Ingest;

public class ResponseHandlerService
{
    private readonly ILogger<ResponseHandlerService> _logger;

    public ResponseHandlerService(ILogger<ResponseHandlerService> logger)
    {
        _logger = logger;
    }

    private string CleanJsonString(string jsonString)
    {
        if (string.IsNullOrEmpty(jsonString))
            return jsonString;

        // Remove Unicode escape sequences like \u0022 -> "
        var cleaned = System.Text.RegularExpressions.Regex.Replace(jsonString, @"\\u0022", "\"");
        
        // Remove other common Unicode escapes
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\\u0027", "'");
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\\u005C", "\\");
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\\u002F", "/");
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\\u0008", "\b");
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\\u000C", "\f");
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\\u000A", "\n");
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\\u000D", "\r");
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\\u0009", "\t");

        return cleaned;
    }

    public BatchResultFileLineDto HandleResponse(object? response, DropboxConfig? dropboxConfig, JsonElement group, int? lineNumber = null)
    {
        // If no response, return generic success
        if (response == null)
        {
            return new BatchResultFileLineDto
            {
                LineKey = CleanJsonString(group.GetRawText()),
                Success = true,
                Errors = null,
                LineNumber = lineNumber
            };
        }

        // If we have configuration for response handling, use it
        if (!string.IsNullOrEmpty(dropboxConfig?.ResponseTypeName))
        {
            return HandleConfiguredResponse(response, dropboxConfig, group, lineNumber);
        }

        // Fallback to hard-coded response type handling
        return HandleHardcodedResponse(response, group, lineNumber);
    }

    private BatchResultFileLineDto HandleConfiguredResponse(object response, DropboxConfig dropboxConfig, JsonElement group, int? lineNumber = null)
    {
        try
        {
            var responseType = response.GetType();
            var expectedTypeName = dropboxConfig.ResponseTypeName;

            // Check if response type matches expected type (allowing for partial matches)
            if (responseType.Name.Equals(expectedTypeName, StringComparison.OrdinalIgnoreCase) ||
                responseType.FullName?.Contains(expectedTypeName, StringComparison.OrdinalIgnoreCase) == true)
            {
                // Generate line key using configured format
                var lineKey = GenerateLineKeyFromFormat(response, dropboxConfig.ResponseLineKeyFormat, group);
                
                return new BatchResultFileLineDto
                {
                    LineKey = lineKey,
                    Success = true,
                    Errors = null,
                    LineNumber = lineNumber
                };
            }
            else
            {
                _logger.LogWarning("Response type {ActualType} doesn't match expected type {ExpectedType}", 
                    responseType.Name, expectedTypeName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling configured response for type {ResponseType}", 
                dropboxConfig.ResponseTypeName);
        }

        // Fallback to generic response
        return new BatchResultFileLineDto
        {
            LineKey = CleanJsonString(group.GetRawText()),
            Success = true,
            Errors = null,
            LineNumber = lineNumber
        };
    }

    private BatchResultFileLineDto HandleHardcodedResponse(object response, JsonElement group, int? lineNumber = null)
    {
        // Handle EventPayloadResponseDto (special case with errors)
        if (response is EventPayloadResponseDto eventResponse)
        {
            return new BatchResultFileLineDto
            {
                LineKey = eventResponse.EventNaturalKey,
                Success = eventResponse.Errors?.Count == 0 || eventResponse.Errors == null,
                Errors = eventResponse.Errors?
                    .Select((err, i) => new KeyValuePair<string, string>($"Error_{i}", JsonSerializer.Serialize(err)))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                LineNumber = lineNumber
            };
        }

        // Handle TagDto
        if (response is TagDto tagDtoResponse)
        {
            return new BatchResultFileLineDto
            {
                LineKey = $"{tagDtoResponse.EntityId}_{tagDtoResponse.Name}",
                Success = true,
                Errors = null,
                LineNumber = lineNumber
            };
        }

        // Handle Tag
        if (response is Tag tagModelResponse)
        {
            return new BatchResultFileLineDto
            {
                LineKey = $"{tagModelResponse.EntityId}_{tagModelResponse.Name}",
                Success = true,
                Errors = null,
                LineNumber = lineNumber
            };
        }

        // Generic success response for other types
        return new BatchResultFileLineDto
        {
            LineKey = CleanJsonString(group.GetRawText()),
            Success = true,
            Errors = null,
            LineNumber = lineNumber
        };
    }

    private string GenerateLineKeyFromFormat(object response, string? format, JsonElement group)
    {
        if (string.IsNullOrEmpty(format))
        {
            return CleanJsonString(group.GetRawText());
        }

        try
        {
            var result = format;
            var responseType = response.GetType();

            // Find all placeholders in the format string (e.g., {PropertyName})
            var placeholders = System.Text.RegularExpressions.Regex.Matches(format, @"\{([^}]+)\}");
            
            foreach (System.Text.RegularExpressions.Match match in placeholders)
            {
                var propertyName = match.Groups[1].Value;
                var property = responseType.GetProperty(propertyName, 
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                if (property != null)
                {
                    var value = property.GetValue(response);
                    var stringValue = value?.ToString() ?? "";
                    result = result.Replace(match.Value, stringValue);
                }
                else
                {
                    _logger.LogWarning("Property {PropertyName} not found on response type {ResponseType}", 
                        propertyName, responseType.Name);
                    result = result.Replace(match.Value, "Unknown");
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating line key from format {Format}", format);
            return CleanJsonString(group.GetRawText());
        }
    }
}
