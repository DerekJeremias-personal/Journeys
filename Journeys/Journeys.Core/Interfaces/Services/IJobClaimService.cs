using Journeys.Core.Models;

namespace Journeys.Core.Interfaces.Services;

public interface IJobClaimService
{
    /// <summary>
    /// Marks a job as Processing. Returns null if job cannot be processed (not found, not claimed by node, or wrong status).
    /// </summary>
    Task<BatchJob?> ProcessJobAsync(string tenantId, string batchFileId, string jobId, string nodeId);

    /// <summary>
    /// Marks a job as completed and releases the claim.
    /// </summary>
    Task<bool> CompleteJobAsync(string tenantId, string batchFileId, string jobId, string nodeId);

    /// <summary>
    /// Marks a job as failed and releases the claim.
    /// </summary>
    Task<bool> FailJobAsync(string tenantId, string batchFileId, string jobId, string nodeId, string? errorMessage = null);

    /// <summary>
    /// Claims multiple jobs atomically with node-level locking to prevent thread contention.
    /// Returns jobs with status "Claiming" that need to be processed.
    /// </summary>
    Task<List<BatchJob>> ClaimMultipleJobsAsync(
        string tenantId,
        string nodeId,
        int targetCount = 5,
        CancellationToken cancellationToken = default);
}

