using Journeys.Core.Interfaces.Services;
using Journeys.DTO.Requests;
using MassTransit;
using Microsoft.AspNetCore.Mvc;

namespace Journeys.API.Controllers
{
    [Controller()]
    [Route("api/[controller]")]
    public class JobQueueController : ControllerBase
    {
        private readonly ILogger<JobQueueController> _logger;
        private readonly IIngestService _ingestService;

        public JobQueueController(IIngestService ingestService, ILogger<JobQueueController> logger)
        {
            _logger = logger;
            _ingestService = ingestService;
        }


        [HttpPost]
        [Route("{tenantId}/enqueue")]
        public async Task<IActionResult> Enqueue(string tenantId, [FromBody] EnqueueRequest? request)
        {
            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(request?.JobId))
            {
                _logger.LogInformation("JobQueueController::Enqueue - TenantId: {0}, JobId: {1}", tenantId, request?.JobId);
                return Ok();
            }

            await _ingestService.ReprocessBatchJob(tenantId, request.BatchFileId, request.JobId);

            _logger.LogInformation("JobQueueController::Enqueue - Successfully enqueued messade: TenantId: {0}, JobId: {1}", tenantId, request?.JobId);
            return Ok();
        }
    }

}
