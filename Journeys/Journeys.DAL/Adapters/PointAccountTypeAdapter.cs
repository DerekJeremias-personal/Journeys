using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DAL.Adapters
{
    public class PointAccountTypeAdapter : BaseAdapter<PointAccountType>, IPointAccountTypeAdapter
    {
        private const string POINT_ACCOUNT_TYPE_MODEL_ID = "393dcebf-493d-4983-831b-b52f7268a6ef";

        public PointAccountTypeAdapter(IDynamicDataAdapter dynAdapter) : base(dynAdapter)
        {
        }

        public async Task<PointAccountType> FetchPointAccountTypeAsync(string tenantId, string id)
        {
            return await base.FetchEntityAsync(tenantId, id, POINT_ACCOUNT_TYPE_MODEL_ID);
        }

        public async Task<PagedResultSet<PointAccountType>> GetAllPointAccountTypesAsync(string tenantId, int pageSize, string continuationToken = null)
        {
            return await base.GetAllEntitiesAsync(tenantId, POINT_ACCOUNT_TYPE_MODEL_ID, pageSize, continuationToken);
        }

        public async Task<PointAccountType> UpsertPointAccountTypeAsync(string tenantId, PointAccountType acctType)
        {
            return await base.UpsertEntityAsync(tenantId, POINT_ACCOUNT_TYPE_MODEL_ID, acctType, typeof(PointAccountType));
        }

        public async Task DeletePointAccountTypeAsync(string tenantId, string id)
        {
            await base.DeleteEntityAsync(tenantId, POINT_ACCOUNT_TYPE_MODEL_ID, id);
        }

        public async Task DeletePointAccountTypeAsync(string tenantId, PointAccountType acctType)
        {
            await base.DeleteEntityAsync(tenantId, POINT_ACCOUNT_TYPE_MODEL_ID, acctType);
        }

    }
}
