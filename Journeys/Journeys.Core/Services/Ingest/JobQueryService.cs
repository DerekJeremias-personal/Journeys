using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Microsoft.Extensions.Logging;

namespace Journeys.Core.Services.Ingest;

public class JobQueryService : IJobQueryService
{
    private readonly IBatchJobAdapter _batchJobAdapter;
    private readonly ILogger<JobQueryService> _logger;

    public JobQueryService(
        IBatchJobAdapter batchJobAdapter,
        ILogger<JobQueryService> logger)
    {
        _batchJobAdapter = batchJobAdapter;
        _logger = logger;
    }

    public async Task<ChunkCompletionStatus> GetChunkCompletionStatusAsync(
        string tenantId,
        string parentBatchJobId)
    {
        try
        {
            var status = await _batchJobAdapter.GetChunkCompletionStatusAsync(
                tenantId,
                parentBatchJobId);

            return status;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error getting chunk completion status for parent job {ParentJobId} in tenant {TenantId}",
                parentBatchJobId,
                tenantId);

            return new ChunkCompletionStatus
            {
                TotalChunks = 0,
                CompletedChunks = 0,
                PendingChunks = 0,
                ProcessingChunks = 0,
                FailedChunks = 0
            };
        }
    }

    public async Task<bool> AreAllChunksCompleteAsync(
        string tenantId,
        string parentBatchJobId)
    {
        var status = await GetChunkCompletionStatusAsync(tenantId, parentBatchJobId);
        return status.AreAllComplete;
    }
}


