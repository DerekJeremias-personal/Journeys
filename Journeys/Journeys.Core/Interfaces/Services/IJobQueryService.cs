using Journeys.Core.Models;

namespace Journeys.Core.Interfaces.Services;

public interface IJobQueryService
{
    /// <summary>
    /// Gets the completion status of all chunks for a parent batch job.
    /// </summary>
    Task<ChunkCompletionStatus> GetChunkCompletionStatusAsync(
        string tenantId,
        string parentBatchJobId);

    /// <summary>
    /// Checks if all chunks for a parent batch job are complete.
    /// </summary>
    Task<bool> AreAllChunksCompleteAsync(
        string tenantId,
        string parentBatchJobId);
}


