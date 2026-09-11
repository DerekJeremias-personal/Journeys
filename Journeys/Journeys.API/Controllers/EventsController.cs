
using Journeys.Core.Caching;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.Services;
using Journeys.Core.Utility;
using Journeys.DTO.Exceptions;
using Journeys.DTO.Models;
using Journeys.Infra.Backend;
using Journeys.DTO.Models.RulesEngine;
using Journeys.DTO.Requests;
using Journeys.DTO.Responses;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Journeys.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EventsController : ControllerBase
    {
        private readonly IEventService _eventService;
        private readonly IDynamicDataAdapter _dynamicDataAdapter;
        private readonly ILogger<EventsController> _logger;
        private readonly IExportService _exportService;
        private readonly ModelCache _modelCache;

        public EventsController(
            IEventService eventService, 
            IDynamicDataAdapter dynamicDataAdapter,
            ILogger<EventsController> logger,
            IExportService exportService,
            ModelCache cache)
        {
            _eventService = eventService;
            _dynamicDataAdapter = dynamicDataAdapter;
            _logger = logger;
            _exportService = exportService;
            _modelCache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        [HttpPost("{tenantId}/{modelName}/export")]
        public async Task<IActionResult> Export(string tenantId, string modelName, [FromBody] ExportByQueryRequest request, CancellationToken cancellation = default(CancellationToken))
        {
            try
            {
                var allEntities = new List<EventPayloadResponseDto>();
                string continuationToken = null;
                const int batchSize = 1000;

                do
                {
                    var data = await _eventService.QueryAsync(
                        tenantId, 
                        modelName, 
                        request.QueryString, 
                        request.Parameters, 
                        "",
                        SortOrder.ASC,
                        batchSize, 
                        continuationToken, 
                        cancellation,
                        false);

                    if (data.Entities != null)
                    {
                        allEntities.AddRange(data.Entities);
                    }
                    
                    continuationToken = data.ContinuationToken;
                } while (!string.IsNullOrEmpty(continuationToken) && !cancellation.IsCancellationRequested);

                _logger.LogInformation("Retrieved {Count} total entities for export", allEntities.Count);

                var csvContent = _exportService.GenerateCsv(allEntities, request);
                var bytes = Encoding.UTF8.GetBytes(csvContent);
                var stream = new MemoryStream(bytes);
                
                return File(stream, "text/csv", $"{modelName}_export.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting data for model {Model}", modelName);
                return BadRequest(new { error = ex.Message });
            }
        }

        private object GetNestedPropertyValue(dynamic obj, string path)
        {
            try
            {
                var properties = path.Split('.');
                object value = obj;

                foreach (var prop in properties)
                {
                    if (value == null) return null;
                    var dict = value as IDictionary<string, object>;
                    if (dict == null) return null;
                    if (!dict.TryGetValue(prop, out value))
                        return null;
                }

                return value;
            }
            catch
            {
                return null;
            }
        }

        [HttpPost("{tenantId}/{modelName}/admin/query")]
        public async Task<IActionResult> AdminQuery(string tenantId, string modelName, [FromBody] GetByQueryRequest request, CancellationToken cancellation = default)
        {
            try
            {
                if (!string.IsNullOrEmpty(request.LoyaltyAccountId))
                {
                    if (string.IsNullOrEmpty(request.Query))
                    {
                        request.Query = "c.accountid = @LoyaltyAccountId";
                        request.Parameters = new Dictionary<string, object> { { "@LoyaltyAccountId", request.LoyaltyAccountId } };
                    }
                    else
                    {
                        request.Query += " AND c.accountid = @LoyaltyAccountId";
                        if (request.Parameters == null)
                        {
                            request.Parameters = new Dictionary<string, object>();
                        }
                        request.Parameters["@LoyaltyAccountId"] = request.LoyaltyAccountId;
                    }
                    if (request.SortBy == null)
                    {
                        request.SortBy = "";
                    }
                }
                else
                {
                    request.Query = request.Query ?? "";
                    request.Parameters = request.Parameters ?? new Dictionary<string, object>();
                    request.SortBy = request.SortBy ?? "";
                }

                if (!string.IsNullOrWhiteSpace(request.Query))
                {
                    request.Query += " AND (NOT IS_DEFINED(c.db_status) OR c.db_status != 'deleted')";
                }
                else
                {
                    request.Query = "(NOT IS_DEFINED(c.db_status) OR c.db_status != 'deleted')";
                }

                var response = await _eventService.QueryAsync(tenantId, modelName, request.Query, request.Parameters, request.SortBy, request.SortOrder, request.PageSize, request.ContinuationToken, cancellation, false);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
        }

        [HttpPost("{tenantId}/{modelName}/query")]
        public async Task<IActionResult> Query(string tenantId, string modelName, [FromBody] GetByQueryRequest request, CancellationToken cancellation = default(CancellationToken))
        {
            try
            {
                if (string.IsNullOrEmpty(request.LoyaltyAccountId))
                {
                    return BadRequest("LoyaltyAccountId is required");
                }
                //TODO: Ensure the user account executing the call has access to the requested LoyaltyAccountId

                var model = await _modelCache.GetOrLoadByName(tenantId, modelName);
                if (ModelUtility.IsLoyaltyAccount(model))
                {
                    var accountDetails = await _eventService.GetAccountEntity(tenantId, modelName, request.LoyaltyAccountId, cancellation, true);
                    var response = new PagedResultSetResponse<EventPayloadResponseDto>
                    {
                        Entities = new List<EventPayloadResponseDto> { accountDetails },
                        ContinuationToken = null,
                        Count = 1
                    };
                    return Ok(response);
                }
                else
                {
                    if (string.IsNullOrEmpty(request.Query))
                    {
                        request.Query = "c.accountid = @LoyaltyAccountId";
                        request.Parameters = new Dictionary<string, object> { { "@LoyaltyAccountId", request.LoyaltyAccountId } };
                    }
                    else
                    {
                        request.Query += " AND c.accountid = @LoyaltyAccountId";
                        if (request.Parameters == null)
                        {
                            request.Parameters = new Dictionary<string, object>();
                        }
                        request.Parameters.Add("@LoyaltyAccountId", request.LoyaltyAccountId);
                    }

                    // Ensure SortBy is not null or empty
                    if (request.SortBy == null)
                    {
                        request.SortBy = "";
                    }
                    
                    var response = await _eventService.QueryAsync(tenantId, modelName, request.Query, request.Parameters, request.SortBy, request.SortOrder, request.PageSize, request.ContinuationToken, cancellation, false);
                    return Ok(response);
                }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex);
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{tenantId}/{modelName}/process_bulk")]
        [RequestSizeLimit(524288000)] // 500MB in bytes
        public async Task<IActionResult> ProcessBulk(string tenantId, string modelName, [FromForm] IFormFile file, CancellationToken cancellationToken)
        {
            try
            {
                if (!modelName.Equals("order", StringComparison.OrdinalIgnoreCase))
                {
                    throw new NotImplementedException("Only the Order model is supported for bulk processing at this time.");
                }
            }
            catch (NotImplementedException nie)
            {
                _logger.LogError(nie);
            }

            //UNIQUEKEY,DISTRIBUTOR_CODE,DISTRIBUTOR,SFOQ_ID,DIST_DEALERID,DEALER_NAME,INVOICE_NBR,INVOICE_DATE,ITEM_NBR,ITEM_QUANTITY,VPL_PRICE,Business Desc,Family Frocast Code and Desc,Load Date
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file was uploaded or the file is empty.");
            }

            try
            {
                HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>()?.DisableBuffering();
                var outputStream = Response.Body;

                // Open the inbound file stream
                using var inputStream = file.OpenReadStream();

                // Set up the response for streaming
                Response.ContentType = "application/octet-stream";
                Response.Headers.Append("Content-Disposition", "attachment; filename=ProcessedFile.txt");
                await _eventService.ProcessBulk(tenantId, modelName, inputStream, outputStream, cancellationToken);

                return new EmptyResult(); // The response is already written to the stream
            }
            catch (NotImplementedException nie)
            {
                _logger.LogError(nie, "Bulk processing not implemented for model {ModelName}", modelName);
                throw;
            }
            catch (OperationCanceledException canEx)
            {
                _logger.LogError(canEx);
                return new EmptyResult(); // Handle cancellation gracefully
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing the file for tenant {TenantId}", tenantId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing the file.");
            }
        }


        [HttpPost("{tenantId}/{modelName}/process")]
        public async Task<IActionResult> process(string tenantId, string modelName, [FromBody] JsonElement payload, [FromQuery] string? campaignId, CancellationToken token)
        {
            try
            {
                var response = await _eventService.ProcessEventAsync(tenantId, modelName, payload, token, reprocessEvent: false, campaignId: campaignId);
                return Ok(response);
            }
            catch (APIErrorsException ex)
            {
                _logger.LogError(ex, "API errors occurred while processing event for tenant {TenantId} and model {ModelName}", tenantId, modelName);
                var errResponse = new EventPayloadResponseDto
                {
                    TenantId = tenantId,
                    LoyaltyAccountId = ex.AccountId,
                    Event = payload,
                    Errors = ex.Errors
                };
                if (ex.Errors.ContainsKey("draftTestingNotPermitted"))
                    return StatusCode(StatusCodes.Status403Forbidden, errResponse);

                return BadRequest(errResponse);
            }
            catch (BackendValidationException ex)
            {
                _logger.LogError(ex, "Backend validation failed while processing event for tenant {TenantId} and model {ModelName}", tenantId, modelName);
                return BadRequest(new EventPayloadResponseDto
                {
                    TenantId = tenantId,
                    Event = payload,
                    Errors = ex.ValidationErrors
                });
            }
            catch (Exception ex)
            {
                //TODO: Make better;
                var content = new Dictionary<string, string?>
                {
                    { "Exception.Message", ex?.Message },
                    { "Exception.InnerException", ex?.InnerException?.Message },
                    { "Exception.StackTrace", ex?.StackTrace }
                };
                _logger.LogError(ex);
                return BadRequest(content);
            }
        }

        [HttpPost("{tenantId}/{modelName}/admin/reconcile")]
        public async Task<ActionResult<ReconcileAccountResponse>> ReconcileAccount(string tenantId, string modelName, [FromBody] ReconcileAccountRequest request)
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                return BadRequest("Invalid tenant ID.");
            }

            if (string.IsNullOrEmpty(modelName))
            {
                return BadRequest("Invalid model.");
            }

            var reconTask = await _eventService.ReconcileLoyaltyAccountEventsAsync(tenantId, modelName, request);
            //var auditTask = _adminAuditService.AuditOperation(HttpContext.Request, tenantId, request, actionPrettyPrint, true);

            //await Task.WhenAll(savedLedgerTask, auditTask);

            var response = reconTask;

            return Ok(response);
        }

        [HttpPost("{tenantId}/admin/resettle")]
        public async Task<ActionResult<LoyaltyAccountDto>> ResettleAccount(string tenantId, [FromBody] ResettleAccountRequest request)
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                return BadRequest("Invalid tenant ID.");
            }

            if (request == null || string.IsNullOrEmpty(request.AccountId))
            {
                return BadRequest("Invalid account id.");
            }

            var res = await _eventService.ResettleAccountAsync(tenantId, request.AccountId);

            return Ok(res);
        }

        [HttpPost("{tenantId}/admin/resettlebyxid")]
        public async Task<ActionResult<LoyaltyAccountDto>> ResettleAccountByXid(string tenantId, [FromBody] ResettleAccountByXidRequest request)
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                return BadRequest("Invalid tenant ID.");
            }

            if (request == null || string.IsNullOrEmpty(request.ExtRefId))
            {
                return BadRequest("Invalid external id.");
            }

            var res = await _eventService.ResettleAccountByXidAsync(tenantId, request.ExtRefId);

            return Ok(res);
        }

        [HttpPost("{tenantId}/{modelName}/admin/dynamic/save")]
        public async Task<ActionResult<LoyaltyAccountDto>> SaveDynamicObject(string tenantId, string modelName, [FromBody] EventPayloadResponseDto request)
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                return BadRequest("Invalid tenant ID.");
            }

            if (request == null || string.IsNullOrEmpty(request.EventNaturalKey))
            {
                return BadRequest("Invalid external id.");
            }

            var res = await _eventService.SaveEventPayload(tenantId, modelName, request);

            return Ok(res);
        }
    }
}
