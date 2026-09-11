namespace Journeys.Core.Configuration;

/// <summary>
/// Configuration options for ingest processing concurrency control.
/// </summary>
public class IngestOptions
{
    /// <summary>
    /// Maximum number of concurrent operations for general ingest processing.
    /// </summary>
    public int MaxConcurrency { get; set; } = 5;

    /// <summary>
    /// Whether to enable parallel processing for general ingest operations.
    /// </summary>
    public bool EnableParallelProcessing { get; set; } = true;

    /// <summary>
    /// Configuration for chunk processing operations.
    /// </summary>
    public ChunkProcessingOptions ChunkProcessing { get; set; } = new();

    /// <summary>
    /// Configuration for result aggregation operations.
    /// </summary>
    public ResultAggregationOptions ResultAggregation { get; set; } = new();
}

/// <summary>
/// Configuration options for chunk processing concurrency control.
/// </summary>
public class ChunkProcessingOptions
{
    /// <summary>
    /// Maximum number of concurrent chunk processing operations.
    /// </summary>
    public int MaxConcurrency { get; set; } = 3;

    /// <summary>
    /// Whether to enable parallel processing for chunk operations.
    /// </summary>
    public bool EnableParallelProcessing { get; set; } = true;
}

/// <summary>
/// Configuration options for result aggregation concurrency control.
/// </summary>
public class ResultAggregationOptions
{
    /// <summary>
    /// Maximum number of concurrent result file reading operations.
    /// </summary>
    public int MaxConcurrency { get; set; } = 2;

    /// <summary>
    /// Whether to enable parallel processing for result aggregation.
    /// </summary>
    public bool EnableParallelProcessing { get; set; } = true;
}
