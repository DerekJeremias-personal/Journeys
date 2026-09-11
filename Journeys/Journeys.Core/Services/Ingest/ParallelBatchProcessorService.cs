using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Microsoft.Extensions.Options;
using Journeys.Core.Configuration;
using Serilog;
using System.Text.Json;

namespace Journeys.Core.Services.Ingest;

public class ParallelBatchProcessorService
{
    private readonly SemaphoreSlim _semaphore;
    private readonly IEventService _eventService;
    private readonly IBatchJobService _batchJobService;
    private readonly BatchProcessingOptions _options;

    public ParallelBatchProcessorService(
        IEventService eventService, 
        IBatchJobService batchJobService,
        IOptions<BatchProcessingOptions> options)
    {
        _eventService = eventService;
        _batchJobService = batchJobService;
        _options = options.Value;
        _semaphore = new SemaphoreSlim(_options.MaxConcurrency);
    }

    public async Task ProcessBatchJobsInParallelAsync(List<BatchJob> batchJobs, Func<JsonElement, Task> processGroup)
    {
        if (!_options.EnableParallelProcessing || batchJobs.Count <= 1)
        {
            // Process sequentially if parallel processing is disabled or only one job
            foreach (var job in batchJobs)
            {
                await ProcessSingleBatchJobAsync(job, processGroup);
            }
            return;
        }

        Log.Debug("Processing {JobCount} batch jobs in parallel with max concurrency {MaxConcurrency}", 
            batchJobs.Count, _options.MaxConcurrency);

        var tasks = batchJobs.Select(job => ProcessSingleBatchJobAsync(job, processGroup));
        await Task.WhenAll(tasks);

        Log.Debug("Completed parallel processing of {JobCount} batch jobs", batchJobs.Count);
    }

    private async Task ProcessSingleBatchJobAsync(BatchJob batchJob, Func<JsonElement, Task> processGroup)
    {
        await _semaphore.WaitAsync();
        try
        {
            Log.Debug("Starting processing of batch job {BatchJobId}", batchJob.Id);
            await _batchJobService.ProcessBatchJobWithIngestServiceAsync(batchJob, processGroup);
            Log.Debug("Completed processing of batch job {BatchJobId}", batchJob.Id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing batch job {BatchJobId}", batchJob.Id);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }
} 