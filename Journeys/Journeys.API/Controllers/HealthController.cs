using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Journeys.DTO.Exceptions;
using Journeys.DTO.Responses;

namespace Journeys.API.Controllers
{
    [AllowAnonymous]
    [Route("[controller]")]
    [ApiController]
    public class HealthController(ILogger<HealthController> logger) : ControllerBase
    {
        private readonly ILogger<HealthController> _logger = logger;

        [HttpGet]
        public ActionResult<HealthStatusResponse> Get() => Ok(new HealthStatusResponse
        {
            Status = "healthy",
            Timestamp = DateTimeOffset.UtcNow,
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
        });

        [HttpGet("Echo")]
        public IActionResult EchoQuery(
            [FromQuery(Name = "code")] int statusCode,
            [FromQuery(Name = "tenantId")] string tenantId,
            [FromQuery(Name = "throwOnError")] bool throwOnError = false)
        {
            // 400–599 → either log+throw or log+return
            if (statusCode is >= 400 and < 600)
            {
                _logger.LogError(
                    "Simulated API error with status code {StatusCode} for tenant {TenantId}",
                    statusCode, tenantId);

                if (throwOnError)
                {
                    var errors = new Dictionary<string, string>
                    {
                        ["TenantId"] = tenantId,
                        ["StatusCode"] = statusCode.ToString()
                    };
                    throw new APIErrorsException(errors);
                }

                return StatusCode(statusCode);
            }

            // 200–299 → echo that status
            if (statusCode is >= 200 and < 300)
            {
                return StatusCode(statusCode);
            }

            // anything else → default 200 OK
            return StatusCode(200);
        }
    }
}
