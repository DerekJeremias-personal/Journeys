using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using Journeys.DTO.Requests;
using Azure;
using System.Net;

namespace Journeys.DAL.Adapters;

public class BatchJobAdapter(IDynamicDataAdapter dataAdapter) : IBatchJobAdapter
{
    private const string MODEL_ID = "5aaeb5ae-518f-4b29-8a9a-c77f0dbd747f";

    public async Task<BatchJob?> FetchBatchJobAsync(string tenantId, string batchFileId, string batchJobId)
    {
        // BatchFileId is the partition key (pk parameter)
        return await dataAdapter.GetEntityAsync<BatchJob>(tenantId, batchJobId, MODEL_ID, batchFileId);
    }

    public async Task<PagedResultSet<BatchJob>?> FetchBatchFileJobsAsync(string tenantId, string batchFileId, int pageSize = 1000, string continuationToken = null)
    {
        return await dataAdapter.GetEntitiesByPKAsync<BatchJob>(tenantId, batchFileId, MODEL_ID, pageSize, null, continuationToken);
    }

    public async Task<BatchJob> UpsertBatchJobAsync(BatchJob batchJob)
    {
        batchJob.ModelId = MODEL_ID;
        batchJob.Id ??= Guid.NewGuid().ToString();

        // SetEntityAsync will throw exception if ETag mismatch (HTTP 412)
        // The ETag from batchJob.ETag will be automatically validated by the REST API
        return await dataAdapter.SetEntityAsync(batchJob.TenantId, batchJob, MODEL_ID, typeof(BatchJob));
    }

    public async Task<ChunkCompletionStatus> GetChunkCompletionStatusAsync(
        string tenantId,
        string parentBatchJobId)
    {
        // Query all chunks for the parent job
        // Note: This query requires tenantId, so we need to query within tenant partition
        // If parentBatchJobId spans multiple tenants, we'd need cross-partition query
        var result = await dataAdapter.QueryEntitiesAsync<BatchJob>(
            tenantId,
            MODEL_ID,
            query: @"c.ParentBatchJobId = @parentBatchJobId AND c.IsChunk = true",
            parameters: new Dictionary<string, object>
            {
                { "@parentBatchJobId", parentBatchJobId }
            },
            sortBy: "ChunkIndex",
            sortOrder: SortOrder.ASC,
            pageSize: 10000 // Large page size to get all chunks
        );

        var chunks = result.Entities ?? new List<BatchJob>();
        var totalChunks = chunks.Count;

        var completedChunks = chunks.Count(c => c.Status == BatchJobStatusStrings.COMPLETE);
        var pendingChunks = chunks.Count(c => c.Status == BatchJobStatusStrings.PENDING);
        var processingChunks = chunks.Count(c => c.Status == BatchJobStatusStrings.PROCESSING);
        var failedChunks = chunks.Count(c => c.Status == BatchJobStatusStrings.FAILED);

        return new ChunkCompletionStatus
        {
            TotalChunks = totalChunks,
            CompletedChunks = completedChunks,
            PendingChunks = pendingChunks,
            ProcessingChunks = processingChunks,
            FailedChunks = failedChunks
        };
    }

    public async Task<List<BatchJob>> TryClaimMultipleJobsAsync(
        string tenantId,
        string nodeId,
        int targetCount = 5,
        int maxCandidates = 20,
        int claimTimeoutSeconds = 60)
    {
        var now = DateTimeOffset.UtcNow;
        var claimExpiration = now.AddSeconds(claimTimeoutSeconds);

        // Query for candidates (Pending or stale Claiming jobs)
        var result = await dataAdapter.QueryEntitiesAsync<BatchJob>(
            tenantId,
            MODEL_ID,
            query: @"(
                (c.status = @pendingStatus)
                OR
                (c.status = @claimingStatus AND c.claimexpiration < @now)
            ) AND (NOT IS_DEFINED(c.claimedby) OR c.claimedby = null OR c.claimedby = '' OR c.claimedby = @nodeId)",
            parameters: new Dictionary<string, object>
            {
                { "@pendingStatus", BatchJobStatusStrings.PENDING },
                { "@claimingStatus", BatchJobStatusStrings.CLAIMING },
                { "@now", now },
                { "@nodeId", nodeId }
            },
            sortBy: "c.createdate",
            sortOrder: SortOrder.ASC,
            pageSize: maxCandidates
        );

        if (result.Entities == null || result.Entities.Count == 0)
        {
            return new List<BatchJob>();
        }

        var claimedJobs = new List<BatchJob>();

        // Try to claim each candidate atomically
        foreach (var candidate in result.Entities)
        {
            if (claimedJobs.Count >= targetCount) break;

            try
            {
                // Re-fetch to get fresh ETag (critical for atomicity)
                var freshJob = await FetchBatchJobAsync(tenantId, candidate.BatchFileId, candidate.Id);
                
                if (freshJob == null) continue;
                
                // Validate it's still claimable
                var isClaimable = freshJob.Status == BatchJobStatusStrings.PENDING ||
                    (freshJob.Status == BatchJobStatusStrings.CLAIMING && 
                     (freshJob.ClaimExpiration == null || freshJob.ClaimExpiration < now));

                if (!isClaimable) continue;

                // Update to "Claiming" with claim metadata
                freshJob.Status = BatchJobStatusStrings.CLAIMING;
                freshJob.ClaimedBy = nodeId;
                freshJob.ClaimExpiration = claimExpiration;
                freshJob.LastUpdated = now;

                // Atomic update - throws 412 if ETag conflict
                var claimed = await UpsertBatchJobAsync(freshJob);
                claimedJobs.Add(claimed);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.PreconditionFailed)
            {
                // ETag conflict - someone else claimed it, try next candidate
                continue;
            }
            catch (Exception ex)
            {
                // Log but continue to next candidate (could be transient error)
                continue;
            }
        }

        return claimedJobs;
    }

    public async Task<bool> DeleteBatchJobAsync(string tenantId, string batchJobId, string batchFileId)
    {
        var pkDictionary = new Dictionary<string, string> { { "tenantId", tenantId }, { "batchfileid", batchFileId } };
        return await dataAdapter.RemoveEntityAsync(tenantId, batchJobId, MODEL_ID, pkDictionary);
    }
}
