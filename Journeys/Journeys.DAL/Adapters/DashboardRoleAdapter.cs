using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DAL.Adapters
{
    public class DashboardRoleAdapter : BaseAdapter<DashboardRole>, IDashboardRoleAdapter
    {
        private const string DASHBOARD_ROLE_MODEL_ID = "35ff14f7-3bae-42d9-b2db-d2d4eb4b01";
        public DashboardRoleAdapter(IDynamicDataAdapter dynAdapter) : base(dynAdapter)
        {
        }
        public async Task<DashboardRole> FetchDashboardRoleAsync(string tenantId, string id)
        {
            return await base.FetchEntityAsync(tenantId, id, DASHBOARD_ROLE_MODEL_ID);
        }
        public async Task<PagedResultSet<DashboardRole>> GetAllDashboardRoleAsync(string tenantId, int pageSize, string continuationToken = null)
        {
            return await base.GetAllEntitiesAsync(tenantId, DASHBOARD_ROLE_MODEL_ID, pageSize, continuationToken);
        }
        public async Task<DashboardRole> UpsertDashboardRoleAsync(string tenantId, DashboardRole dashboardRole)
        {
            return await base.UpsertEntityAsync(tenantId, DASHBOARD_ROLE_MODEL_ID, dashboardRole, typeof(DashboardRole));
        }

    }
}
