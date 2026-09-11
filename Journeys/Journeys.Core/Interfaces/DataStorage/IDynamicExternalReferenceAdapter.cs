using Journeys.Core.Interfaces.Entities;
using Journeys.Core.Models;
using Journeys.DTO.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.DataStorage
{
    public interface IDynamicExternalReferenceAdapter
    {
        Task<ExternalReference?> FetchExtReferenceFromDto(string tenantId, string xId, ExternalReferenceDto dto);
        Task<ExternalReference?> FetchExtReferenceByKeyAsync(string tenantId, string type, string xid);
        Task<PagedResultSet<JsonElement>> FetchEntityByKeyAsync(string tenantId, string key, string modelId, int pageSize, string pk2 = null, string? continuationToken = null);

        Task<JsonElement?> GetEntityByExtId(string tenantId, string modelId, string extId, string extIdType, string pk = null, string pk2 = null);

        Task<JsonElement> UpsertEntityAsync(string tenantId, string modelId, WrappedEventPayload entity, string xId, string? type = null, string? status = null);

        ExternalReference CreateExternalReference(string tenantId, string type, string status, string mapFromId, string mapToId);

        Task DeleteExternalReferenceAsync(string tenantId, string id, string type, string xid);
        Task DeleteEntityAsync(string tenantId, string modelId, JsonElement entity);
        Task DeleteEntityAsync(string tenantId, string modelId, string id, Dictionary<string, string>? pks = null);

    }

}
