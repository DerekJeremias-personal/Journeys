using Journeys.Core.Interfaces.Services;
using Journeys.DTO.Models.Analytics;
using Microsoft.AspNetCore.Mvc;

namespace Journeys.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsReportsController : ControllerBase
{
    private readonly IAnalyticsReportService _analyticsReportService;
    private readonly ILogger<AnalyticsReportsController> _logger;

    public AnalyticsReportsController(
        IAnalyticsReportService analyticsReportService,
        ILogger<AnalyticsReportsController> logger)
    {
        _analyticsReportService = analyticsReportService;
        _logger = logger;
    }

    [HttpPost("{tenantId}/{reportKey}")]
    public async Task<ActionResult<AnalyticsReportResult>> GetReportAsync(
        string tenantId,
        string reportKey,
        [FromBody] AnalyticsReportRequest? request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return BadRequest(new { error = "Tenant ID is required." });
        }

        if (string.IsNullOrWhiteSpace(reportKey))
        {
            return BadRequest(new { error = "Report key is required." });
        }

        try
        {
            var result = await _analyticsReportService.GetReportAsync(
                tenantId,
                reportKey,
                request,
                cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading analytics report {ReportKey} for tenant {TenantId}", reportKey, tenantId);
            return StatusCode(500, new { error = "Failed to load analytics report." });
        }
    }
}
