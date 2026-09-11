using Backend.Dto.Interfaces;
using Backend.Dto.Responses;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.JsonConverters;
using Journeys.Core.Models;
using Journeys.DTO.Exceptions;
using Journeys.DTO.Requests;
using Journeys.Infra.Backend.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ErrorResponse = Journeys.Infra.Backend.Models.ErrorResponse;
using FieldValidationErrorResponse = Journeys.Infra.Backend.Models.FieldValidationErrorResponse;

namespace Journeys.Infra.Backend
{
    public class BackendAdapter : BaseAdapter, IDynamicDataAdapter
    {
        private PolymorphicJsonConverter<TenantedModelBase> _polymorphicJsonConverter;
        private Dictionary<string, Type> _typeMapTable = new Dictionary<string, Type>();

        private const string UPSERT_URL = "/api/{0}/entity/{1}/{2}"; // " / api/{0}/dynamic/upsert";
        private const string MOVE_URL = "/api/{0}/entity/{1}/{2}/move";

        private const string GET_URL = "/api/{0}/entity/{1}/{2}/{3}?pk={4}&pk2={5}"; // " / api/{0}/dynamic/getobject/{1}?modelId={2}&modelType={3}&pk={4}";

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

        private readonly ILogger<BackendAdapter> _logger;

        public BackendAdapter(IHttpClientFactory clientFactory, IOptions<KeyValueStorageConfig> keyValueStorageConfig, ILogger<BackendAdapter> logger)
            : base(clientFactory, keyValueStorageConfig, logger, MODEL_TYPE)
        {
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
                var route = String.Format(Path.Join(_keyValueStorageConfig.BaseUrl, GET_URL), tenantid, MODEL_TYPE, modelId, id, pk, pk2);
                var response = await client.GetAsync(route);

                return await HandleGetResponse<T>(response, serializerOptions, tenantid, id, modelId);
            }
        }

