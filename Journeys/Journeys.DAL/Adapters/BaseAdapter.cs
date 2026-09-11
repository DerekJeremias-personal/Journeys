using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using Journeys.DTO.Requests;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Journeys.DAL.Adapters
{
    public abstract class BaseAdapter<T> : IDALAdapter<T> where T : TenantedModelBase
    {
        private readonly IDynamicDataAdapter _dynAdapter;

        protected JsonSerializerOptions _serializerOptions;

        public BaseAdapter(IDynamicDataAdapter dynamicDataAdapter)
        {
            _dynAdapter = dynamicDataAdapter;
            _serializerOptions = _dynAdapter.GetJsonSerializerOptions();
        }

        public virtual async Task<T> FetchEntityAsync(string tenantId, string id, string modelId, string pk = null, string pk2 = null)
        {
            return await _dynAdapter.GetEntityAsync<T>(tenantId, id, modelId, pk, pk2, _serializerOptions);
        }

        public virtual async Task<PagedResultSet<T>> FetchEntityByKeyAsync(string tenantId, string key, string modelId, int pageSize, string pk2 = null, string? continuationToken = null)
        {
            return await _dynAdapter.GetEntitiesByPKAsync<T>(tenantId, key, modelId, pageSize, pk2, continuationToken, _serializerOptions);
        }

        public virtual async Task<PagedResultSet<T>> GetEntitiesByFiltersAsync(string tenantId, string modelId, string query, Dictionary<string, object> paramaters, string sortBy, SortOrder sortOrder, int pageSize, string continuationToken, bool includeChildModels = false)
        {
            return await _dynAdapter.QueryEntitiesAsync<T>(
                tenantId,
                modelId,
                query,
                paramaters,
                sortBy,
                sortOrder,
                pageSize,
                default,
                continuationToken,
                _serializerOptions,
                includeChildModels
            );
        }

        public virtual async Task<List<T>> GetManyEntitiesAsync(string tenantId, string modelId, List<string> ids)
        {
            return await _dynAdapter.GetManyEntitiesAsync<T>(tenantId, ids, modelId, null, _serializerOptions);
        }
        public virtual async Task<List<T>> GetManyEntitiesAsync(string tenantId, string modelId, List<(string, Dictionary<string, string>)> ids)
        {
            return await _dynAdapter.GetManyEntitiesAsync<T>(tenantId, ids, modelId, null, _serializerOptions);
        }

        public virtual async Task<PagedResultSet<T>> GetAllEntitiesAsync(string tenantId, string modelId, int pageSize, string continuationToken = null)
        {
            return await _dynAdapter.GetAllEntitiesAsync<T>(tenantId, modelId, pageSize, continuationToken, _serializerOptions);
        }

        public virtual async Task<T> UpsertEntityAsync(string tenantId, string modelId, T entity, Type? entityType = default(Type))
        {
            //Ensure housekeeping fields are present and up to date
            entity.TenantId = tenantId.ToLower();
            entity.ModelId = modelId;
            entity.LastUpdated = DateTime.UtcNow;
            entity.CreateDate = ((!entity.CreateDate.HasValue || entity.CreateDate == DateTimeOffset.MinValue) ? DateTime.UtcNow : entity.CreateDate);
            if (string.IsNullOrEmpty(entity.Id))
            {
                entity.Id = Guid.NewGuid().ToString();
            }
            
            return await _dynAdapter.SetEntityAsync(tenantId, entity, modelId, entityType, _serializerOptions);
        }

        public virtual async Task<T> MoveEntityAsync(string tenantId, string modelId, T entity, Dictionary<string, string> newPartition, Type? entityType = default(Type))
        {
            //Ensure housekeeping fields are present and up to date
            entity.TenantId = tenantId;
            entity.ModelId = modelId;
            entity.LastUpdated = DateTime.UtcNow;

            return await _dynAdapter.MoveEntityAsync(tenantId, entity, newPartition, modelId, entityType, _serializerOptions);
        }

        public virtual async Task DeleteEntityAsync(string tenantId, string modelId, T entity)
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
