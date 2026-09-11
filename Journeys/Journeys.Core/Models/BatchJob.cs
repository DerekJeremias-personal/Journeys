using Azure;
using System.Text.Json.Serialization;

namespace Journeys.Core.Models;

public class BatchJob : TenantedModelBase
{
    [JsonConstructor]
    public BatchJob(string status, string batchFileId, string tenancy, string fileFriendlyName, string path, DateTimeOffset createDate, 
        DateTimeOffset lastUpdated, string tenantId, string? id, string claimedBy = null, string batchResultFileId = null, string? parentBatchJobId = null,
        decimal? chunkIndex = null, decimal? totalChunks = null, decimal? chunkStartLine = null, decimal? chunkEndLine = null, bool isChunk = false,
        decimal? processedBytes = null, decimal? processedLines = null, DateTimeOffset? lastProcessedAt = null, DateTimeOffset? claimExpiration = null, string etag = null, decimal? totalErrors = null) : base(tenantId, id)
    {
        Status = status;
        BatchFileId = batchFileId;
        Tenancy = tenancy;
        ClaimedBy = claimedBy;
        BatchResultFileId = batchResultFileId;
        CreateDate = createDate;
        LastUpdated = lastUpdated;
        FileFriendlyName = fileFriendlyName;
        Path = path;
        ParentBatchJobId = parentBatchJobId;
        ChunkIndex = chunkIndex;
        TotalChunks = totalChunks;
        ChunkStartLine = chunkStartLine;
        ChunkEndLine = chunkEndLine;
        IsChunk = isChunk;
        ProcessedBytes = processedBytes;
        ProcessedLines = processedLines;
        LastProcessedAt = lastProcessedAt;
        ClaimExpiration = claimExpiration;
        ETag = etag;
        TotalErrors = totalErrors;
    }

    [JsonIgnore]
    public string? ModelId { get; set; }

    public string FileFriendlyName { get; set; }

    public string Tenancy { get; set; }
    
    public string Path { get; set; }

    public string Status { get; set; } = BatchJobStatusStrings.PENDING;
    
    public string BatchFileId { get; set; }

    public string? ClaimedBy { get; set; }
    
    public string? BatchResultFileId { get; set; }

    public DateTimeOffset CreateDate { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;

    public string? ParentBatchJobId { get; set; }
    public decimal? ChunkIndex { get; set; } // 0-based chunk index
    public decimal? TotalChunks { get; set; }
    public decimal? TotalErrors { get; set; }

    public decimal? ChunkStartLine { get; set; }
    public decimal? ChunkEndLine { get; set; }
    public bool IsChunk { get; set; } = false;

    public bool ReprocessEvent { get; set; } = false;

    public decimal? ProcessedBytes { get; set; }
    public decimal? ProcessedLines { get; set; }
    public DateTimeOffset? LastProcessedAt { get; set; }

    /// <summary>
    /// Expiration time for claim lock. Used to detect stale claims when a node crashes.
    /// </summary>
    public DateTimeOffset? ClaimExpiration { get; set; }


    /// <summary>
    /// ETag for optimistic concurrency control.
    /// Automatically managed by the storage REST API.
    /// </summary>
    //[JsonPropertyName("_etag")]
    //public string? ETag { get; set; }


}