        public async Task<PagedResultSet<T>> QueryEntitiesAsync<T>(string tenantId, string? modelId, string query, Dictionary<string, object> parameters, string sortBy, SortOrder sortOrder, int pageSize, CancellationToken token = default(CancellationToken), string continuationToken = null, JsonSerializerOptions serializerOptions = null, bool includeChildModels = false)
        {
            string url = String.Format(Path.Join(_keyValueStorageConfig.BaseUrl, GET_ALL_BY_QUERY), tenantId, MODEL_TYPE, modelId);
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
                _logger.LogError(ex, "Error getting entities by PK for tenant {TenantId}, modelId {ModelId}, partitionKey {PartitionKey}", tenantId, modelId, partitionKey);
                throw;
            }
        }

        public async Task<List<T>> GetManyEntitiesAsync<T>(string tenantId, List<string> ids, string modelId, Type? entityType = default(Type), JsonSerializerOptions serializerOptions = null)
        {
            if ((ids?.Count ?? 0) == 0) return null;

            entityType = entityType ?? typeof(T);
            string url = String.Format(Path.Join(_keyValueStorageConfig.BaseUrl, GET_MANY_URL), tenantId, MODEL_TYPE, modelId);
            var req = new GetManyRequest
            {
                ModelId = modelId,
                ModelType = MODEL_TYPE,
                ObjectIds = ids
            };

            return await PostRequest<T>(tenantId, modelId, url, req, entityType, serializerOptions ?? _serializerOptions);
        }

        public async Task<List<T>> GetManyEntitiesAsync<T>(string tenantId, List<(string, Dictionary<string, string>)> ids, string modelId, Type? entityType = default(Type), JsonSerializerOptions serializerOptions = null)
        {
            if ((ids?.Count ?? 0) == 0) return null;

            entityType = null; // entityType ?? typeof(T);
            string url = String.Format(Path.Join(_keyValueStorageConfig.BaseUrl, GET_MANY_URL), tenantId, MODEL_TYPE, modelId);
            var req = new GetManyRequest
            {
                ModelId = modelId,
                ModelType = MODEL_TYPE,
                ObjectIdsWithPKs = ids.Select(i => new ObjectIdWithPK(i.Item1, i.Item2)).ToList()
            };

            return await PostRequest<T>(tenantId, modelId, url, req, entityType, serializerOptions ?? _serializerOptions);
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

        /// <summary>
        /// Sets (creates or updates) an entity with proper error handling
        /// </summary>
        public async Task<T> SetEntityAsync<T>(string tenantId, T entity, string modelId, Type? entityType = default(Type), JsonSerializerOptions serializerOptions = null)
        {
            using (var client = _clientFactory.CreateClient())
            {
                client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);
                client.Timeout = new TimeSpan(0, 10, 0); //ToDo: Change this back to 30 seconds
                try
                {
                    var response = await client.PostAsJsonAsync(
                        string.Format(Path.Join(_keyValueStorageConfig.BaseUrl, UPSERT_URL), tenantId, MODEL_TYPE, modelId),
                        entity,
                        serializerOptions ?? _serializerOptions);

                    return await HandleSetEntityResponse<T>(response, entityType, serializerOptions, tenantId, modelId);
                }
                catch (BackendEntityConcurrencyException ex)
                {
                    _logger.LogError(ex, "Concurrency error in SetEntityAsync for tenant {TenantId}, modelId {ModelId}", tenantId, modelId);
                    throw;
                }
                catch (Exception ex) when (!(ex is BackendValidationException || ex is BackendSystemException || ex is BackendEntityNotFoundException))
                {
                    _logger.LogError(ex, "Unexpected error in SetEntityAsync for tenant {TenantId}, modelId {ModelId}", tenantId, modelId);
                    throw new BackendSystemException("unexpected_error", "An unexpected error occurred while communicating with the backend", ex.Message);
                }
            }
        }

        /// <summary>
        /// Moves an entity with proper error handling
        /// </summary>
        public async Task<T> MoveEntityAsync<T>(string tenantId, T entity, Dictionary<string, string> newPartition, string modelId, Type? entityType = default(Type), JsonSerializerOptions serializerOptions = null)
        {
            var req = new KeyValueStorageMoveRequest<T>(MODEL_TYPE, modelId, entity, newPartition);

            using (var client = _clientFactory.CreateClient())
            {
                client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);

                try
                {
                    var json = JsonSerializer.Serialize(req);

                    var response = await client.PostAsJsonAsync(
                        string.Format(Path.Join(_keyValueStorageConfig.BaseUrl, MOVE_URL), tenantId, MODEL_TYPE, modelId),
                        req,
                        serializerOptions ?? _serializerOptions);

                    return await HandleSetEntityResponse<T>(response, entityType, serializerOptions, tenantId, modelId);
                }
                catch (Exception ex) when (!(ex is BackendValidationException || ex is BackendSystemException || ex is BackendEntityNotFoundException))
                {
                    _logger.LogError(ex, "Unexpected error in MoveEntityAsync for tenant {TenantId}, modelId {ModelId}", tenantId, modelId);
                    throw new BackendSystemException("unexpected_error", "An unexpected error occurred while communicating with the backend", ex.Message);
                }
            }
        }

        public async Task<object> SetLogicalEntityAsync(string tenantId, JsonElement entity, string modelId, JsonSerializerOptions serializerOptions = null)
        {
            var req = new UpsertRequest<JsonElement>(MODEL_TYPE, modelId, entity);

            using (var client = _clientFactory.CreateClient())
            {
                client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);

                try
                {
                    var response = await client.PostAsJsonAsync(
                        string.Format(Path.Join(_keyValueStorageConfig.BaseUrl, UPSERT_URL), tenantId, MODEL_TYPE, modelId),
                        req);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            return json;
                        }
                    }

                    // Handle errors using the same pattern as other methods
                    await HandleErrorResponse(response, tenantId, modelId);
                    return null; // This line should never be reached due to exception throwing above
                }
                catch (Exception ex) when (!(ex is BackendValidationException || ex is BackendSystemException || ex is BackendEntityNotFoundException))
                {
                    _logger.LogError(ex, "Unexpected error in SetLogicalEntityAsync for tenant {TenantId}, modelId {ModelId}", tenantId, modelId);
                    throw new BackendSystemException("unexpected_error", "An unexpected error occurred while communicating with the backend", ex.Message);
                }
            }
        }

        public async Task<bool> RemoveEntityAsync(string tenantId, string id, string modelId, Dictionary<string, string>? pks = null)
        {
            using var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);

            try
            {
                var path = string.Format(Path.Join(_keyValueStorageConfig.BaseUrl, DELETE_BY_ID_AND_PK_URL), tenantId, MODEL_TYPE, modelId, id);

                var request = new HttpRequestMessage(HttpMethod.Delete, path)
                {
                    Content = JsonContent.Create(pks, options: _serializerOptions)
                };

                var response = await client.SendAsync(request);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return true;
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Entity {EntityId} not found for deletion in tenant {TenantId}, modelId {ModelId}", id, tenantId, modelId);
                    return false;
                }
                else
                {
                    await HandleErrorResponse(response, tenantId, modelId);
                    return false; // This line should never be reached due to exception throwing above
                }
            }
            catch (Exception ex) when (!(ex is BackendValidationException || ex is BackendSystemException || ex is BackendEntityNotFoundException))
            {
                _logger.LogError(ex, "Unexpected error in RemoveEntityAsync for tenant {TenantId}, modelId {ModelId}, entityId {EntityId}", tenantId, modelId, id);
                throw new BackendSystemException("unexpected_error", "An unexpected error occurred while communicating with the backend", ex.Message);
            }
        }

        public async Task<bool> RemoveEntityAsync<T>(string tenantId, T entity, string modelId)
        {
            using (var client = _clientFactory.CreateClient())
            {
                client.DefaultRequestHeaders.Add("X-API-KEY", _keyValueStorageConfig.Key);

                try
                {
                    var request = new
                    {
                        Entity = entity as IDynamicEntity ?? throw new ArgumentException("Entity must implement IDynamicEntity"),
                        ModelId = modelId,
                        ModelType = MODEL_TYPE
                    };

                    var path = string.Format(Path.Join(_keyValueStorageConfig.BaseUrl, DELETE_URL), tenantId, MODEL_TYPE, modelId);

                    var httpRequest = new HttpRequestMessage(HttpMethod.Delete, path)
                    {
                        Content = JsonContent.Create(request, options: _serializerOptions)
                    };

                    var response = await client.SendAsync(httpRequest);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        return true;
                    }
                    else if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        _logger.LogWarning("Entity not found for deletion in tenant {TenantId}, modelId {ModelId}", tenantId, modelId);
                        return false;
                    }
                    else
                    {
                        await HandleErrorResponse(response, tenantId, modelId);
                        return false; // This line should never be reached due to exception throwing above
                    }
                }
                catch (Exception ex) when (!(ex is BackendValidationException || ex is BackendSystemException || ex is BackendEntityNotFoundException))
                {
                    _logger.LogError(ex, "Unexpected error in RemoveEntityAsync<T> for tenant {TenantId}, modelId {ModelId}", tenantId, modelId);
                    throw new BackendSystemException("unexpected_error", "An unexpected error occurred while communicating with the backend", ex.Message);
                }
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


    }

}
