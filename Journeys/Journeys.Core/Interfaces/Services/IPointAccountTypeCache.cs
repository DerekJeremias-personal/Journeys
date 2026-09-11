using Journeys.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.Services
{
    public interface IPointAccountTypeCache
    {
        Task<PointAccountType> GetPointAccountTypeAsync(string tenantId, string pointAccountTypeId);
        Task<bool> EnsurePATsLoaded(string tenantId);
        Task<List<PointAccountType>> GetAllPointAccountTypes(string tenantId);

        bool CachePointAccountType(string tenantId, PointAccountType pointAccountType);

        /// <summary>Removes one PAT from hybrid cache.</summary>
        Task InvalidatePointAccountTypeAsync(string tenantId, string pointAccountTypeId);

        /// <summary>Removes all PAT entries for a tenant from cache.</summary>
        Task InvalidateTenantPointAccountTypesAsync(string tenantId);

    }
}
