using Journeys.DTO.Models.Analytics;

namespace Journeys.Core.Interfaces.Services;

public interface IAnalyticsReportService
{
    Task<AnalyticsReportResult> GetReportAsync(
        string tenantId,
        string reportKey,
        AnalyticsReportRequest? request,
        CancellationToken cancellationToken = default);
}
