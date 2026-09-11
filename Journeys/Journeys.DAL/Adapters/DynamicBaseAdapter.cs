using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Journeys.DAL.Adapters
{
    public class DynamicBaseAdapter
    {
        private readonly IDynamicDataAdapter _dynAdapter;

        protected JsonSerializerOptions _serializerOptions;

        public DynamicBaseAdapter(IDynamicDataAdapter dynamicDataAdapter)
        {
            _dynAdapter = dynamicDataAdapter;
            _serializerOptions = _dynAdapter.GetJsonSerializerOptions();
        }

        public virtual async Task<JsonElement> FetchEntityAsync(string tenantId, string id, string modelId, string pk = null, string pk2 = null)
        {
            return await _dynAdapter.GetEntityAsync<JsonElement>(tenantId, id, modelId, pk, pk2, _serializerOptions);
        }

        public virtual async Task<PagedResultSet<JsonElement>> FetchEntityByKeyAsync(string tenantId, string key, string modelId, int pageSize, string pk2 = null, string? continuationToken = null)
        {
            return await _dynAdapter.GetEntitiesByPKAsync<JsonElement>(tenantId, key, modelId, pageSize, pk2, continuationToken, _serializerOptions);
        }

        public virtual async Task<List<JsonElement>> GetEntitiesByFiltersAsync(string tenantId, string modelId, List<string> filters, string pk = null)
        {
            return null; // await _dynAdapter.(tenantId, modelId, filters);
        }

        public virtual async Task<List<JsonElement>> GetManyEntitiesAsync(string tenantId, string modelId, List<string> ids)
        {
            return await _dynAdapter.GetManyEntitiesAsync<JsonElement>(tenantId, ids, modelId, null, _serializerOptions);
        }
        public virtual async Task<List<JsonElement>> GetManyEntitiesAsync(string tenantId, string modelId, List<(string, Dictionary<string, string>)> ids)
        {
            return await _dynAdapter.GetManyEntitiesAsync<JsonElement>(tenantId, ids, modelId, null, _serializerOptions);
        }

        public virtual async Task<PagedResultSet<JsonElement>> GetAllEntitiesAsync(string tenantId, string modelId, int pageSize, string continuationToken = null)
        {
            return await _dynAdapter.GetAllEntitiesAsync<JsonElement>(tenantId, modelId, pageSize, continuationToken, _serializerOptions);
        }

        public virtual async Task<JsonElement> UpsertEntityAsync(string tenantId, string modelId, JsonElement entity, Type? entityType = default(Type))
        {
            return await _dynAdapter.SetEntityAsync(tenantId, entity, modelId, entityType, _serializerOptions);
        }

        public virtual async Task DeleteEntityAsync(string tenantId, string modelId, JsonElement entity)
        {
            await _dynAdapter.RemoveEntityAsync(tenantId, entity, modelId);
        }

        public virtual async Task DeleteEntityAsync(string tenantId, string modelId, string id, Dictionary<string, string>? pks = null)
        {
            await _dynAdapter.RemoveEntityAsync(tenantId, id, modelId, pks);
        }

        public void SetPolymorphicTypes(Dictionary<string, Type> map, string typeMapPropertyName)
        {
            _dynAdapter.SetPolymorphicTypeMap(map, typeMapPropertyName);
            _serializerOptions = _dynAdapter.GetJsonSerializerOptions();
        }

        public JsonSerializerOptions? GetJsonSerializerOptions()
        {
            return _dynAdapter.GetJsonSerializerOptions();
        }

    }
}
