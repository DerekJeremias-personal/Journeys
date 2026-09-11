namespace Journeys.DTO.Models.Analytics;

public class AnalyticsReportColumn
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? Format { get; set; }
}
