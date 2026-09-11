using Journeys.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.DataStorage
{
    public interface IPointAccountTypeAdapter
    {
        Task<PointAccountType> FetchPointAccountTypeAsync(string tenantId, string entityId);
        Task<PagedResultSet<PointAccountType>> GetAllPointAccountTypesAsync(string tenantId, int pageSize, string continuationToken = null);
        Task<PointAccountType> UpsertPointAccountTypeAsync(string tenantId, PointAccountType acctType);
        Task DeletePointAccountTypeAsync(string tenantId, string id);
        Task DeletePointAccountTypeAsync(string tenantId, PointAccountType acctType);
    }
}
