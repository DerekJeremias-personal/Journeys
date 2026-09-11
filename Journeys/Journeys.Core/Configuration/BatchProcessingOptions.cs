namespace Journeys.Core.Configuration;

public class BatchProcessingOptions
{
    public long ChunkSizeBytes { get; set; } = 4096; // * 1024; // 1MB
    public int MaxConcurrency { get; set; } = Environment.ProcessorCount;
    public bool EnableChunking { get; set; } = true;
    public bool EnableParallelProcessing { get; set; } = true;
    public int WriteFrequency { get; set; } = 10; // Write every N items
    public bool EnableFrequentWrites { get; set; } = true;
    public bool FlushOnError { get; set; } = true;
    
    // MassTransit Job Consumer Settings
    public int ChunkJobConcurrencyLimit { get; set; } = 5; // Number of chunk jobs to process in parallel

    // ChunkJobProcessor Settings
    public TimeSpan OrphanDetectionTimeout { get; set; } = TimeSpan.FromMinutes(30);
    public int ClaimPageSize { get; set; } = 10;

    /// <summary>
    /// List of tenant IDs that this node should process jobs for.
    /// Jobs will be queried in round-robin fashion across these tenants.
    /// If empty or null, the processor will not start.
    /// </summary>
    public List<string> TenantIds { get; set; } = new List<string>();

}