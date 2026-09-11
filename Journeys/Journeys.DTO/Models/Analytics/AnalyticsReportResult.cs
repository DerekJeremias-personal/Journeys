namespace Journeys.DTO.Models.Analytics;

public class AnalyticsReportResult
{
    public string ReportKey { get; set; } = string.Empty;
    public List<AnalyticsReportColumn> Columns { get; set; } = new();
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public Dictionary<string, object?> Summary { get; set; } = new();
    public int RowCount { get; set; }
    public bool Truncated { get; set; }
    public string? DataAsOf { get; set; }
    public string Timezone { get; set; } = "UTC";
    public List<string> Warnings { get; set; } = new();
}
