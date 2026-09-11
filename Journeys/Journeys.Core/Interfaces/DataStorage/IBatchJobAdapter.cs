using Journeys.Core.Models;

namespace Journeys.Core.Interfaces.DataStorage;

public interface IBatchJobAdapter
{
    /// <summary>
    /// Fetches a batch job by ID. Requires BatchFileId as partition key.
    /// </summary>
    Task<BatchJob?> FetchBatchJobAsync(string tenantId, string batchFileId, string batchJobId);

    Task<PagedResultSet<BatchJob>?> FetchBatchFileJobsAsync(string tenantId, string batchFileId, int pageSize = 1000, string continuationToken = null);


    /// <summary>
    /// Upserts a batch job. ETag is automatically validated by the storage REST API for optimistic concurrency.
    /// </summary>
    Task<BatchJob> UpsertBatchJobAsync(BatchJob batchJob);

    /// <summary>
    /// Gets the completion status of all chunks for a parent batch job.
    /// </summary>
    Task<ChunkCompletionStatus> GetChunkCompletionStatusAsync(
        string tenantId,
        string parentBatchJobId);

    /// <summary>
    /// Attempts to claim multiple jobs atomically using ETag-based optimistic concurrency.
    /// Returns jobs that were successfully claimed (status updated to "Claiming").
    /// </summary>
    Task<List<BatchJob>> TryClaimMultipleJobsAsync(
        string tenantId,
        string nodeId,
        int targetCount = 5,
        int maxCandidates = 20,
        int claimTimeoutSeconds = 60);

    Task<bool> DeleteBatchJobAsync(string tenantId, string batchJobId, string batchFileId);

}