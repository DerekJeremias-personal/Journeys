namespace Journeys.DTO.Models.Analytics;

public class AnalyticsReportRequest
{
    public Dictionary<string, string?> Filters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int? Limit { get; set; }
}
