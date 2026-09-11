namespace Journeys.Core.Models;

public class ProcessChunkJobMessage
{
    public string TenantId { get; set; } = string.Empty;
    public string BatchJobId { get; set; } = string.Empty;

    /// <summary>
    /// BatchFileId (partition key) - required for efficient job claiming without cross-partition queries.
    /// </summary>
    public string BatchFileId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

