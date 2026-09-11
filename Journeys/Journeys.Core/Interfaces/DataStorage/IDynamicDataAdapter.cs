using Journeys.Core.Models;
using Journeys.DTO.Requests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.DataStorage
{
    public interface IDynamicDataAdapter
    {
        JsonSerializerOptions GetJsonSerializerOptions();

        Task<T?> GetEntityAsync<T>(string tenantid, string id, string modelId, string pk = null, string pk2 = null, JsonSerializerOptions serializerOptions = null);

        Task<PagedResultSet<T>> GetEntitiesByPKAsync<T>(string tenantId, string partitionKey, string modelId, int pageSize, string pk2 = null, string? continuationToken = null, JsonSerializerOptions serializerOptions = null);

        Task<List<T>> GetManyEntitiesAsync<T>(string tenantId, List<string> ids, string modelId, Type? entityType = default, JsonSerializerOptions serializerOptions = null);
        Task<List<T>> GetManyEntitiesAsync<T>(string tenantId, List<(string, Dictionary<string, string>)> ids, string modelId, Type? entityType = default, JsonSerializerOptions serializerOptions = null);
        Task<PagedResultSet<T>> GetAllEntitiesAsync<T>(string tenantId, string modelId, int pageSize, string? continuationToken = null, JsonSerializerOptions serializerOptions = null);

        Task<T> SetEntityAsync<T>(string tenantId, T entity, string modelId, Type? entityType = default, JsonSerializerOptions serializerOptions = null);
        Task<object> SetLogicalEntityAsync(string tenantId, JsonElement entity, string modelId, JsonSerializerOptions serializerOptions = null);
        Task<T> MoveEntityAsync<T>(string tenantId, T entity, Dictionary<string, string> newPartition, string modelId, Type? entityType = default(Type), JsonSerializerOptions serializerOptions = null);

        Task<bool> RemoveEntityAsync(string tenantId, string id, string modelId, Dictionary<string, string> pks = null);
        Task<bool> RemoveEntityAsync<T>(string tenantId, T entity, string scehmaId);

        void SetPolymorphicTypeMap(Dictionary<string, Type> map, string typeMapPropertyName);
        Task<PagedResultSet<T>> QueryEntitiesAsync<T>(string tenantId, string modelId, string query, Dictionary<string, object> parameters, string sortBy, SortOrder sortOrder, int pageSize, CancellationToken token = default(CancellationToken), string continuationToken = null, JsonSerializerOptions serializerOptions = null, bool includeChildModels = false);
    }
}
