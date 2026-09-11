namespace Journeys.Core.Utility;

public sealed class PendingEventModelSpecDto
{
    public int SchemaVersion { get; set; } = 1;
    public string Name { get; set; } = string.Empty;
    public string? ModelType { get; set; }
    public string Source { get; set; } = "user-stated";
    public string? AttributesSummary { get; set; }
    public string? RawUserExcerpt { get; set; }
}
