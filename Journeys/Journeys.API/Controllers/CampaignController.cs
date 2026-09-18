using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.Core.Services;
using Journeys.DTO.Exceptions;
using Journeys.DTO.Models;
using Journeys.DTO.Requests;
using Journeys.DTO.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;

namespace Journeys.API.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class CampaignController : ControllerBase
    {

        private readonly ICampaignService _campaignService;
        private readonly ICampaignAssistantContextService _campaignAssistantContextService;
        private readonly ILogger<CampaignController> _logger;

        public CampaignController(
            ICampaignService campaignService,
            ICampaignAssistantContextService campaignAssistantContextService,
            ILogger<CampaignController> logger)
        {
            _campaignService = campaignService;
            _campaignAssistantContextService = campaignAssistantContextService;
            _logger = logger;
        }

        [HttpGet("{tenantId}/{campaignId}/assistant-context")]
        public async Task<ActionResult<CampaignAssistantContextDto>> GetCampaignAssistantContextAsync(
            string tenantId,
            string campaignId,
            [FromQuery] string? status = null,
            [FromQuery] bool includeSampleTemplate = true,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(campaignId))
                return BadRequest(new { error = "Tenant ID and Campaign ID are required.", tenantId, campaignId });

            try
            {
                status ??= CampaignStatusStrings.Live;
                var ctx = await _campaignAssistantContextService.GetContextAsync(
                    tenantId,
                    campaignId,
                    status,
                    includeSampleTemplate,
                    cancellationToken);
                if (ctx == null)
                {
                    return NotFound(new
                    {
                        error = $"Campaign with ID '{campaignId}' and status '{status}' not found.",
                        tenantId,
                        campaignId,
                        status
                    });
                }

                return Ok(ctx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building assistant context for campaign {CampaignId} status {Status} tenant {TenantId}",
                    campaignId, status, tenantId);
                return StatusCode(500, new { error = "An unexpected error occurred while building assistant context." });
            }
        }

        [HttpGet("{tenantId}/{campaignId}")]
        public async Task<ActionResult<CampaignDto>> GetCampaignAsync(string tenantId, string campaignId, string campaignStatus = null)
        {
            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(campaignId))
            {
                return BadRequest(new { error = "Tenant ID and Campaign ID are required.", tenantId, campaignId });
            }

            try
            {
                campaignStatus ??= CampaignStatusStrings.Live;
                var campaign = await _campaignService.FetchCampaignAsync(tenantId, campaignId, campaignStatus);
                if (campaign == null)
                {
                    return NotFound(new { error = $"Campaign with ID '{campaignId}' and status '{campaignStatus}' not found.", tenantId, campaignId, campaignStatus });
                }
                return Ok(campaign);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching campaign {CampaignId} with status {Status} for tenant {TenantId}", campaignId, campaignStatus, tenantId);
                return StatusCode(500, new { error = "An unexpected error occurred while fetching the campaign." });
            }
        }

        [HttpPost("{tenantId}/getmany")]
        public async Task<ActionResult<List<CampaignDto>>> GetCampaignsAsync(string tenantId, [FromBody] GetManyCampaignsRequest request)
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                return BadRequest(new { error = "Tenant ID is required.", tenantId });
            }

            if (request == null)
            {
                return BadRequest(new { error = "Request body is required." });
            }

            try
            {
                var campaigns = await _campaignService.GetManyCampaignsAsync(tenantId, request.Ids, request.Status);
                if (campaigns == null || campaigns.Count == 0)
                {
                    return NotFound(new { error = $"No campaigns found for the specified IDs and status.", tenantId, status = request.Status });
                }
                return Ok(campaigns);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching multiple campaigns for tenant {TenantId} with status {Status}", tenantId, request?.Status);
                return StatusCode(500, new { error = "An unexpected error occurred while fetching campaigns." });
            }
        }

        [HttpPost("{tenantId}")]
        public async Task<ActionResult<PagedResultSetResponse<CampaignDto>>> GetCampaignsByFiltersAsync(string tenantId, [FromBody] GetCampaignsByFilterRequest req)
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                return BadRequest(new { error = "Tenant ID is required.", tenantId });
            }

            if (req == null)
            {
                return BadRequest(new { error = "Filter request body is required." });
            }

            try
            {
                var campaigns = await _campaignService.GetCampaignsByFiltersAsync(tenantId, req);
                if (campaigns == null)
                {
                    return NotFound(new { error = "No campaigns found matching the specified filters.", tenantId });
                }
                return Ok(campaigns);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching campaigns by filters for tenant {TenantId}", tenantId);
                return StatusCode(500, new { error = "An unexpected error occurred while fetching campaigns." });
            }
        }

        [HttpPost("{tenantId}/getall")]
        public async Task<ActionResult<PagedResultSetResponse<CampaignDto>>> GetAllCampaignsAsync(string tenantId, [FromBody] GetAllRequest req)
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                return BadRequest(new { error = "Tenant ID is required.", tenantId });
            }

            if (req == null)
            {
                return BadRequest(new { error = "Request body is required." });
            }

            try
            {
                var res = await _campaignService.GetAllCampaignsAsync(tenantId, req.PageSize, req.ContinuationToken);
                if (res == null)
                {
                    return NotFound(new { error = "No campaigns found for the specified tenant.", tenantId });
                }
                return Ok(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all campaigns for tenant {TenantId}", tenantId);
                return StatusCode(500, new { error = "An unexpected error occurred while fetching campaigns." });
            }
        }

        [HttpPost("{tenantId}/validate")]
        public async Task<ActionResult<CampaignValidationResultDto>> ValidateCampaignAsync(
            string tenantId,
            [FromBody] CampaignDto req,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(tenantId))
                return BadRequest(new { error = "Tenant ID is required.", tenantId });

            if (req == null)
                return BadRequest(new { error = "Campaign data is required." });

            try
            {
                var result = await _campaignService.ValidateCampaignAsync(tenantId, req, cancellationToken);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error validating campaign {CampaignId} (ExtCampaignId: {ExtCampaignId}) for tenant {TenantId}",
                    req.Id, req.ExtCampaignId, tenantId);
                return StatusCode(500, new { error = "An unexpected error occurred while validating the campaign." });
            }
        }

        [HttpPost("{tenantId}/save")]
        public async Task<ActionResult<CampaignDto>> SaveCampaignAsync(string tenantId, [FromBody] CampaignDto req)
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                return BadRequest(new { error = "Tenant ID is required.", tenantId });
            }

            if (req == null)
            {
                return BadRequest(new { error = "Campaign data is required." });
            }

            try
            {
                var savedCampaign = await _campaignService.UpsertCampaignAsync(tenantId, req);
                return Ok(savedCampaign);
            }
            catch (APIErrorsException ex)
            {
                _logger.LogError(ex, "API validation errors saving campaign {CampaignId} (ExtCampaignId: {ExtCampaignId}) for tenant {TenantId}", 
                    req.Id, req.ExtCampaignId, tenantId);
                return BadRequest(new { errors = ex.Errors });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error saving campaign {CampaignId} (ExtCampaignId: {ExtCampaignId}) for tenant {TenantId}", 
                    req.Id, req.ExtCampaignId, tenantId);
                return StatusCode(500, new { error = "An unexpected error occurred while saving the campaign." });
            }
        }

        [HttpPost("{tenantId}/{campaignId}/copy")]
        public async Task<ActionResult<CampaignDto>> CopyCampaignAsync(
            string tenantId,
            string campaignId,
            [FromQuery] string status,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CopyCampaignRequest? request = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                return BadRequest(new { error = "Tenant ID is required.", tenantId });

            if (string.IsNullOrWhiteSpace(campaignId))
                return BadRequest(new { error = "Campaign ID is required.", campaignId });

            if (string.IsNullOrWhiteSpace(status))
                return BadRequest(new { error = "Campaign status is required.", status });

            try
            {
                CampaignShellValidator.ValidateStatus(status);

                _logger.LogInformation(
                    "Copy campaign {CampaignId} for tenant {TenantId}; action {Action}",
                    campaignId,
                    tenantId,
                    "copy");

                var copy = await _campaignService.CopyCampaignAsync(
                    tenantId,
                    campaignId,
                    status,
                    request?.Name,
                    cancellationToken);
                if (copy == null)
                {
                    return NotFound(new
                    {
                        error = $"Campaign with ID '{campaignId}' and status '{status}' not found.",
                        tenantId,
                        campaignId,
                        status
                    });
                }

                return Ok(copy);
            }
            catch (APIErrorsException ex)
            {
                _logger.LogError(
                    ex,
                    "Validation error copying campaign {CampaignId} for tenant {TenantId}; action {Action}",
                    campaignId,
                    tenantId,
                    "copy");
                return BadRequest(new { errors = ex.Errors });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error copying campaign {CampaignId} for tenant {TenantId}; action {Action}",
                    campaignId,
                    tenantId,
                    "copy");
                return StatusCode(500, new { error = "An unexpected error occurred while copying the campaign." });
            }
        }

        [HttpPost("{tenantId}/{campaignId}/restore")]
        public async Task<ActionResult<CampaignDto>> RestoreArchivedCampaignAsync(
            string tenantId,
            string campaignId,
            [FromQuery] string status,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                return BadRequest(new { error = "Tenant ID is required.", tenantId });

            if (string.IsNullOrWhiteSpace(campaignId))
                return BadRequest(new { error = "Campaign ID is required.", campaignId });

            try
            {
                CampaignShellValidator.ValidateStatus(status);

                if (!CampaignStatusStrings.Archive.Equals(status, StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { error = "Campaign status must be archive.", status });

                _logger.LogInformation(
                    "Restore campaign {CampaignId} for tenant {TenantId}; action {Action}",
                    campaignId,
                    tenantId,
                    "restore");

                var restored = await _campaignService.RestoreArchivedCampaignAsync(
                    tenantId,
                    campaignId,
                    cancellationToken);
                if (restored == null)
                {
                    return NotFound(new
                    {
                        error = $"Archived campaign with ID '{campaignId}' not found.",
                        tenantId,
                        campaignId,
                        status
                    });
                }

                return Ok(restored);
            }
            catch (APIErrorsException ex)
            {
                _logger.LogError(
                    ex,
                    "Validation error restoring campaign {CampaignId} for tenant {TenantId}; action {Action}",
                    campaignId,
                    tenantId,
                    "restore");
                return BadRequest(new { errors = ex.Errors });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error restoring campaign {CampaignId} for tenant {TenantId}; action {Action}",
                    campaignId,
                    tenantId,
                    "restore");
                return StatusCode(500, new { error = "An unexpected error occurred while restoring the campaign." });
            }
        }

        [HttpDelete("{tenantId}")]
        public async Task<IActionResult> RemoveCampaignAsync(string tenantId, [FromBody] RemoveCampaignRequest req)
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                return BadRequest(new { error = "Tenant ID is required.", tenantId });
            }

            if (req == null || req.Campaign == null)
            {
                return BadRequest(new { error = "Campaign data is required in the request body." });
            }

            try
            {
                await _campaignService.DeleteCampaignAsync(tenantId, req.Campaign);
                return NoContent();
            }
            catch (ArgumentNullException ex)
            {
                _logger.LogError(ex, "Invalid argument when deleting campaign for tenant {TenantId}", tenantId);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting campaign {CampaignId} for tenant {TenantId}", req.Campaign.Id, tenantId);
                return StatusCode(500, new { error = "An unexpected error occurred while deleting the campaign." });
            }
        }

        [HttpDelete("{tenantId}/{campaignId}")]
        public async Task<IActionResult> RemoveCampaignByIdAsync(string tenantId, string campaignId, string status)
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                return BadRequest(new { error = "Tenant ID is required.", tenantId });
            }

            if (string.IsNullOrEmpty(campaignId))
            {
                return BadRequest(new { error = "Campaign ID is required.", campaignId });
            }

            if (string.IsNullOrEmpty(status))
            {
                return BadRequest(new { error = "Campaign status is required.", status });
            }

            try
            {
                await _campaignService.DeleteCampaignAsync(tenantId, campaignId, status);
                return NoContent();
            }
            catch (ArgumentNullException ex)
            {
                _logger.LogError(ex, "Invalid argument when deleting campaign {CampaignId} with status {Status} for tenant {TenantId}", 
                    campaignId, status, tenantId);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting campaign {CampaignId} with status {Status} for tenant {TenantId}", 
                    campaignId, status, tenantId);
                return StatusCode(500, new { error = "An unexpected error occurred while deleting the campaign." });
            }
        }


        [HttpGet("{tenantId}/pointaccounttype/get/{patId}")]
        public async Task<ActionResult<PointAccountTypeDto>> GetPointAccountTypeAsync(string tenantId, string patId)
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                return BadRequest(new { error = "Tenant ID is required.", tenantId });
            }

            if (string.IsNullOrEmpty(patId))
            {
                return BadRequest(new { error = "Point Account Type ID is required.", patId });
            }

            try
            {
                var pat = await _campaignService.FetchPointAccountType(tenantId, patId);
                if (pat == null)
                {
                    return NotFound(new { error = $"Point Account Type with ID '{patId}' not found.", tenantId, patId });
                }
                return Ok(pat);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching point account type {PatId} for tenant {TenantId}", patId, tenantId);
                return StatusCode(500, new { error = "An unexpected error occurred while fetching the point account type." });
            }
        }

        [HttpPost("{tenantId}/pointaccounttype/getall")]
        public async Task<ActionResult<PagedResultSetResponse<PointAccountTypeDto>>> GetAllPointAccountTypesAsync(string tenantId, [FromBody] GetAllRequest req)
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                return BadRequest(new { error = "Tenant ID is required.", tenantId });
            }

            if (req == null)
            {
                return BadRequest(new { error = "Request body is required." });
            }

            try
            {
                var pat = await _campaignService.GetAllPointAccountTypesAsync(tenantId, req.PageSize, req.ContinuationToken);
                if (pat == null)
                {
                    return NotFound(new { error = "No point account types found for the specified tenant.", tenantId });
                }
                return Ok(pat);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all point account types for tenant {TenantId}", tenantId);
                return StatusCode(500, new { error = "An unexpected error occurred while fetching point account types." });
            }
        }

        [HttpPost("{tenantId}/pointaccounttype/upsert")]
        public async Task<ActionResult<PointAccountTypeDto>> UpsertPointAccountTypesAsync(string tenantId, PointAccountTypeDto pat)
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                return BadRequest(new { error = "Tenant ID is required.", tenantId });
            }

            if (pat == null)
            {
                return BadRequest(new { error = "Point Account Type data is required." });
            }

            try
            {
                var ret = await _campaignService.UpsertPointAccountTypeAsync(tenantId, pat);
                if (ret == null)
                {
                    return NotFound(new { error = "Point Account Type could not be saved.", tenantId, patId = pat.Id });
                }
                return Ok(ret);
            }
            catch (APIErrorsException ex)
            {
                _logger.LogError(ex, "API validation errors upserting point account type {PatId} for tenant {TenantId}", pat.Id, tenantId);
                return BadRequest(new { errors = ex.Errors });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error upserting point account type {PatId} for tenant {TenantId}", pat.Id, tenantId);
                return StatusCode(500, new { error = "An unexpected error occurred while saving the point account type." });
            }
        }

        [HttpDelete("{tenantId}/pointaccounttype/{patId}")]
        public async Task<IActionResult> DeletePointAccountTypeAsync(string tenantId, string patId)
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                return BadRequest(new { error = "Tenant ID is required.", tenantId });
            }

            if (string.IsNullOrEmpty(patId))
            {
                return BadRequest(new { error = "Point Account Type ID is required.", patId });
            }

            try
            {
                await _campaignService.DeletePointAccountTypeAsync(tenantId, patId);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting point account type {PatId} for tenant {TenantId}", patId, tenantId);
                return StatusCode(500, new { error = "An unexpected error occurred while deleting the point account type." });
            }
        }

        [HttpGet("{tenantId}/{campaignId}/stats")]
        public async Task<ActionResult<CampaignStatisticsDto>> GetCampaignStatsAsync(string tenantId, [FromQuery] GetCampaignStatsRequest req)
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                return BadRequest(new { error = "Tenant ID is required.", tenantId });
            }

            if (req == null || string.IsNullOrEmpty(req.CampaignId))
            {
                return BadRequest(new { error = "Campaign ID is required in the request.", campaignId = req?.CampaignId });
            }

            try
            {
                var stats = await _campaignService.GetCampaignStatsAsync(tenantId, req.CampaignId);
                if (stats == null)
                {
                    return NotFound(new { error = $"Statistics not found for campaign '{req.CampaignId}'.", tenantId, campaignId = req.CampaignId });
                }
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching statistics for campaign {CampaignId} for tenant {TenantId}", req.CampaignId, tenantId);
                return StatusCode(500, new { error = "An unexpected error occurred while fetching campaign statistics." });
            }
        }

        [HttpGet("{tenantId}/versions/{extCampaignId}")]
        public async Task<ActionResult<List<CampaignDto>>> GetCampaignVersionsAsync(string tenantId, string extCampaignId)
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                return BadRequest(new { error = "Tenant ID is required.", tenantId });
            }

            if (string.IsNullOrEmpty(extCampaignId))
            {
                return BadRequest(new { error = "External Campaign ID is required.", extCampaignId });
            }

            try
            {
                var campaigns = await _campaignService.GetCampaignVersionsByExtIdAsync(tenantId, extCampaignId);
                if (campaigns == null || campaigns.Count == 0)
                {
                    return NotFound(new { error = $"No campaign versions found for ExtCampaignId '{extCampaignId}'.", tenantId, extCampaignId });
                }
                return Ok(campaigns);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching campaign versions for ExtCampaignId {ExtCampaignId} for tenant {TenantId}", extCampaignId, tenantId);
                return StatusCode(500, new { error = "An unexpected error occurred while fetching campaign versions." });
            }
        }

        [HttpGet("{tenantId}/archived")]
        public async Task<ActionResult<PagedResultSetResponse<CampaignDto>>> GetArchivedCampaignsAsync(
            string tenantId, 
            DateTimeOffset? fromDate = null, 
            DateTimeOffset? toDate = null, 
            int pageSize = 100, 
            string? continuationToken = null)
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                return BadRequest(new { error = "Tenant ID is required.", tenantId });
            }

            try
            {
                var campaigns = await _campaignService.GetArchivedCampaignsByDateRangeAsync(tenantId, fromDate, toDate, pageSize, continuationToken);
                if (campaigns == null)
                {
                    return NotFound(new { error = "No archived campaigns found for the specified criteria.", tenantId, fromDate, toDate });
                }
                return Ok(campaigns);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching archived campaigns for tenant {TenantId} with date range {FromDate} to {ToDate}", 
                    tenantId, fromDate, toDate);
                return StatusCode(500, new { error = "An unexpected error occurred while fetching archived campaigns." });
            }
        }

        [HttpGet("{tenantId}/live/{extCampaignId}")]
        public async Task<ActionResult<CampaignDto>> GetLiveCampaignByExtIdAsync(string tenantId, string extCampaignId)
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                return BadRequest(new { error = "Tenant ID is required.", tenantId });
            }

            if (string.IsNullOrEmpty(extCampaignId))
            {
                return BadRequest(new { error = "External Campaign ID is required.", extCampaignId });
            }

            try
            {
                var campaign = await _campaignService.GetLiveCampaignByExtIdAsync(tenantId, extCampaignId);
                if (campaign == null)
                {
                    return NotFound(new { error = $"No Live campaign found for ExtCampaignId '{extCampaignId}'.", tenantId, extCampaignId });
                }
                return Ok(campaign);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Live campaign for ExtCampaignId {ExtCampaignId} for tenant {TenantId}", extCampaignId, tenantId);
                return StatusCode(500, new { error = "An unexpected error occurred while fetching the Live campaign." });
            }
        }

        [HttpGet("{tenantId}/draft/{extCampaignId}")]
        public async Task<ActionResult<CampaignDto>> GetDraftCampaignByExtIdAsync(string tenantId, string extCampaignId)
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                return BadRequest(new { error = "Tenant ID is required.", tenantId });
            }

            if (string.IsNullOrEmpty(extCampaignId))
            {
                return BadRequest(new { error = "External Campaign ID is required.", extCampaignId });
            }

            try
            {
                var campaign = await _campaignService.GetDraftCampaignByExtIdAsync(tenantId, extCampaignId);
                if (campaign == null)
                {
                    return NotFound(new { error = $"No Draft campaign found for ExtCampaignId '{extCampaignId}'.", tenantId, extCampaignId });
                }
                return Ok(campaign);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Draft campaign for ExtCampaignId {ExtCampaignId} for tenant {TenantId}", extCampaignId, tenantId);
                return StatusCode(500, new { error = "An unexpected error occurred while fetching the Draft campaign." });
            }
        }

    }
}
