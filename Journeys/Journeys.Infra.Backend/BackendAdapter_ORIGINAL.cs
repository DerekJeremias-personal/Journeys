using Backend.Dto.Interfaces;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.JsonConverters;
using Journeys.Core.Models;
using Journeys.Core.Services;
using Journeys.DTO.Exceptions;
using Journeys.DTO.Requests;
using Journeys.Infra.Backend;
using Journeys.Infra.Backend.Models;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Journeys.Infra.Backend
{
    public class BackendAdapter_ORIGINAL //: IDynamicDataAdapter
    {
        private readonly IHttpClientFactory _clientFactory;
        private JsonSerializerOptions _serializerOptions;
        private PolymorphicJsonConverter<TenantedModelBase> _polymorphicJsonConverter;
        private Dictionary<string, Type> _typeMapTable = new Dictionary<string, Type>();

        private readonly KeyValueStorageConfig _keyValueStorageConfig;
        private const string UPSERT_URL = "/api/{0}/entity/{1}/{2}"; // " / api/{0}/dynamic/upsert";
        private const string MOVE_URL = "/api/{0}/entity/{1}/{2}/move";

        private const string GET_URL = "/api/{0}/entity/{1}/{2}/{3}?pk={4}}"; // " / api/{0}/dynamic/getobject/{1}?modelId={2}&modelType={3}&pk={4}";

        private const string GET_BY_PK_URL = "/api/{0}/entity/{1}/{2}/search";
        private const string GET_MANY_URL = "/api/{0}/entity/{1}/{2}/batch";
        private const string GET_ALL_URL = "/api/{0}/entity/{1}/{2}/all";
        private const string GET_ALL_BY_QUERY = "/api/{0}/entity/{1}/{2}/query";

        private const string GET_BY_NAME_URL = "/api/{0}/dynamic/{1}/{2}";
        private const string GET_BY_NAME_PK_URL = "/api/{0}/dynamic/{1}/GetByPK";
        private const string GET_ALL_BY_NAME_URL = "/api/{0}/dynamic/{1}/GetAll";
        private const string GET_ALL_BY_NAME_QUERY = "/api/{0}/dynamic/{1}/query";

        private const string DELETE_BY_ID_AND_PK_URL = "/api/{0}/entity/{1}/{2}/{3}";
        private const string DELETE_URL = "/api/{0}/entity/{1}/{2}";

        private const string MODEL_TYPE = "loyalty";

        private string _typeMapPropertyName = "EventType";

        private readonly ILogger<BackendAdapter_ORIGINAL> _logger;

        public BackendAdapter_ORIGINAL(IHttpClientFactory clientFactory, IOptions<KeyValueStorageConfig> keyValueStorageConfig, ILogger<BackendAdapter_ORIGINAL> logger)
        {
            _keyValueStorageConfig = keyValueStorageConfig.Value;
            _clientFactory = clientFactory;
            _logger = logger;

            _polymorphicJsonConverter = new PolymorphicJsonConverter<TenantedModelBase>(_typeMapTable, _typeMapPropertyName);

            _serializerOptions = new JsonSerializerOptions
            {
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                PropertyNameCaseInsensitive = true,
                WriteIndented = true,
                Converters =
                {
                    new CustomDateTimeConverter(),
                    new CustomDateOnlyConverter(),
                    new CustomTimeSpanConverter(),
                    new JsonStringEnumConverter()
                    , _polymorphicJsonConverter
                }
            };
        }

        public async Task<T?> GetEntityAsync<T>(string tenantid, string id, string modelId, string pk = null, string pk2 = null, JsonSerializerOptions serializerOptions = null)
        {
            using (var client = _clientFactory.CreateClient())
            {
                client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);
                var response = await client.GetAsync(String.Format(Path.Join(_keyValueStorageConfig.BaseUrl, GET_URL), tenantid, MODEL_TYPE, modelId, id, pk));
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrEmpty(json))
                    {
                        return JsonSerializer.Deserialize<T>(json, serializerOptions ?? _serializerOptions);
                    }
                }
            }
            return default(T?);
        }

        public async Task<PagedResultSet<T>> QueryEntitiesAsync<T>(string tenantId, string? modelId, string query, Dictionary<string, object> parameters, string sortBy, SortOrder sortOrder, int pageSize, CancellationToken token = default(CancellationToken), string continuationToken = null, JsonSerializerOptions serializerOptions = null, bool includeChildModels = false)
        {
            string url = String.Format(Path.Join(_keyValueStorageConfig.BaseUrl, GET_ALL_BY_QUERY), tenantId);
            //private async Task<PagedResultSet<T>> PostPagedRequest<T>(string tenantId, string? modelName, string url, object req, Type? entityType = default(Type), JsonSerializerOptions serializerOptions = null)
            var request = new QueryObjectsRequest
            {
                Query = query,
                Parameters = parameters,
                SortBy = sortBy,
                SortOrder = sortOrder,
                PageSize = pageSize,
                ContinuationToken = continuationToken,
                ModelId = modelId,
                ModelType = MODEL_TYPE,
                IncludeChildModels = includeChildModels

            };
            return await PostPagedRequest<T>(tenantId, null, url, request, null, serializerOptions ?? _serializerOptions);
        }

        public async Task<PagedResultSet<T>> GetEntitiesByPKAsync<T>(string tenantId, string partitionKey, string modelId, int pageSize, string pk2 = null, string? continuationToken = null, JsonSerializerOptions serializerOptions = null)
        {

            string url = String.Format(Path.Join(_keyValueStorageConfig.BaseUrl, GET_BY_PK_URL), tenantId, MODEL_TYPE, modelId);

            var request = new GetByPKRequest
            {
                ModelId = modelId,
                ModelType = MODEL_TYPE,
                PK = partitionKey,
                PK2 = pk2,
                PageSize = pageSize,
                ContinuationToken = continuationToken
            };

            try
            {
                var response = await PostPagedRequest<T>(tenantId, null, url, request, null, serializerOptions ?? _serializerOptions);
                if (response == null)
                {
                    _logger.LogWarning($"No entities found for tenant {tenantId} with partition key {partitionKey} and model ID {modelId}.");
                    throw new APIErrorsException(new Dictionary<string, string> { { "NOT_FOUND", $"No entities found for tenant {tenantId} with partition key {partitionKey} and model ID {modelId}." } });
                }
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }

        }

        public async Task<List<T>> GetManyEntitiesAsync<T>(string tenantId, List<string> ids, string modelId, Type? entityType = default(Type), JsonSerializerOptions serializerOptions = null)
        {
            if ((ids?.Count ?? 0) == 0) return null;

            entityType = entityType ?? typeof(T);
            string url = String.Format(Path.Join(_keyValueStorageConfig.BaseUrl, GET_MANY_URL), tenantId, MODEL_TYPE, modelId);
            var req = new KeyValueStorageGetManyRequest
            {
                ModelId = modelId,
                ModelType = MODEL_TYPE,
                ObjectIds = ids
            };

            return await PostRequest<T>(tenantId, null, url, req, entityType, serializerOptions ?? _serializerOptions);

        }

        public async Task<List<T>> GetManyEntitiesAsync<T>(string tenantId, List<(string, Dictionary<string, string>)> ids, string modelId, Type? entityType = default(Type), JsonSerializerOptions serializerOptions = null)
        {
            if ((ids?.Count ?? 0) == 0) return null;

            entityType = null; // entityType ?? typeof(T);
            string url = String.Format(Path.Join(_keyValueStorageConfig.BaseUrl, GET_MANY_URL), tenantId, MODEL_TYPE, modelId);
            var req = new KeyValueStorageGetManyRequest
            {
                ModelId = modelId,
                ModelType = MODEL_TYPE,
                ObjectIdsWithPKs = ids.Select(i => new ObjectIdWithPK(i.Item1, i.Item2)).ToList()
            };

            return await PostRequest<T>(tenantId, null, url, req, entityType, serializerOptions ?? _serializerOptions);

        }

        public async Task<PagedResultSet<T>> GetAllEntitiesAsync<T>(string tenantId, string modelId, int pageSize, string? continuationToken = null, JsonSerializerOptions serializerOptions = null)
        {

            string url = String.Format(Path.Join(_keyValueStorageConfig.BaseUrl, GET_ALL_URL), tenantId, MODEL_TYPE, modelId);

            var request = new GetByPKRequest
            {
                ModelId = modelId,
                ModelType = MODEL_TYPE,
                PageSize = pageSize,
                ContinuationToken = continuationToken
            };

            return await PostPagedRequest<T>(tenantId, null, url, request, null, serializerOptions ?? _serializerOptions);
        }

        public async Task<T> SetEntityAsync<T>(string tenantId, T entity, string modelId, Type? entityType = default(Type), JsonSerializerOptions serializerOptions = null)
        {
            var req = new KeyValueStorageUpsertRequest<T>(MODEL_TYPE, modelId, entity);

            using (var client = _clientFactory.CreateClient())
            {
                client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);

                var requestJson = JsonSerializer.Serialize(req, _serializerOptions);

                var response = await client.PostAsJsonAsync(string.Format(Path.Join(_keyValueStorageConfig.BaseUrl, UPSERT_URL), tenantId, MODEL_TYPE, modelId), req, serializerOptions ?? _serializerOptions);
                if (response.IsSuccessStatusCode)
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
                            //Unknow type so hope for the best :)
                            var result = JsonSerializer.Deserialize<T>(json, serializerOptions ?? _serializerOptions);
                            if (result == null)
                            {
                                throw new Exception($"Backend Upsert succeeded, however response could not be deserialized to Type {typeof(T).FullName}");
                            }
                            return (T)result;
                        }
                    }
                }

                var resultContent = await response.Content?.ReadAsStringAsync();

                //If you're here, that's an error condition
                throw new Exception($"An error occurred while attempting to SetEntityAsync to Backend: {resultContent}");
            }
        }

        public async Task<T> MoveEntityAsync<T>(string tenantId, T entity, Dictionary<string, string> newPartition, string modelId, Type? entityType = default(Type), JsonSerializerOptions serializerOptions = null)
        {
            var req = new KeyValueStorageMoveRequest<T>(MODEL_TYPE, modelId, entity, newPartition);

            using (var client = _clientFactory.CreateClient())
            {
                client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);

                var requestJson = JsonSerializer.Serialize(req, _serializerOptions);

                var response = await client.PostAsJsonAsync(string.Format(Path.Join(_keyValueStorageConfig.BaseUrl, MOVE_URL), tenantId, MODEL_TYPE, modelId), req, serializerOptions ?? _serializerOptions);
                if (response.IsSuccessStatusCode)
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
                            //Unknow type so hope for the best :)
                            var result = JsonSerializer.Deserialize<T>(json, serializerOptions ?? _serializerOptions);
                            if (result == null)
                            {
                                throw new Exception($"Backend Upsert succeeded, however response could not be deserialized {json} to Type {typeof(T).FullName}");
                            }
                            return (T)result;
                        }
                    }
                }

                var resultContent = await response.Content?.ReadAsStringAsync();

                //If you're here, that's an error condition
                throw new Exception($"An error occurred while attempting to MoveEntityAsync to Backend: {resultContent}");
            }
        }

        public async Task<object> SetLogicalEntityAsync(string tenantId, JsonElement entity, string modelId, JsonSerializerOptions serializerOptions = null)
        {
            var req = new KeyValueStorageUpsertRequest<JsonElement>(MODEL_TYPE, modelId, entity);

            using (var client = _clientFactory.CreateClient())
            {
                client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);

                var requestJson = JsonSerializer.Serialize(req);

                var response = await client.PostAsJsonAsync(string.Format(Path.Join(_keyValueStorageConfig.BaseUrl, UPSERT_URL), tenantId, MODEL_TYPE, modelId), req);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        return json;
                    }
                }

                var resultContent = await response.Content?.ReadAsStringAsync();

                //If you're here, that's an error condition
                throw new Exception($"An error occurred while attempting to SetLogicalEntityAsync to Backend: {resultContent}");
            }
        }

        public async Task<bool> RemoveEntityAsync(string tenantId, string id, string modelId, Dictionary<string, string>? pks = null)
        {
            using var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);

            // Create the request object
            var req = new KeyValueStorageGetRequest(MODEL_TYPE, modelId, id, pks);
            var path = string.Format(Path.Join(_keyValueStorageConfig.BaseUrl, DELETE_BY_ID_AND_PK_URL), tenantId, MODEL_TYPE, modelId, id);

            var request = new HttpRequestMessage(HttpMethod.Delete, path)
            {
                Content = JsonContent.Create(pks, options: _serializerOptions)
            };

            // Send the request
            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine(response.ToString());
            }

            return response.IsSuccessStatusCode;
        }

        public async Task<bool> RemoveEntityAsync<T>(string tenantId, T entity, string modelId)
        {
            using (var client = _clientFactory.CreateClient())
            {
                client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);

                // Create the request object
                var request = new 
                {
                    //Entity = entity as IDynamicEntity ?? throw new ArgumentException("Entity must implement IDynamicEntity"),
                    Entity = entity as IDynamicEntity ?? throw new ArgumentException("Entity must implement IDynamicEntity"),
                    ModelId = modelId,
                    ModelType = MODEL_TYPE
                };

                var path = string.Format(Path.Join(_keyValueStorageConfig.BaseUrl, DELETE_URL), tenantId, MODEL_TYPE, modelId);

                // Create and send the DELETE request
                var httpRequest = new HttpRequestMessage(HttpMethod.Delete, path)
                {
                    Content = JsonContent.Create(request, options: _serializerOptions)
                };

                var response = await client.SendAsync(httpRequest);
                return response.IsSuccessStatusCode;
            }
        }

        public JsonSerializerOptions GetJsonSerializerOptions()
        {
            return this._serializerOptions;
        }

        public void SetPolymorphicTypeMap(Dictionary<string, Type> map, string typeMapPropertyName)
        {
            _typeMapTable = map;
            _typeMapPropertyName = typeMapPropertyName;
            _polymorphicJsonConverter = new PolymorphicJsonConverter<TenantedModelBase>(_typeMapTable, typeMapPropertyName);
            _serializerOptions = new JsonSerializerOptions
            {
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                PropertyNameCaseInsensitive = true,
                WriteIndented = true,
                Converters =
                {
                    new CustomDateTimeConverter(),
                    new CustomDateOnlyConverter(),
                    new CustomTimeSpanConverter(),
                    new JsonStringEnumConverter(),
                    _polymorphicJsonConverter
                }
            };
        }


        private async Task<List<T>> PostRequest<T>(string tenantId, string? modelName, string url, object req, Type? entityType = default(Type), JsonSerializerOptions serializerOptions = null)
        {

            using (var client = _clientFactory.CreateClient())
            {
                client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);

                // Serialize the object to JSON
                string json = JsonSerializer.Serialize(req);

                // Create StringContent from the JSON
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Send the POST request
                HttpResponseMessage response = await client.PostAsync(String.Format(Path.Join(_keyValueStorageConfig.BaseUrl, GET_MANY_URL), tenantId), content);
                if (response.IsSuccessStatusCode)
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
                            //Unknow type so hope for the best :)
                            var result = JsonSerializer.Deserialize<List<T>>(jsonResponse, serializerOptions ?? _serializerOptions);
                            if (result == null)
                            {
                                throw new Exception($"Backend Upsert succeeded, however response could not be deserialized to Type {typeof(T).FullName}");
                            }
                            return (List<T>)result;
                        }
                    }
                    return null;
                }

                var resultContent = await response?.Content?.ReadAsStringAsync();

                //If you're here, that's an error condition
                throw new Exception($"An error occurred while attempting to PostRequest to Backend: {resultContent}");
            }
        }

        private async Task<PagedResultSet<T>> PostPagedRequest<T>(string tenantId, string? modelName, string url, object req, Type? entityType = default(Type), JsonSerializerOptions serializerOptions = null)
        {

            using (var client = _clientFactory.CreateClient())
            {
                client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);

                // Serialize the object to JSON
                string json = JsonSerializer.Serialize(req);

                // Create StringContent from the JSON
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Send the POST request
                HttpResponseMessage response = await client.PostAsync(url, content);
                if (response.IsSuccessStatusCode)
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
                }

                var resultContent = await response.Content?.ReadAsStringAsync();

                //If you're here, that's an error condition
                throw new Exception($"An error occurred while attempting to PostPagedRequest to Backend: {resultContent}");
            }
        }

        private dynamic HydrateResponse<T>(string json, Type entityType, JsonSerializerOptions serializerOptions = null)
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
                            _logger.LogError(ex);
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


        private async Task<PagedResultSet<T>> HydratePagedSet<T>(string json, JsonSerializerOptions serializerOptions = null)
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
            if (root.TryGetProperty("count", out JsonElement countNode))
            {
                count = countNode.GetInt32();
            }

            string? continuationToken = null;
            if (root.TryGetProperty("continuationToken", out JsonElement continuationTokenJson))
            {
                continuationToken = continuationTokenJson.GetString();
            }

            if (root.TryGetProperty("entities", out JsonElement entitiesElement))
            {
                string entitiesJson = entitiesElement.GetRawText();
                if (string.IsNullOrEmpty(entitiesJson)) return null;

                var entities = JsonSerializer.Deserialize<List<T>>(entitiesJson, optionsWithConverter);

                return new PagedResultSet<T>
                {
                    ContinuationToken = continuationToken,
                    Entities = entities,
                    Count = count
                };
            }
            return null;
        }

        // Add this method to create converter for specific types
        private JsonConverter CreateDictionaryConverter<T>()
        {
            return new SingleObjectToListDictionaryConverter<T>();
        }
    }
}

