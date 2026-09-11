using Journeys.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.DataStorage
{
    public interface IExternalReferenceAdapter
    {
        Task<PagedResultSet<ExternalReference>> FetchExtReferenceFromIdAsync(string tenantId, string extRefId, string type, int pageSize, string? continuationToken = null);
        Task<ExternalReference?> FetchExtReferenceByDeterministicIdAsync(string tenantId, string type, string mapFromId);
        Task<ExternalReference> UpsertExtReferenceAsync(string tenantId, ExternalReference extRef);
        Task DeleteExtReferenceAsync(string tenantId, string extRefId, string type);
        Task DeleteExtReferenceAsync(string tenantId, ExternalReference extRef);

        ExternalReference CreateExternalReference(string tenantId, string type, string status, string mapFromId, string mapToId);
        void SetPolymorphicTypes(Dictionary<string, Type> map, string typeMapPropertyName);
    }
}
