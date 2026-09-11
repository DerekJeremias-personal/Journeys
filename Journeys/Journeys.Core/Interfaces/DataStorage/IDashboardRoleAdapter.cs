using Journeys.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.DataStorage
{
    public interface IDashboardRoleAdapter
    {
        Task<DashboardRole> FetchDashboardRoleAsync(string tenantId, string id);
        Task<PagedResultSet<DashboardRole>> GetAllDashboardRoleAsync(string tenantId, int pageSize, string continuationToken = null);
        Task<DashboardRole> UpsertDashboardRoleAsync(string tenantId, DashboardRole dashboardRole);
    }
}
