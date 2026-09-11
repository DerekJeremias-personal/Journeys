using Journeys.Core.Extensions;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.DTO.Models;
using Journeys.DTO.Responses;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Services
{
    public class DashboardRoleService :IDashboardRoleService 
    {

        private readonly IDashboardRoleAdapter _dashboardRoleAdapter;
        private readonly ILogger<DashboardRoleService> _logger;
        private readonly IDynamicDataAdapter _dynamicDataAdapter;
        private readonly string MODEL_ID = "35ff14f7-3bae-42d9-b2db-d2d4eb4b01";
        public DashboardRoleService(IDashboardRoleAdapter dashboardRoleAdapter, ILogger<DashboardRoleService> logger, IDynamicDataAdapter dynamicDataAdapter)
        {
            _dashboardRoleAdapter = dashboardRoleAdapter;
            _logger = logger;
            _dynamicDataAdapter=dynamicDataAdapter;
        }
        public async Task<DashboardRoleDto> FetchDashboardRoleAsync(string tenantId, string id)
        {
            DashboardRoleDto dashboardRoleDto = null;
            try
            {
                var res = await _dashboardRoleAdapter.FetchDashboardRoleAsync(tenantId, id);
                dashboardRoleDto = res.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return dashboardRoleDto;
        }
        public async Task<PagedResultSetResponse<DashboardRoleDto>> GetAllDashboardRolesAsync(string tenantId, int pageSize, string continutationToken = null)
        {
            PagedResultSetResponse<DashboardRoleDto> dashboardRoleDto = new PagedResultSetResponse<DashboardRoleDto>();
            try
            {
                var res = await _dashboardRoleAdapter.GetAllDashboardRoleAsync(tenantId, pageSize, continutationToken);
                dashboardRoleDto.Count = res.Count;
                dashboardRoleDto.ContinuationToken = res.ContinuationToken;
                dashboardRoleDto.Entities = res.Entities.Select(x => x.ToDto()).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return dashboardRoleDto;
        }

        public async Task<DashboardRoleDto> UpsertDashboardRoleAsync(string tenantId, DashboardRoleDto dashboardRoleDto)
        {
            try
            {
                var res = await _dashboardRoleAdapter.UpsertDashboardRoleAsync(tenantId, dashboardRoleDto.FromDto());
                dashboardRoleDto = res.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return dashboardRoleDto;
        }

        public async Task<PagedResultSetResponse<DashboardRoleDto>> QueryAsync(string tenantId, string modelName, string query, Dictionary<string, object> parameters, string sortBy, DTO.Requests.SortOrder sortOrder, int pageSize, string? continuationToken, CancellationToken token, bool includeChildModels)
        {
            try
            {
  
                var result = await _dynamicDataAdapter.QueryEntitiesAsync<DashboardRoleDto>(tenantId, MODEL_ID, query, parameters, sortBy, (DTO.Requests.SortOrder)sortOrder, pageSize, token, continuationToken, null, includeChildModels);
                var response = new PagedResultSetResponse<DashboardRoleDto>();

                response.Entities = result?.Entities?.Select(entity => new DashboardRoleDto
                {
                    TenantId = tenantId,
                    Id = entity.Id,
                    DashboardId = entity.DashboardId,
                    RoleId = entity.RoleId,
                    CreateDate = (DateTimeOffset)entity.CreateDate,
                    LastUpdated = (DateTimeOffset)entity.LastUpdated,
                })?.ToList() ?? new List<DashboardRoleDto>();
                response.ContinuationToken = result.ContinuationToken;
                response.Count = result.Count;
                return response?? null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing QueryAsync for tenant {TenantId} and model {ModelName}.", tenantId, modelName);
                throw;
            }
        }

    }
}
