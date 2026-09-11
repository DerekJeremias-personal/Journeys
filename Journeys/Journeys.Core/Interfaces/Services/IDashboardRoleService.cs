using Journeys.DTO.Models;
using Journeys.DTO.Models.RulesEngine;
using Journeys.DTO.Requests;
using Journeys.DTO.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.Services
{
    public interface IDashboardRoleService
    {
        Task<DashboardRoleDto> FetchDashboardRoleAsync(string tenantId, string id);
        Task<PagedResultSetResponse<DashboardRoleDto>> GetAllDashboardRolesAsync(string tenantId, int pageSize, string continutationToken = null);
        Task<DashboardRoleDto> UpsertDashboardRoleAsync(string tenantId, DashboardRoleDto dashboardRoleDto);
        Task<PagedResultSetResponse<DashboardRoleDto>> QueryAsync(string tenantId, string modelName, string query, Dictionary<string, object> parameters, string sortBy, SortOrder sortOrder, int pageSize, string? continuationToken, CancellationToken token, bool includeChildModels);


    }
}
