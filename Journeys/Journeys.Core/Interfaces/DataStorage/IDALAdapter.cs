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
    public interface IDALAdapter<T> where T : TenantedModelBase
    {
        Task<T> FetchEntityAsync(string tenantId, string id, string modelId, string pk = null, string pk2 = null);
        Task<PagedResultSet<T>> FetchEntityByKeyAsync(string tenantId, string key, string modelId, int pageSize, string pk2 = null, string? continuationToken = null);
        Task<PagedResultSet<T>> GetEntitiesByFiltersAsync(string tenantId, string modelId, string query, Dictionary<string, object> paramaters, string sortBy, SortOrder sortOrder, int pageSize, string continuationToken, bool includeChildModels);

        Task<T> UpsertEntityAsync(string tenantId, string modelId, T entity, Type? entityType = default);
        Task<T> MoveEntityAsync(string tenantId, string modelId, T entity, Dictionary<string, string> newPartition, Type? entityType = default(Type));

        Task DeleteEntityAsync(string tenantId, string modelId, T entity);
        Task DeleteEntityAsync(string tenantId, string modelId, string id, Dictionary<string, string>? pks = null);

        JsonSerializerOptions GetJsonSerializerOptions();
    }
}
