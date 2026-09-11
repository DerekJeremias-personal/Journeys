using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Tests.Stubs
{
    internal class StubPointAccountTypeAdapter : IPointAccountTypeAdapter
    {
        Dictionary<string, Dictionary<string, PointAccountType>> _pointAccountTypes = new Dictionary<string, Dictionary<string, PointAccountType>>();
        public int FetchCallCount { get; private set; }

        public async Task<PointAccountType> FetchPointAccountTypeAsync(string tenantId, string pointAccountTypeId)
        {
            FetchCallCount++;
            // Return null for testing - the cache should handle this
            return null;
        }

        public async Task<PagedResultSet<PointAccountType>> GetAllPointAccountTypesAsync(string tenantId, int pageSize, string continuationToken = null)
        {
            // Return empty set for testing
            return new PagedResultSet<PointAccountType>
            {
                Entities = new List<PointAccountType>(),
                Count = 0,
                ContinuationToken = null
            };
        }

        public async Task<PointAccountType> UpsertTagAsync(string tenantId, PointAccountType acctType)
        {
            if (string.IsNullOrEmpty(tenantId) || acctType == null ||
                string.IsNullOrEmpty(acctType.Id)) return null;

            if (!_pointAccountTypes.ContainsKey(tenantId))
            {
                _pointAccountTypes.Add(tenantId, new Dictionary<string, PointAccountType>
                {
                    { acctType.Id, acctType  }
                });
                return acctType;
            }
            if (!_pointAccountTypes[tenantId].ContainsKey(acctType.Id))
            {
                _pointAccountTypes[tenantId].Add(acctType.Id, acctType);
                return acctType;
            }
            _pointAccountTypes[tenantId][acctType.Id] = acctType;

            return await Task.FromResult(acctType);
        }

        public async Task DeleteEntityTagsAsync(string tenantId, string entityId)
        {
            if (!string.IsNullOrEmpty(tenantId) && _pointAccountTypes.ContainsKey(tenantId))
            {
                var tenant = _pointAccountTypes[tenantId];
                if (tenant != null && tenant.ContainsKey(entityId))
                {
                    tenant.Remove(entityId);
                }
            }
        }

        public async Task DeleteTagAsync(string tenantId, PointAccountType acctType)
        {
            if (!string.IsNullOrEmpty(tenantId) && _pointAccountTypes.ContainsKey(tenantId))
            {
                var tenant = _pointAccountTypes[tenantId];
                if (tenant != null && tenant.ContainsKey(acctType.Id))
                {
                    tenant.Remove(acctType.Id);
                }
            }
        }

        

        public Task<PointAccountType> UpsertPointAccountTypeAsync(string tenantId, PointAccountType acctType)
        {
            throw new NotImplementedException();
        }

        public Task DeletePointAccountTypeAsync(string tenantId, string id)
        {
            throw new NotImplementedException();
        }

        public Task DeletePointAccountTypeAsync(string tenantId, PointAccountType acctType)
        {
            throw new NotImplementedException();
        }
    }
}
