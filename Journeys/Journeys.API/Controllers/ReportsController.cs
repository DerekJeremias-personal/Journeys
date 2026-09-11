using Journeys.Core.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Journeys.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [ApiKey]
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _reportService;

        public ReportsController(IReportService reportService)
        {
            _reportService = reportService;
        }

        [HttpGet("{tenantId}/daily-report")]
        public async Task<IActionResult> GetDailyReport(string tenantId )
        {
            try
            {
                var result = await _reportService.ProcessDailyReport(tenantId);
                return Ok(result);
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { error = "Failed to fetch loyalty report", message = ex.Message });
            }
        }
    }
}
