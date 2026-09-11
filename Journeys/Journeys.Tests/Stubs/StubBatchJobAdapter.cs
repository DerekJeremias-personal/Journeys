using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;

namespace Journeys.Tests.Stubs;

public class StubBatchJobAdapter : IBatchJobAdapter
{
    public Task<BatchJob?> FetchBatchJobAsync(string tenantId, string batchFileId, string batchJobId)
    {
        return Task.FromResult<BatchJob?>(null);
    }

    public Task<PagedResultSet<BatchJob>?> FetchBatchFileJobsAsync(string tenantId, string batchFileId, int pageSize = 1000, string continuationToken = null)
    {
        return Task.FromResult((PagedResultSet<BatchJob>)null);
    }

    public Task<BatchJob> UpsertBatchJobAsync(BatchJob batchJob)
    {
        return Task.FromResult(batchJob);
    }

    public Task<ChunkCompletionStatus> GetChunkCompletionStatusAsync(string tenantId, string parentBatchJobId)
    {
        return Task.FromResult(new ChunkCompletionStatus
        {
            TotalChunks = 0,
            CompletedChunks = 0,
            PendingChunks = 0,
            ProcessingChunks = 0,
            FailedChunks = 0
        });
    }

    public Task<bool> DeleteBatchJobAsync(string tenantId, string batchJobId, string batchFileId)
    {
        return Task.FromResult(true);
    }

    public Task<List<BatchJob>> TryClaimMultipleJobsAsync(string tenantId, string nodeId, int targetCount = 5, int maxCandidates = 20, int claimTimeoutSeconds = 60)
    {
        throw new NotImplementedException();
    }
} 