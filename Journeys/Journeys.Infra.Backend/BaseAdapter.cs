using Journeys.Core.JsonConverters;
using Journeys.Core.Models;
using Journeys.Infra.Backend.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.Infra.Backend
{
    public class BaseAdapter
    {
        protected readonly IHttpClientFactory _clientFactory;
        protected JsonSerializerOptions _serializerOptions;
        protected readonly KeyValueStorageConfig _keyValueStorageConfig;
        private readonly ILogger<BaseAdapter> _logger;

        private readonly string MODEL_TYPE;

        public BaseAdapter(IHttpClientFactory clientFactory, IOptions<KeyValueStorageConfig> keyValueStorageConfig, ILogger<BaseAdapter> logger, string modelType)
        {
            _keyValueStorageConfig = keyValueStorageConfig.Value;
            _clientFactory = clientFactory;
            _logger = logger;
            MODEL_TYPE = modelType;
        }

        #region Private Helper Methods

        /// <summary>
        /// Handles GET operation responses with proper error handling
        /// </summary>
        protected async Task<T?> HandleGetResponse<T>(HttpResponseMessage response, JsonSerializerOptions serializerOptions, string tenantId, string entityId, string modelId)
        {
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var json = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrEmpty(json))
                {
                    return JsonSerializer.Deserialize<T>(json, serializerOptions ?? _serializerOptions);
                }
                return default(T);
            }
            else if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.NoContent)
            {
                _logger.LogDebug("Entity {EntityId} not found in tenant {TenantId}, modelId {ModelId}", entityId, tenantId, modelId);
                return default(T);
            }
            else
            {
                await HandleErrorResponse(response, tenantId, modelId);
                return default(T); // This line should never be reached due to exception throwing above
            }
        }

        /// <summary>
        /// Handles SET operation responses (Create/Update) with proper error handling
        /// </summary>
        protected async Task<T> HandleSetEntityResponse<T>(HttpResponseMessage response, Type? entityType, JsonSerializerOptions serializerOptions, string tenantId, string modelId)
        {
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var json = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(json))
                {
                    if (entityType != default(Type))
                    {
                        return HydrateResponse<T>(json, entityType);
                    }
                    else
                    {
                        var result = JsonSerializer.Deserialize<T>(json, serializerOptions ?? _serializerOptions);
                        if (result == null)
                        {
                            throw new BackendSystemException("deserialization_error", $"Backend operation succeeded, but response could not be deserialized to Type {typeof(T).FullName}", json);
                        }
                        return result;
                    }
                }
                else
                {
                    throw new BackendSystemException("empty_response", "Backend returned success but with empty response body");
                }
            }
            else
            {
                await HandleErrorResponse(response, tenantId, modelId);
                return default(T); // This line should never be reached due to exception throwing above
            }
        }

        /// <summary>
        /// Centralized error response handling for all HTTP error status codes
        /// </summary>
        protected async Task HandleErrorResponse(HttpResponseMessage response, string tenantId, string modelId)
        {
            var responseContent = await response.Content.ReadAsStringAsync();

            switch (response.StatusCode)
            {
                case HttpStatusCode.BadRequest:
                    await HandleValidationError(responseContent, tenantId, modelId);
                    break;

                case HttpStatusCode.NotFound:
                    _logger.LogWarning("Resource not found for tenant {TenantId}, modelId {ModelId}. Response: {Response}", tenantId, modelId, responseContent);
                    throw new BackendEntityNotFoundException("unknown", modelId, tenantId);

                case HttpStatusCode.Unauthorized:
                    _logger.LogError("Unauthorized access for tenant {TenantId}, modelId {ModelId}. Response: {Response}", tenantId, modelId, responseContent);
                    throw new BackendSystemException("unauthorized", "Unauthorized access to backend service", responseContent);

                case HttpStatusCode.Forbidden:
                    _logger.LogError("Forbidden access for tenant {TenantId}, modelId {ModelId}. Response: {Response}", tenantId, modelId, responseContent);
                    throw new BackendSystemException("forbidden", "Access forbidden to backend resource", responseContent);

                case HttpStatusCode.InternalServerError:
                    await HandleSystemError(responseContent, tenantId, modelId);
                    break;

                case HttpStatusCode.ServiceUnavailable:
                    _logger.LogError("Backend service unavailable for tenant {TenantId}, modelId {ModelId}. Response: {Response}", tenantId, modelId, responseContent);
                    throw new BackendSystemException("service_unavailable", "Backend service is temporarily unavailable", responseContent);

                case HttpStatusCode.RequestTimeout:
                    _logger.LogError("Request timeout for tenant {TenantId}, modelId {ModelId}. Response: {Response}", tenantId, modelId, responseContent);
                    throw new BackendSystemException("timeout", "Request to backend service timed out", responseContent);

                default:
                    _logger.LogError("Unexpected HTTP status {StatusCode} for tenant {TenantId}, modelId {ModelId}. Response: {Response}",
                        response.StatusCode, tenantId, modelId, responseContent);
                    throw new BackendSystemException("unexpected_http_status",
                        $"Unexpected HTTP status code: {response.StatusCode}", responseContent);
            }
        }

        /// <summary>
        /// Handles HTTP 400 Bad Request responses (validation errors)
        /// </summary>
        protected async Task HandleValidationError(string responseContent, string tenantId, string modelId)
        {
            try
            {
                var validationError = JsonSerializer.Deserialize<FieldValidationErrorResponse>(responseContent, _serializerOptions);
                if (validationError?.ValidationErrors != null)
                {
                    var opcurerror = validationError
                                        .ValidationErrors
                                        .FirstOrDefault(x => x.Key.ToLower().Equals("error") && 
                                            (x.Value.Contains("PreconditionFailed") || x.Value.Contains("(412)")));
                    
                    if(!string.IsNullOrEmpty(opcurerror.Value))
                    {
                        //Throw optimistic concurrency error
                        throw new BackendEntityConcurrencyException("unknown", modelId, tenantId);
                    }

                    _logger.LogWarning("Validation errors for tenant {TenantId}, modelId {ModelId}: {ValidationErrors}",
                        tenantId, modelId, string.Join(", ", validationError.ValidationErrors.Select(kvp => $"{kvp.Key}: {kvp.Value}")));

                    
                    throw new BackendValidationException(validationError.ValidationErrors, validationError.Message, validationError.Code);
                }
                else
                {
                    if (validationError.Type.ToLower().Equals("bad_db_request"))
                    {
                        _logger.LogWarning("Bad request for tenant {TenantId}, modelId {ModelId} but could not parse validation errors: {Response}",
                            tenantId, modelId, responseContent);
                        throw new BackendSystemException(validationError.Type, validationError.Message);
                    }

                    _logger.LogWarning("Bad request for tenant {TenantId}, modelId {ModelId} but could not parse validation errors: {Response}",
                        tenantId, modelId, responseContent);
                    throw new BackendValidationException(new Dictionary<string, string> { { "general", "Validation error occurred but details could not be parsed" } },
                        "Validation error occurred", "validation_error");
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize validation error response for tenant {TenantId}, modelId {ModelId}: {Response}",
                    tenantId, modelId, responseContent);
                throw new BackendValidationException(new Dictionary<string, string> { { "general", "Validation error occurred but response format was invalid" } },
                    "Validation error occurred", "validation_error");
            }
        }

        /// <summary>
        /// Handles HTTP 500 Internal Server Error responses (system errors)
        /// </summary>
        protected async Task HandleSystemError(string responseContent, string tenantId, string modelId)
        {
            try
            {
                var systemError = JsonSerializer.Deserialize<ErrorResponse>(responseContent, _serializerOptions);
                if (systemError != null)
                {
                    _logger.LogError("Backend system error for tenant {TenantId}, modelId {ModelId}: {Code} - {Message}",
                        tenantId, modelId, systemError.Code, systemError.Message);

                    throw new BackendSystemException(systemError.Code ?? "internal_error",
                        systemError.Message ?? "An internal server error occurred", responseContent);
                }
                else
                {
                    _logger.LogError("Internal server error for tenant {TenantId}, modelId {ModelId}: {Response}",
                        tenantId, modelId, responseContent);
                    throw new BackendSystemException("internal_error", "An internal server error occurred", responseContent);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize system error response for tenant {TenantId}, modelId {ModelId}: {Response}",
                    tenantId, modelId, responseContent);
                throw new BackendSystemException("internal_error", "An internal server error occurred", responseContent);
            }
        }

        #endregion

        #region Existing Private Methods (unchanged)

        protected async Task<List<T>> PostRequest<T>(string tenantId, string modelId, string url, object req, Type? entityType = default(Type), JsonSerializerOptions serializerOptions = null)
        {
            using (var client = _clientFactory.CreateClient())
            {
                client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);

                try
                {
                    string json = JsonSerializer.Serialize(req);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.PostAsync(url, content);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        var jsonResponse = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrWhiteSpace(jsonResponse) && jsonResponse.Length > 2)
                        {
                            if (entityType != default(Type))
                            {
                                return HydrateResponse<T>(jsonResponse, entityType);
                            }
                            else
                            {
                                var result = JsonSerializer.Deserialize<List<T>>(jsonResponse, serializerOptions ?? _serializerOptions);
                                if (result == null)
                                {
                                    throw new BackendSystemException("deserialization_error", $"Backend operation succeeded, but response could not be deserialized to Type {typeof(T).FullName}", jsonResponse);
                                }
                                return result;
                            }
                        }
                        return new List<T>();
                    }
                    else
                    {
                        await HandleErrorResponse(response, tenantId, modelId ?? "unknown");
                        return new List<T>(); // This line should never be reached due to exception throwing above
                    }
                }
                catch (Exception ex) when (!(ex is BackendValidationException || ex is BackendSystemException || ex is BackendEntityNotFoundException))
                {
                    _logger.LogError(ex, "Unexpected error in PostRequest for tenant {TenantId}", tenantId);
                    throw new BackendSystemException("unexpected_error", "An unexpected error occurred while communicating with the backend", ex.Message);
                }
            }
        }

        protected async Task<PagedResultSet<T>> PostPagedRequest<T>(string tenantId, string? modelName, string url, object req, Type? entityType = default(Type), JsonSerializerOptions serializerOptions = null)
        {
            using (var client = _clientFactory.CreateClient())
            {
                client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);

                try
                {
                    string json = JsonSerializer.Serialize(req);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.PostAsync(url, content);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        var jsonResponse = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrWhiteSpace(jsonResponse))
                        {
                            if (entityType != default(Type))
                            {
                                return HydrateResponse<T>(jsonResponse, entityType);
                            }
                            else
                            {
                                return await HydratePagedSet<T>(jsonResponse, serializerOptions);
                            }
                        }
                        return new PagedResultSet<T> { Entities = new List<T>(), Count = 0 };
                    }
                    else
                    {
                        await HandleErrorResponse(response, tenantId, modelName ?? "unknown");
                        return new PagedResultSet<T> { Entities = new List<T>(), Count = 0 }; // This line should never be reached due to exception throwing above
                    }
                }
                catch (Exception ex) when (!(ex is BackendValidationException || ex is BackendSystemException || ex is BackendEntityNotFoundException))
                {
                    _logger.LogError(ex, "Unexpected error in PostPagedRequest for tenant {TenantId}", tenantId);
                    throw new BackendSystemException("unexpected_error", "An unexpected error occurred while communicating with the backend", ex.Message);
                }
            }
        }

        protected dynamic HydrateResponse<T>(string json, Type entityType, JsonSerializerOptions serializerOptions = null)
        {
            if (!string.IsNullOrEmpty(json) && entityType != default(Type))
            {
                var options = serializerOptions ?? _serializerOptions;

                // Create a new options instance with the additional converter
                var optionsWithConverter = new JsonSerializerOptions(options);

                // Check if entityType has any Dictionary<string, List<T>> properties
                var dictProperties = entityType.GetProperties()
                    .Where(p => p.PropertyType.IsGenericType &&
                                p.PropertyType.GetGenericTypeDefinition() == typeof(Dictionary<,>) &&
                                p.PropertyType.GetGenericArguments()[0] == typeof(string) &&
                                p.PropertyType.GetGenericArguments()[1].IsGenericType &&
                                p.PropertyType.GetGenericArguments()[1].GetGenericTypeDefinition() == typeof(List<>));

                foreach (var prop in dictProperties)
                {
                    var valueType = prop.PropertyType.GetGenericArguments()[1].GetGenericArguments()[0];
                    var converterType = typeof(SingleObjectToListDictionaryConverter<>).MakeGenericType(valueType);
                    var converter = (JsonConverter)Activator.CreateInstance(converterType)!;
                    optionsWithConverter.Converters.Add(converter);
                }

                using (var document = JsonDocument.Parse(json))
                {
                    // Check if the JSON is an array or a single object
                    if (document.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        try
                        {
                            // Deserialize as List<T>
                            var deserializedList = JsonSerializer.Deserialize(
                                document.RootElement.GetRawText(),
                                typeof(List<>).MakeGenericType(entityType),
                                optionsWithConverter);

                            if (deserializedList is List<T> typedListResult)
                            {
                                return typedListResult;
                            }
                            else
                            {
                                throw new InvalidCastException($"Deserialized object could not be cast to List<{typeof(T).Name}>.");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error deserializing array response");
                            throw;
                        }
                    }
                    else
                    {
                        // Deserialize as single T
                        var deserializedObj = JsonSerializer.Deserialize(
                            document.RootElement.GetRawText(),
                            entityType,
                            optionsWithConverter);

                        if (deserializedObj is T typedResult)
                        {
                            return typedResult;
                        }
                        else
                        {
                            throw new InvalidCastException($"Deserialized object could not be cast to {typeof(T).Name}.");
                        }
                    }
                }
            }
            return null;
        }

        protected async Task<PagedResultSet<T>> HydratePagedSet<T>(string json, JsonSerializerOptions serializerOptions = null)
        {
            var options = serializerOptions ?? _serializerOptions;
            // Create a new options instance with the additional converter
            var optionsWithConverter = new JsonSerializerOptions(options);

            // Check if T has a property of type Dictionary<string, List<something>>
            var dictProperties = typeof(T).GetProperties()
                .Where(p => p.PropertyType.IsGenericType &&
                            p.PropertyType.GetGenericTypeDefinition() == typeof(Dictionary<,>) &&
                            p.PropertyType.GetGenericArguments()[0] == typeof(string) &&
                            p.PropertyType.GetGenericArguments()[1].IsGenericType &&
                            p.PropertyType.GetGenericArguments()[1].GetGenericTypeDefinition() == typeof(List<>));

            foreach (var prop in dictProperties)
            {
                var valueType = prop.PropertyType.GetGenericArguments()[1].GetGenericArguments()[0];
                var converterType = typeof(SingleObjectToListDictionaryConverter<>).MakeGenericType(valueType);
                var converter = (JsonConverter)Activator.CreateInstance(converterType)!;
                optionsWithConverter.Converters.Add(converter);
            }

            // Parse the raw JSON into a JsonDocument
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            // Extract the continuationToken and count from the root
            int count = 0;
            if (root.TryGetProperty("Count", out JsonElement countNode))
            {
                count = countNode.GetInt32();
            }

            string? continuationToken = null;
            if (root.TryGetProperty("ContinuationToken", out JsonElement continuationTokenJson))
            {
                continuationToken = continuationTokenJson.GetString();
            }

            if (root.TryGetProperty("Items", out JsonElement entitiesElement))
            {
                string entitiesJson = entitiesElement.GetRawText();
                if (string.IsNullOrEmpty(entitiesJson)) return new PagedResultSet<T> { Entities = new List<T>(), Count = 0 };

                var entities = JsonSerializer.Deserialize<List<T>>(entitiesJson, optionsWithConverter);

                return new PagedResultSet<T>
                {
                    ContinuationToken = continuationToken,
                    Entities = entities ?? new List<T>(),
                    Count = count
                };
            }
            return new PagedResultSet<T> { Entities = new List<T>(), Count = 0 };
        }

        // Add this method to create converter for specific types
        protected JsonConverter CreateDictionaryConverter<T>()
        {
            return new SingleObjectToListDictionaryConverter<T>();
        }

        #endregion
    }
}
