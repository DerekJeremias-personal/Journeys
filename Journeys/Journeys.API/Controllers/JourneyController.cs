using Journeys.Core;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Services;
using Journeys.Core.Utility;
using Journeys.DTO.Exceptions;
using Journeys.DTO.Models;
using Journeys.DTO.Requests;
using Journeys.DTO.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Text;
using System.Text.Json;

namespace Journeys.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JourneyController : ControllerBase
    {
        private readonly IRulesService _rulesService;
        private readonly ILogger<JourneyController> _logger;
        private readonly IAdminAuditService _adminAuditService;

        public JourneyController(IRulesService rulesService, IAdminAuditService adminAuditService, ILogger<JourneyController> logger)
        {
            _rulesService = rulesService;
            _logger = logger;
            _adminAuditService = adminAuditService ?? throw new ArgumentNullException(nameof(adminAuditService), "AdminAuditService cannot be null.");
        }

        [HttpGet("{tenantId}/ManuallyEnter/{campaignId}/Journey/{journeyId}/ForAccount/{loyaltyAccountXReference}")]
        public async Task<IActionResult> ManuallyEnterTier(string tenantId, string campaignId, string journeyId, string loyaltyAccountXReference, CancellationToken cancellationToken)
        {
            AdminAuditDto? auditEntry = null;
            try
            {
                var actionPrettyPrint = $"Manually Enter {journeyId} in Campaign {campaignId} for {loyaltyAccountXReference}";

                var accountTask = _rulesService.ManuallyEnterTier(tenantId, campaignId, journeyId, loyaltyAccountXReference, cancellationToken);
                var auditTask = _adminAuditService.AuditOperation(HttpContext.Request, tenantId, new { tenantId = tenantId, campaignId = campaignId, journeyId = journeyId, loyaltyXref = loyaltyAccountXReference }, actionPrettyPrint, true);
                await Task.WhenAll(accountTask, auditTask.ContinueWith(async (dto) => auditEntry = await dto));

                var account = accountTask.Result;
                return Ok(account);
            }
            catch (APIErrorsException ex)
            {
                _logger.LogError(ex, "Error entering tier for account {LoyaltyAccountXReference} in journey {JourneyId} for campaign {CampaignId} in tenant {TenantId}", loyaltyAccountXReference, journeyId, campaignId, tenantId);
                if (auditEntry != null)
                {
                    await _adminAuditService.RollbackAudit(auditEntry);
                }
                return BadRequest(JsonUtility.Serialize(ex.Errors, _logger));
            }
        }

        [HttpGet("{tenantId}/ManuallyExit/{campaignId}/Journey/{journeyId}/ForAccount/{loyaltyAccountXReference}")]
        public async Task<IActionResult> ManuallyExitTier(string tenantId, string campaignId, string journeyId, string loyaltyAccountXReference, CancellationToken cancellationToken)
        {
            AdminAuditDto? auditEntry = null;
            try
            {
                var actionPrettyPrint = $"Manually Exit {journeyId} in Campaign {campaignId} for {loyaltyAccountXReference}";

                var accountTask = _rulesService.ManuallyExitTier(tenantId, campaignId, journeyId, loyaltyAccountXReference, cancellationToken);
                var auditTask = _adminAuditService.AuditOperation(HttpContext.Request, tenantId, new { tenantId = tenantId, campaignId = campaignId, journeyId = journeyId, loyaltyXref = loyaltyAccountXReference }, actionPrettyPrint, true);
                await Task.WhenAll(accountTask, auditTask.ContinueWith(async (dto) => auditEntry = await dto));
                auditEntry = auditTask.Result;
                var account = accountTask.Result;

                return Ok(account);
            }
            catch (APIErrorsException ex)
            {
                _logger.LogError(ex, "Error exiting tier for account {LoyaltyAccountXReference} in journey {JourneyId} for campaign {CampaignId} in tenant {TenantId}", loyaltyAccountXReference, journeyId, campaignId, tenantId);
                if (auditEntry != null)
                {
                    await _adminAuditService.RollbackAudit(auditEntry);
                }
                return BadRequest(JsonUtility.Serialize(ex.Errors, _logger));
            }
        }

        [HttpGet("{tenantId}/InitializeStateSampleFile")]
        public async Task<IActionResult> InitializeStateSampleFile(string tenantId)
        {
            try
            {
                var stream = new MemoryStream();
                var fileName = "SampleFile.ndjson";
                var contentType = "application/octet-stream";

                for (var i = 0; i < 10; i++)
                {
                    var sample = new InitStateEntry($"xref-{i}")
                    {
                        JourneyStates = new List<InitJourneyState>
                        {
                            new InitJourneyState("9bf375ef-8e7f-42c9-8274-b0b5dc4bd0d9", new List<string> { "Bronze", "Silver", "Gold" }),
                            new InitJourneyState("f00df00d-8e7f-42c9-8274-b0b5dc4bd0d9", new List<string> { "Ruthenium256", "Ruthenium271", "Ruthenium299", "Ruthenium334", "Ruthenium458", "Ruthenium524", "Ruthenium6" }),
                        },
                        PointStates = new List<InitPointState>
                        {
                            new InitPointState("a246f62f-a9d8-4fe0-bd65-9e9d7f5881a5", 166),   // dealer_spendable
                            new InitPointState("9c61de1a-460d-4172-8781-acc04c783880", 10000)  // dealer_tier_qualification
                        }
                    };
                    var sampleJson = JsonUtility.Serialize(sample, null);
                    var sampleBytes = System.Text.Encoding.UTF8.GetBytes(sampleJson + Environment.NewLine);
                    await stream.WriteAsync(sampleBytes, 0, sampleBytes.Length);
                }

                stream.Position = 0; // Reset the stream position to the beginning

                return File(stream, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating the sample file for tenant {TenantId}", tenantId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while generating the sample file.");
            }
        }

        [HttpPost("{tenantId}/InitializeState")]
        public async Task<IActionResult> InitializeState(string tenantId, [FromForm] IFormFile file, CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file was uploaded or the file is empty.");
            }

            Stream outputStream = null;
            try
            {
                HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>()?.DisableBuffering();
                outputStream = Response.Body;

                // Open the inbound file stream
                using var inputStream = file.OpenReadStream();

                // Set up the response for streaming
                Response.ContentType = "application/octet-stream";
                Response.Headers.Append("Content-Disposition", "attachment; filename=ProcessedFile.txt");
                await _rulesService.InitState(tenantId, inputStream, outputStream, cancellationToken);

                return new EmptyResult(); // The response is already written to the stream
            }
            catch (OperationCanceledException canEx)
            {
                try
                {
                    if (outputStream != null)
                        outputStream.Flush();
                }
                catch { } //Intentionally swallow the exception.  If we cannot flush, the stream is closed or errored.
                _logger.LogWarning(canEx, "Request was cancelled by the client for tenant {TenantId}", tenantId);
                return StatusCode(StatusCodes.Status499ClientClosedRequest, "Request was cancelled by the client.");
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, "Error processing the file for tenant {TenantId}", tenantId);
                try
                {
                    if (outputStream != null)
                        outputStream.Flush();
                }
                catch { } //Intentionally swallow the exception.  If we cannot flush, the stream is closed or errored.
                _logger.LogWarning(ex, "An error occurred while processing the file for tenant {TenantId}", tenantId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing the file.");
            }
        }

        [HttpPost("{tenantId}/PreviewTierMove")]
        public async Task<IActionResult> PreviewTierMove(string tenantId, [FromBody] MoveTierRequest request, CancellationToken cancellationToken)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest("Request body is required.");
                }

                var preview = await _rulesService.PreviewTierMoveAsync(tenantId, request, cancellationToken);
                return Ok(preview);
            }
            catch (APIErrorsException ex)
            {
                _logger.LogError(ex, "Error previewing tier move for account {LoyaltyAccountId} in tenant {TenantId}", 
                    request?.LoyaltyAccountId ?? request?.LoyaltyAccountXReference, tenantId);
                return BadRequest(JsonUtility.Serialize(ex.Errors, _logger));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error previewing tier move for tenant {TenantId}", tenantId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while previewing tier move.");
            }
        }

        [HttpPost("{tenantId}/MoveTier")]
        public async Task<IActionResult> MoveTier(string tenantId, [FromBody] MoveTierRequest request, CancellationToken cancellationToken)
        {
            AdminAuditDto? auditEntry = null;
            try
            {
                if (request == null)
                {
                    return BadRequest("Request body is required.");
                }

                var actionPrettyPrint = $"Move Tier to Campaign {request.TargetCampaignId}, Journey {request.TargetJourneyId} for account {request.LoyaltyAccountXReference ?? request.LoyaltyAccountId}";

                var moveTask = _rulesService.MoveTierAsync(tenantId, request, cancellationToken);
                var auditTask = _adminAuditService.AuditOperation(HttpContext.Request, tenantId, request, actionPrettyPrint, true);
                await Task.WhenAll(moveTask, auditTask.ContinueWith(async (dto) => auditEntry = await dto));

                var result = moveTask.Result;
                return Ok(result);
            }
            catch (APIErrorsException ex)
            {
                _logger.LogError(ex, "Error moving tier for account {LoyaltyAccountId} in tenant {TenantId}", 
                    request?.LoyaltyAccountId ?? request?.LoyaltyAccountXReference, tenantId);
                if (auditEntry != null)
                {
                    await _adminAuditService.RollbackAudit(auditEntry);
                }
                return BadRequest(JsonUtility.Serialize(ex.Errors, _logger));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error moving tier for tenant {TenantId}", tenantId);
                if (auditEntry != null)
                {
                    await _adminAuditService.RollbackAudit(auditEntry);
                }
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while moving tier.");
            }
        }
    }
}
