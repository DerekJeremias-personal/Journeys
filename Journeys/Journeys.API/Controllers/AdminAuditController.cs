using Journeys.Core.Interfaces.Services;
using Journeys.Core.Services;
using Journeys.DTO.Models;
using Journeys.DTO.Requests;
using Microsoft.AspNetCore.Mvc;

namespace Journeys.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminAuditController : ControllerBase
    {
        private readonly IAdminAuditService _adminAuditService;
        private readonly ILogger<AdminAuditController> _logger;

        public AdminAuditController(IAdminAuditService adminAuditService, ILogger<AdminAuditController> logger)
        {
            _adminAuditService = adminAuditService;
            _logger = logger;
        }

        [HttpPost("{tenantId}/save")]
        public async Task<IActionResult> Upsert(string tenantId, [FromBody] AdminAuditDto request)
        {
            try
            {
                var newConfig = await _adminAuditService.UpsertAdminAuditAsync(tenantId, request);
                return Ok(newConfig);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
        }

        [HttpDelete("{tenantId}/{auditId}")]
        public async Task<IActionResult> Delete(string tenantId, string auditId)
        {
            //var result = await _adminAuditService.Del(tenantId, auditId);
            return NoContent();
        }

        [HttpGet("{tenantId}/{auditId}")]
        public async Task<IActionResult> GetAdminAudit(string tenantId, string auditId, string yearMonth)
        {
            var result = await _adminAuditService.FetchAdminAuditAsync(tenantId, auditId, yearMonth);
            return Ok(result);
        }

        [HttpPost("{tenantId}/query")]
        public async Task<IActionResult> Query(string tenantId, [FromBody] QueryAdminAuditsRequest request, CancellationToken cancellation = default(CancellationToken))
        {
            try
            {
                var response = await _adminAuditService.QueryAsync(tenantId, request, cancellation);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
        }
    }
}
