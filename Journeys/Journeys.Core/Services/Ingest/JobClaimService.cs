using Journeys.Core.Configuration;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading;

namespace Journeys.Core.Services.Ingest;

public class JobClaimService : IJobClaimService
{
    private readonly IBatchJobAdapter _batchJobAdapter;
    private readonly BatchProcessingOptions _options;
    private readonly ILogger<JobClaimService> _logger;

    // Node-level lock: Only one thread per node can claim at a time
    private static readonly SemaphoreSlim _claimLock = new SemaphoreSlim(1, 1);

    private const int DEFAULT_TARGET_COUNT = 5;
    private const int DEFAULT_MAX_CANDIDATES = 20;
    private const int DEFAULT_CLAIM_TIMEOUT_SECONDS = 60;

    public JobClaimService(
        IBatchJobAdapter batchJobAdapter,
        IOptions<BatchProcessingOptions> options,
        ILogger<JobClaimService> logger)
    {
        _batchJobAdapter = batchJobAdapter;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<BatchJob?> ProcessJobAsync(
    string tenantId,
    string batchFileId,
    string jobId,
    string nodeId)
    {
        try
        {
            var job = await _batchJobAdapter.FetchBatchJobAsync(tenantId, batchFileId, jobId);
            if (job == null)
            {
                _logger.LogWarning(
                    "Job {JobId} not found when attempting to complete",
                    jobId);
                return null;
            }

            if (job.ClaimedBy != nodeId)
            {
                _logger.LogWarning(
                    "Job {JobId} is not claimed by node {NodeId}, claimed by {ClaimedBy}",
                    jobId,
                    nodeId,
                    job.ClaimedBy);
                return null;
            }

            // Handle both "Claiming" and "Pending" status (for backward compatibility)
            if (job.Status != BatchJobStatusStrings.CLAIMING && job.Status != BatchJobStatusStrings.PENDING)
            {
                _logger.LogWarning(
                    "Job {JobId} is in status {Status}, expected Claiming or Pending",
                    jobId,
                    job.Status);
                return null;
            }

            job.Status = BatchJobStatusStrings.PROCESSING;
            job.ClaimedBy = nodeId;
            job.LastUpdated = DateTimeOffset.UtcNow;

            var storedBatchJob = await _batchJobAdapter.UpsertBatchJobAsync(job);

            _logger.LogInformation(
                "Processing job {JobId} for tenant {TenantId}, batch file {BatchFileId}",
                jobId,
                tenantId,
                batchFileId);

            return storedBatchJob;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating job {JobId} to processing", jobId);
            return null;
        }
    }

    public async Task<bool> CompleteJobAsync(
        string tenantId,
        string batchFileId,
        string jobId,
        string nodeId)
    {
        try
        {
            var job = await _batchJobAdapter.FetchBatchJobAsync(tenantId, batchFileId, jobId);
            if (job == null)
            {
                _logger.LogWarning(
                    "Job {JobId} not found when attempting to complete",
                    jobId);
                return false;
            }

            if (job.ClaimedBy != nodeId)
            {
                _logger.LogWarning(
                    "Job {JobId} is not claimed by node {NodeId}, claimed by {ClaimedBy}",
                    jobId,
                    nodeId,
                    job.ClaimedBy);
                return false;
            }

            job.Status = BatchJobStatusStrings.COMPLETE;
            job.ClaimedBy = null;
            job.LastUpdated = DateTimeOffset.UtcNow;

            await _batchJobAdapter.UpsertBatchJobAsync(job);

            _logger.LogInformation(
                "Completed job {JobId} for tenant {TenantId}, batch file {BatchFileId}",
                jobId,
                tenantId,
                batchFileId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing job {JobId}", jobId);
            return false;
        }
    }

    public async Task<bool> FailJobAsync(
        string tenantId,
        string batchFileId,
        string jobId,
        string nodeId,
        string? errorMessage = null)
    {
        try
        {
            var job = await _batchJobAdapter.FetchBatchJobAsync(tenantId, batchFileId, jobId);
            if (job == null)
            {
                _logger.LogWarning(
                    "Job {JobId} not found when attempting to fail",
                    jobId);
                return false;
            }

            if (job.ClaimedBy != nodeId)
            {
                _logger.LogWarning(
                    "Job {JobId} is not claimed by node {NodeId}, claimed by {ClaimedBy}",
                    jobId,
                    nodeId,
                    job.ClaimedBy);
                return false;
            }

            job.Status = BatchJobStatusStrings.FAILED;
            job.ClaimedBy = null;
            job.LastUpdated = DateTimeOffset.UtcNow;

            await _batchJobAdapter.UpsertBatchJobAsync(job);

            _logger.LogError(
                "Failed job {JobId} for tenant {TenantId}, batch file {BatchFileId}. Error: {ErrorMessage}",
                jobId,
                tenantId,
                batchFileId,
                errorMessage ?? "Unknown error");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error failing job {JobId}", jobId);
            return false;
        }
    }

    public async Task<List<BatchJob>> ClaimMultipleJobsAsync(
        string tenantId,
        string nodeId,
        int targetCount = DEFAULT_TARGET_COUNT,
        CancellationToken cancellationToken = default)
    {
        // Node-level lock: Only one thread per node can claim at a time
        await _claimLock.WaitAsync(cancellationToken);
        try
        {
            var claimedJobs = await _batchJobAdapter.TryClaimMultipleJobsAsync(
                tenantId,
                nodeId,
                targetCount,
                DEFAULT_MAX_CANDIDATES,
                DEFAULT_CLAIM_TIMEOUT_SECONDS);

            if (claimedJobs.Count > 0)
            {
                _logger.LogInformation(
                    "Claimed {Count} jobs out of {Target} requested by node {NodeId} for tenant {TenantId}",
                    claimedJobs.Count,
                    targetCount,
                    nodeId,
                    tenantId);

                if (claimedJobs.Count < targetCount)
                {
                    _logger.LogWarning(
                        "Only claimed {Actual} of {Target} jobs - possible contention or insufficient jobs available",
                        claimedJobs.Count,
                        targetCount);
                }
            }
            else
            {
                _logger.LogDebug(
                    "No jobs available to claim for node {NodeId} and tenant {TenantId}",
                    nodeId,
                    tenantId);
            }

            return claimedJobs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error claiming multiple jobs for node {NodeId} and tenant {TenantId}", nodeId, tenantId);
            return new List<BatchJob>();
        }
        finally
        {
            _claimLock.Release();
        }
    }
}

