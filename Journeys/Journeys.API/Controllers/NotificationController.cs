using Journeys.API.Models;
using Journeys.Core.Interfaces.Notifications;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.Core.Services;
using Journeys.Core.Utility;
using Journeys.DAL.Adapters;
using Journeys.DTO.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Journeys.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly ILogger<NotificationController> _logger;

        public NotificationController(INotificationService notificationService, ILogger<NotificationController> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }


        [HttpPost("{tenantId}/save")]
        public async Task<IActionResult> Upsert(string tenantId, [FromBody] NotificationConfigDto request)
        {
            var newConfig = await _notificationService.UpsertNotificationConfigAsync(tenantId, request);
            return Ok(newConfig);
        }

        [HttpDelete("{tenantId}/{configId}")]
        public async Task<IActionResult> Delete(string tenantId, string configId)
        {
            var result = await _notificationService.DeleteNotificationConfigAsync(tenantId, configId);
            return NoContent();
        }

        [HttpGet("{tenantId}/{status}")]
        public async Task<IActionResult> GetAll(string tenantId, string status = null)
        {
            var result = await _notificationService.GetNotificationConfigsAsync(tenantId, status);
            return Ok(result ?? []);
        }
    }

}
