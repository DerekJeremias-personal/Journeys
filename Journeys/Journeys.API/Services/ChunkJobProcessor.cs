using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace Journeys.API.Services;

public class ChunkJobProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChunkJobProcessor> _logger;
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly string _nodeId;
    private readonly int _concurrentJobs;
    private readonly int _workerCount;
    private readonly int _claimRetryDelayMs;
    private readonly List<string> _tenantIds;

    public ChunkJobProcessor(
        IServiceScopeFactory serviceScopeFactory,
        IConfiguration configuration,
        ILogger<ChunkJobProcessor> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _configuration = configuration;
        _logger = logger;

        _concurrentJobs = _configuration.GetValue<int>("ChunkProcessing:ConcurrentJobsPerNode", 20);
        _workerCount = _configuration.GetValue<int>("ChunkProcessing:WorkerCount", 10);
        _claimRetryDelayMs = _configuration.GetValue<int>("ChunkProcessing:ClaimRetryDelayMs", 10000);

        // Load tenant IDs from configuration
        var tenantIdsSection = _configuration.GetSection("ChunkProcessing:TenantIds");
        _tenantIds = tenantIdsSection.Get<List<string>>() ?? new List<string>();

        // Validate tenant IDs
        if (_tenantIds == null || _tenantIds.Count == 0)
        {
            _logger.LogWarning(
                "ChunkJobProcessor: No tenant IDs configured. Processor will start but won't process any jobs. " +
                "Configure 'ChunkProcessing:TenantIds' in appsettings.json");
        }
        else
        {
            // Remove null/empty tenant IDs and log warnings
            var invalidTenants = _tenantIds.Where(t => string.IsNullOrWhiteSpace(t)).ToList();
            if (invalidTenants.Count > 0)
            {
                _logger.LogWarning(
                    "ChunkJobProcessor: Found {Count} invalid (null/empty) tenant IDs in configuration. They will be ignored.",
                    invalidTenants.Count);
            }

            _tenantIds = _tenantIds.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToList();

            _logger.LogInformation(
                "ChunkJobProcessor: Configured to process jobs for {TenantCount} tenant(s): {TenantIds}",
                _tenantIds.Count,
                string.Join(", ", _tenantIds));
        }

        _concurrencyLimiter = new SemaphoreSlim(_concurrentJobs, _concurrentJobs);
        _nodeId = GetNodeId();

        _logger.LogInformation(
            "ChunkJobProcessor initialized: NodeId={NodeId}, ConcurrentJobs={ConcurrentJobs}, WorkerCount={WorkerCount}, TenantCount={TenantCount}",
            _nodeId,
            _concurrentJobs,
            _workerCount,
            _tenantIds.Count);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Validate tenant IDs before starting
        if (_tenantIds == null || _tenantIds.Count == 0)
        {
            _logger.LogError(
                "ChunkJobProcessor cannot start: No tenant IDs configured. " +
                "Configure 'ChunkProcessing:TenantIds' in appsettings.json with at least one tenant ID.");
            return;
        }

        _logger.LogInformation(
            "ChunkJobProcessor started on node {NodeId} processing jobs for {TenantCount} tenant(s)",
            _nodeId,
            _tenantIds.Count);

        var tasks = new List<Task>();

        // Create fixed worker pool
        for (int i = 0; i < _workerCount; i++)
        {
            var workerId = i;
            var guid = Guid.NewGuid();
            var random = (Math.Abs(guid.GetHashCode()) % 100) + 1;  // 1-100
            await Task.Delay((100*i)+random);
            tasks.Add(ProcessJobsWorkerAsync(workerId, stoppingToken));
        }

        await Task.WhenAll(tasks);

        _logger.LogInformation("ChunkJobProcessor stopped on node {NodeId}", _nodeId);
    }

    private async Task ProcessJobsWorkerAsync(int workerId, CancellationToken stoppingToken)
    {
        _logger.LogDebug("Worker {WorkerId} started", workerId);

        // Each worker maintains its own round-robin state
        int lastTenantIndex = -1;

        while (!stoppingToken.IsCancellationRequested)
        {
            // Create a scope for this iteration to resolve scoped services
            using var scope = _serviceScopeFactory.CreateScope();
            var jobClaimService = scope.ServiceProvider.GetRequiredService<IJobClaimService>();
            var ingestService = scope.ServiceProvider.GetRequiredService<IIngestService>();

            try
            {
                // Round-robin through configured tenants to find jobs
                List<BatchJob> claimedJobs = new List<BatchJob>();
                int queriesMade = 0;

                // Try each tenant in round-robin fashion
                for (int i = 0; i < _tenantIds.Count; i++)
                {
                    // Advance to next tenant (round-robin)
                    lastTenantIndex = (lastTenantIndex + 1) % _tenantIds.Count;
                    var tenantId = _tenantIds[lastTenantIndex];
                    queriesMade++;

                    _logger.LogDebug(
                        "Worker {WorkerId} querying tenant {TenantId} for available jobs (round-robin position {Index}/{Total})",
                        workerId,
                        tenantId,
                        lastTenantIndex + 1,
                        _tenantIds.Count);

                    // Try to claim multiple jobs from this tenant
                    var jobs = await jobClaimService.ClaimMultipleJobsAsync(tenantId, _nodeId, targetCount: 5, stoppingToken);

                    if (jobs != null && jobs.Count > 0)
                    {
                        _logger.LogDebug(
                            "Worker {WorkerId} claimed {Count} job(s) from tenant {TenantId}",
                            workerId,
                            jobs.Count,
                            tenantId);
                        claimedJobs.AddRange(jobs);
                        break; // Found jobs, exit round-robin loop
                    }
                }

                if (claimedJobs.Count > 0)
                {
                    // Process all claimed jobs (each will take a semaphore slot when processing)
                    var processingTasks = claimedJobs.Select(job => 
                        ProcessChunkJobAsync(job, jobClaimService, ingestService, stoppingToken));
                    await Task.WhenAll(processingTasks);
                }
                else
                {
                    // No jobs found in any tenant after checking all of them
                    if (queriesMade > 0)
                    {
                        _logger.LogDebug(
                            "Worker {WorkerId} found no jobs in any of {TenantCount} tenant(s) after {QueryCount} queries",
                            workerId,
                            _tenantIds.Count,
                            queriesMade);
                    }

                    // Wait before retry
                    await Task.Delay(_claimRetryDelayMs, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker {WorkerId} error", workerId);
                // Wait a bit before retrying after error
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogDebug("Worker {WorkerId} stopped", workerId);
    }

    private async Task ProcessChunkJobAsync(
        BatchJob job,
        IJobClaimService jobClaimService,
        IIngestService ingestService,
        CancellationToken cancellationToken)
    {
        // Wait for available semaphore slot before processing (each job needs its own slot)
        await _concurrencyLimiter.WaitAsync(cancellationToken);
        try
        {
            var startTime = DateTimeOffset.UtcNow;

            // Update job from "Claiming" to "Processing"
            var processingJob = await jobClaimService.ProcessJobAsync(
                job.TenantId,
                job.BatchFileId,
                job.Id,
                _nodeId);

            if (processingJob == null)
            {
                _logger.LogWarning(
                    "Failed to update job {JobId} to Processing status (may have been claimed by another node)",
                    job.Id);
                return;
            }

            _logger.LogInformation(
                "Processing chunk job {JobId} for tenant {TenantId}, batch file {BatchFileId}, chunk index {ChunkIndex}",
                processingJob.Id,
                processingJob.TenantId,
                processingJob.BatchFileId,
                processingJob.ChunkIndex);

            // Check if resuming from checkpoint
            var hasProgress = (processingJob.ProcessedLines.HasValue && processingJob.ProcessedLines > 0)
                           || (processingJob.ProcessedBytes.HasValue && processingJob.ProcessedBytes > 0);

            if (hasProgress)
            {
                _logger.LogInformation(
                    "Resuming job {JobId} from line {ProcessedLines}, bytes {ProcessedBytes}",
                    processingJob.Id,
                    processingJob.ProcessedLines ?? 0,
                    processingJob.ProcessedBytes ?? 0);
            }

            try
            {
                // Process chunk - IngestService.RunJobAsync already handles resume via ResumabilityService
                await ingestService.RunJobAsync(processingJob);

                // Mark as complete
                var completed = await jobClaimService.CompleteJobAsync(
                    processingJob.TenantId,
                    processingJob.BatchFileId,
                    processingJob.Id,
                    _nodeId);

                if (completed)
                {
                    var duration = DateTimeOffset.UtcNow - startTime;
                    _logger.LogInformation(
                        "Successfully completed chunk job {JobId} in {Duration}ms",
                        processingJob.Id,
                        duration.TotalMilliseconds);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to mark job {JobId} as complete (may have been claimed by another node)",
                        processingJob.Id);
                }
            }
            catch (Exception ex)
            {
                var jobId = processingJob.Id ?? "unknown";
                _logger.LogError(
                    ex,
                    "Error processing chunk job {JobId} for tenant {TenantId}, batch file {BatchFileId}",
                    jobId,
                    processingJob.TenantId,
                    processingJob.BatchFileId);

                // Mark as failed (only if we have a valid job ID)
                if (!string.IsNullOrWhiteSpace(processingJob.Id))
                {
                    await jobClaimService.FailJobAsync(
                        processingJob.TenantId,
                        processingJob.BatchFileId,
                        processingJob.Id,
                        _nodeId,
                        ex.Message);
                }

                throw; // Re-throw to let worker handle retry logic if needed
            }
        }
        finally
        {
            // Always release semaphore slot
            _concurrencyLimiter.Release();
        }
    }

    private string GetNodeId()
    {
        return Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID")
               ?? Environment.GetEnvironmentVariable("CONTAINER_APP_REPLICA_NAME")
               ?? Environment.MachineName
               ?? $"node-{Guid.NewGuid()}";
    }
}

