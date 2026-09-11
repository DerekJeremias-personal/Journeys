using Journeys.Core.Caching;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Services;
using Journeys.Core.Utility;
using Journeys.DTO.Models;
using Journeys.DTO.Models.RulesEngine;
using Journeys.DTO.Requests;
using Journeys.DTO.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Journeys.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardRoleController : ControllerBase
    {
        private readonly IDashboardRoleService _dashboardRoleService;
        private readonly ILogger<DashboardRoleController> _logger;


        public DashboardRoleController(IConfiguration configuration,IDashboardRoleService dashboardRoleService, ILogger<DashboardRoleController> logger)
        {
            _dashboardRoleService = dashboardRoleService;
            _logger=logger;
        }

        [HttpGet("{tenantId}/{dashboareRoleId}")]
        public async Task<IActionResult> GetDashboardRoleAsync(string tenantId, string dashboareRoleId)
        {
            try
            {
                var roles = await _dashboardRoleService.FetchDashboardRoleAsync(tenantId, dashboareRoleId);

                if (roles == null)
                {
                    return NotFound(new { message = "No dashboard roles found for the specified tenant and role." });
                }

                return Ok(roles);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "An unexpected error occurred while fetching dashboard roles.",
                    error = ex.Message
                });
            }
        }

        [HttpPost("{tenantId}/getall")]
        public async Task<ActionResult<PagedResultSetResponse<List<DashboardRoleDto>>>> GetAllDashboardRoleAsync(string tenantId, [FromBody] GetAllRequest req)
        {
            var res = await _dashboardRoleService.GetAllDashboardRolesAsync(tenantId, req.PageSize, req.ContinuationToken);
            if (res == null)
            {
                return NotFound();
            }
            return Ok(res);
        }

        [HttpPost("{tenantId}/save")]
        public async Task<ActionResult<DashboardRoleDto>> UpsertDashboardRoleAsync(string tenantId, DashboardRoleDto dashboardRoleDto)
        {
            var ret = await _dashboardRoleService.UpsertDashboardRoleAsync(tenantId, dashboardRoleDto);
            if (ret == null)
            {
                return NotFound();
            }
            return Ok(ret);
        }

        [HttpPost("{tenantId}/{modelName}/query")]
        public async Task<IActionResult> Query(string tenantId, string modelName, [FromBody] GetByQueryRequest request, CancellationToken cancellation = default(CancellationToken))
        {
            try
            {
              
                var response = await _dashboardRoleService.QueryAsync(tenantId, modelName, request.Query, request.Parameters, request.SortBy, request.SortOrder, request.PageSize, request.ContinuationToken, cancellation, false);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                return BadRequest(ex.Message);
            }
        }


    }
}
