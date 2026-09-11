using Azure;
using System.IO;
using Journeys.Core.Caching;
using Journeys.Core.Configuration;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.FileStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.DTO.Exceptions;
using Journeys.DTO.Models;
using Journeys.DTO.Models.RulesEngine;
using Journeys.DTO.Requests;
using Journeys.DTO.Responses;
using Journeys.DTO.Responses.BlobResponses;
using Microsoft.Extensions.Options;
using Serilog;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Journeys.Core.Services.Ingest;

public class IngestService : IIngestService
{
    private readonly IDropboxConfigCache _dropboxConfigCache;
    private readonly IDataLakeAdapter _dataLakeAdapter;
    private readonly IBatchFileService _batchFileService;
    private readonly IBatchJobService _batchJobService;
    private readonly IBatchResultFileService _batchResultFileService;
    private readonly IEventService _eventService;
    private readonly FileChunkingService _fileChunkingService;
    private readonly ParallelBatchProcessorService _parallelBatchProcessor;
    private readonly DynamicServiceInvoker _dynamicServiceInvoker;
    private readonly ResponseHandlerService _responseHandlerService;
    private readonly ResumabilityService _resumabilityService;
    private readonly IOptions<BatchProcessingOptions> _options;
    private readonly IOptions<IngestOptions> _ingestOptions;

    public IngestService(
        IDropboxConfigCache dropboxConfigCache,
        IDataLakeAdapter dataLakeAdapter,
        IBatchFileService batchFileService,
        IBatchJobService batchJobService,
        IBatchResultFileService batchResultFileService,
        IEventService eventService,
        FileChunkingService fileChunkingService,
        ParallelBatchProcessorService parallelBatchProcessor,
        DynamicServiceInvoker dynamicServiceInvoker,
        ResponseHandlerService responseHandlerService,
        ResumabilityService resumabilityService,
        IOptions<BatchProcessingOptions> options,
        IOptions<IngestOptions> ingestOptions)
    {
        _dropboxConfigCache = dropboxConfigCache;
        _dataLakeAdapter = dataLakeAdapter;
        _batchFileService = batchFileService;
        _batchJobService = batchJobService;
        _batchResultFileService = batchResultFileService;
        _eventService = eventService;
        _fileChunkingService = fileChunkingService;
        _parallelBatchProcessor = parallelBatchProcessor;
        _dynamicServiceInvoker = dynamicServiceInvoker;
        _responseHandlerService = responseHandlerService;
        _responseHandlerService = responseHandlerService;
        _resumabilityService = resumabilityService;
        _options = options;
        _ingestOptions = ingestOptions;
    }

    public async Task<BatchJob> GetJobAsync(string tenantId, string batchFileId, string batchJobId)
    {
        if (string.IsNullOrEmpty(batchJobId)) throw new InvalidDataException($"IngestService::GetJobAsync - No batchJobId provided");
        if (string.IsNullOrEmpty(batchFileId)) throw new InvalidDataException($"IngestService::GetJobAsync - No batchFileId provided");

        var batchJob = await _batchJobService.GetJobAsync(tenantId, batchFileId, batchJobId);
        if (batchJob == null)
        {
            Log.Information($"IngestService::GetJobAsync -  No job found for TenantId: {tenantId}, batch file id: {batchFileId}, job id: {batchJobId}");
        }
        return batchJob;
    }

    public async Task ReprocessBatchJob(string tenantId, string batchFileId, string batchJobId)
    {
        var job = await _batchJobService.GetJobAsync(tenantId, batchFileId, batchJobId);
        if (job == null)
        {
            Log.Debug($"IngestService::ReprocessBatchJob - No job found for id: {batchJobId}");
            return;
        }
        var reprocessedJob = await _batchJobService.ReprocessJobAsync(job);

        // Reset job to Pending status so ChunkJobProcessor can pick it up
        if (reprocessedJob != null)
        {
            reprocessedJob.Status = BatchJobStatusStrings.PENDING;
            await _batchJobService.UpsertBatchJobAsync(reprocessedJob);
        }

        Log.Debug($"IngestService::ReprocessBatchJob - job {batchJobId} reset to Pending status");
    }

    public async Task<(DropboxConfig?, BatchJob?)> ImportBlobFromUriAsync(Uri blobUri)
    {
        var tenancy = blobUri.Segments[1].TrimEnd('/');
        var tenant = blobUri.Segments[2].TrimEnd('/');
        var path = string.Join("", blobUri.Segments[2..^1]).TrimEnd('/');
        var originalFileName = blobUri.Segments[^1];
        originalFileName = WebUtility.UrlDecode(originalFileName);

        bool reprocessEvent = (path.EndsWith("reprocess") || path.EndsWith("reconcile"));

        if (string.IsNullOrEmpty(tenant))
        {
            Log.Debug($"IngestService::ImportBlobFromUriAsync - Blob Uri must contain a valid tenant id (name)");
            throw new Exception("IngestService::ImportBlobFromUriAsync - Blob Uri must contain a valid tenant id (name)");
        }

        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(originalFileName))
        {
            Log.Debug($"IngestService::ImportBlobFromUriAsync - Blob Uri must contain a valid Path and OriginalFileName");
            throw new Exception("IngestService::ImportBlobFromUriAsync - Invalid Blob storage ingest request.");
        }

        Log.Debug("IngestService::ImportBlobFromUriAsync - Tenant: {0}; Path: {1}; OriginalFilename: {2}", tenant, path, originalFileName);

        var dropboxConfig = await _dropboxConfigCache.GetDropboxConfigForDirectoryAsync(tenant.ToLower(), path.ToLower());
        if (dropboxConfig == null)
        {
            Log.Warning($"IngestService::ImportBlobFromUriAsync - File create event received for tenant {tenant} on unhandled path {path}");
            return (null, null);
        }

        try
        {
            await using var readStream = await _dataLakeAdapter.GetFileReadStreamAsync(path.ToLower(), originalFileName, tenancy.ToLower());

            var (_, batchJob) = await _batchFileService.CreateBatchFromFileStreamAsync(tenancy, tenant, dropboxConfig, 
                originalFileName, path, readStream);

            // Update reprocess flag if needed (for both new and existing jobs)
            if (reprocessEvent && !batchJob.ReprocessEvent)
            {
                batchJob.ReprocessEvent = true;
                batchJob.LastUpdated = DateTimeOffset.UtcNow;
                try
                {
                    batchJob = await _batchJobService.UpsertBatchJobAsync(batchJob);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex,
                        "IngestService::ImportBlobFromUriAsync - Error updating ReprocessEvent flag for BatchJob {BatchJobId}",
                        batchJob.Id);
                }
            }

            return (dropboxConfig, batchJob);
        }
        catch (Exception e)
        {
            Log.Error(e, "IngestService::ImportBlobFromUriAsync - Error while ingesting blob");
            throw;
        }
    }

    /// <summary>
    /// Creates batch jobs from a blob URI without processing them.
    /// Handles file chunking if needed and saves all jobs with PENDING status.
    /// Jobs will be processed by ChunkJobProcessor.
    /// </summary>
    public async Task<int> CreateBatchJobsFromBlobUriAsync(Uri blobUri)
    {
        Log.Debug("IngestService::CreateBatchJobsFromBlobUriAsync - Processing blob: {BlobUri}", blobUri);

        // Step 1: Import blob to create BatchFile and initial BatchJob
        var (dropboxConfig, batchJob) = await ImportBlobFromUriAsync(blobUri);

        if (dropboxConfig == null || batchJob == null)
        {
            Log.Warning("IngestService::CreateBatchJobsFromBlobUriAsync - No batch job created for blob {BlobUri}", blobUri);
            return 0;
        }

        Log.Information("IngestService::CreateBatchJobsFromBlobUriAsync - Created initial batch job {BatchJobId} for blob {BlobUri}",
            batchJob.Id, blobUri);

        // Step 2: Get BatchFile to check if chunking is needed
        var batchFile = await _batchFileService.GetBatchFileAsync(batchJob.TenantId, batchJob.BatchFileId);
        if (batchFile == null)
        {
            Log.Warning("IngestService::CreateBatchJobsFromBlobUriAsync - BatchFile not found for batch job {BatchJobId}", batchJob.Id);
            return 1; // Initial job was created, return 1
        }

        // Step 3: Get batch jobs if there are any for this batchfile
        var storedBatchJobs = await _batchJobService.GetBatchFileJobsAsync(batchJob.TenantId, batchFile.Id) ?? new PagedResultSet<BatchJob>();

        // Step 4: Check if chunking is needed
        var chunkJobs = await _fileChunkingService.CreateChunkedBatchJobsAsync(batchJob, batchFile);
        if (chunkJobs.Count > 1)
        {
            // Chunking needed - save all chunk jobs
            Log.Information("IngestService::CreateBatchJobsFromBlobUriAsync - Created {ChunkCount} chunk jobs for batch {BatchJobId}",
                chunkJobs.Count, batchJob.Id);

            foreach (var chunkJob in chunkJobs)
            {
                var storedBatchJob = storedBatchJobs.Entities?.FirstOrDefault(x => x.Id == chunkJob.Id);
                if (storedBatchJob != null) continue; // already stored

                chunkJob.Status = BatchJobStatusStrings.PENDING;
                await _batchJobService.UpsertBatchJobAsync(chunkJob);
                Log.Debug("Created chunk job {ChunkJobId} for batch {BatchJobId}", chunkJob.Id, batchJob.Id);
            }

            Log.Information("IngestService::CreateBatchJobsFromBlobUriAsync - Saved {ChunkCount} chunk jobs for batch {BatchJobId}. Jobs will be processed by ChunkJobProcessor.",
                chunkJobs.Count, batchJob.Id);

            return chunkJobs.Count;
        }
        else
        {
            // No chunking needed - initial job already saved with PENDING status
            Log.Information("IngestService::CreateBatchJobsFromBlobUriAsync - File does not need chunking. Single batch job {BatchJobId} created and will be processed by ChunkJobProcessor.",
                 batchJob.Id); 

            return 1;
        }
    }

    //public async Task RunJobAsync(string tenantId, string batchFileId, string jobId)
    //{
    //    Log.Information("Attempting to run job {JobId} from batch file {BatchFileId} for tenant {TenantId}",
    //        jobId, batchFileId, tenantId);

    //    var claimedJob = await _batchJobService.ClaimJobAsync(
    //        tenantId,
    //        BatchJobService.GenerateClaimId(),
    //        batchFileId,
    //        jobId);

    //    if (claimedJob == null)
    //    {
    //        Log.Warning("Could not claim job {JobId} or any pending job for tenant {TenantId}",
    //            jobId, tenantId);
    //        return;
    //    }

    //    await RunAsync(claimedJob);
    //}

    public async Task RunJobAsync(BatchJob batchJob)
    {
        if (batchJob.IsChunk)
        {
            // Job is already claimed by ChunkJobProcessor via IJobClaimService
            // Initialize BatchJobService context so GetSchemaName() and GetDropboxConfig() work
            await _batchJobService.InitializeJobContext(batchJob);

            // Process chunk job directly
            await RunAsync(batchJob);
        }
        else
        {
            // Check if we should chunk this job
            var batchFile = await _batchFileService.GetBatchFileAsync(batchJob.TenantId, batchJob.BatchFileId);
            if (batchFile != null)
            {
                var storedBatchJobs = await _batchJobService.GetBatchFileJobsAsync(batchJob.TenantId, batchFile.Id);
                if (storedBatchJobs.Count > 1) // we know there are other (chunk) batchjobs, so we ignore
                {
                    //If we're here it mean that this is the first (initial locking) batch job...and it has been chunked
                    return; // Original job is done, chunks will be processed by ChunkJobProcessor
                }

                var chunkJobs = await _fileChunkingService.CreateChunkedBatchJobsAsync(batchJob, batchFile);
                if (chunkJobs.Count > 1) //If there should be chunks but none have yet been saved, we also ignore
                {
                    //If we're here it mean that this is the first (initial locking) batch job...and it has been chunked
                    return; // Original job is done, chunks will be processed by ChunkJobProcessor
                }
            }
            
            // Process as single job
            var claimedJob = await _batchJobService.ClaimPendingJobAsync(batchJob, BatchJobService.GenerateClaimId());
            await RunAsync(batchJob);
        }
    }

    public async Task<IngestFileResults> GetIngestResultsAsync(string tenantId, string batchFileId)
    {
        if (string.IsNullOrEmpty(tenantId)) throw new InvalidDataException($"IngestService::ClaimJobAsync - No tenantId provided");
        if (string.IsNullOrEmpty(batchFileId)) throw new InvalidDataException($"IngestService::ClaimJobAsync - No batch file id provided");

        var bf = await _batchFileService.GetBatchFileAsync(tenantId, batchFileId);
        if (bf == null) throw new InvalidDataException($"IngestService::ClaimJobAsync - No batch file found for id: {batchFileId}");

        return await GetResultsAsync(tenantId, bf);
    }

    private async Task RunAsync(BatchJob batchJob)
    {
        var tenancy = batchJob.Tenancy;
        var tenantId = batchJob.TenantId;

        // Ensure job context is initialized (safe to call even if already initialized)
        // This ensures GetSchemaName() and GetDropboxConfig() work for jobs that were
        // claimed outside of BatchJobService (e.g., via IJobClaimService in ChunkJobProcessor)
        try
        {
            _ = _batchJobService.GetModelName();
        }
        catch (Exception)
        {
            // Context not initialized, initialize it now
            Log.Debug("Job context not initialized in RunAsync, initializing now for job {JobId}", batchJob.Id);
            await _batchJobService.InitializeJobContext(batchJob);
        }
        
        var modelName = _batchJobService.GetModelName();
        var dropboxConfig = _batchJobService.GetDropboxConfig();
        
        // Create result file - for chunks, use chunk-specific naming
        var batchResultFile = batchJob.IsChunk 
            ? await _batchResultFileService.CreateResultFileForChunkAsync(batchJob)
            : await _batchResultFileService.CreateResultFileForJobAsync(batchJob);
        
        // Check if we can resume from where we left off
        var resumeInfo = await _resumabilityService.CheckResumeInfoAsync(batchJob, batchResultFile);

        // Use append mode if resuming (file exists and has content)
        var shouldAppend = resumeInfo.CanResume && resumeInfo.OutputFileExists;

        var batchResultFileWriteStream =
            await _batchResultFileService.GetWriteStreamForResultFileAsync(batchJob, batchResultFile, shouldAppend);
        var batchResultFileStreamWriter = new StreamWriter(batchResultFileWriteStream);

        // Use semaphore to synchronize writes to prevent concurrent access
        var writeSemaphore = new SemaphoreSlim(1, 1);

        Log.Debug("IngestService::RunAsync - Processing BatchJob {0} (IsChunk: {1})", batchJob.Id, batchJob.IsChunk);
        if (resumeInfo.CanResume)
        {
            Log.Information("IngestService::RunAsync - Resuming batch job {BatchJobId} from line {ProcessedLines}", 
                batchJob.Id, resumeInfo.ProcessedLines);
        }

        var index = resumeInfo.ProcessedLines + 1; // Start from where we left off
        var writeCounter = resumeInfo.ProcessedLines; // Account for already written lines
        var writeFrequency = _options.Value.WriteFrequency;
        
        try
        {
            // Use the new method that handles both regular and chunk jobs
            await _batchJobService.ProcessBatchJobWithIngestServiceAsync(batchJob, async (group) =>
            {
                Log.Debug("IngestService::RunAsync - Processing group {0}", index++);

                // Validate group is not null or empty
                if (group.ValueKind == JsonValueKind.Undefined || group.ValueKind == JsonValueKind.Null)
                {
                    Log.Warning("IngestService::RunAsync - Skipping null or undefined group at index {Index}", index - 1);
                    return;
                }

                BatchResultFileLineDto resultLineDto;
                
                try
                {
                    var strReq = group.ToString();
                    
                    // Use dynamic service invocation if configured, otherwise fall back to event service
                    object? response;
                    if (dropboxConfig?.ServiceTypeName != null && dropboxConfig?.MethodName != null)
                    {
                        // Dynamic service callj
                        if (dropboxConfig.MethodName == "TagLoyaltyAccountAsync")
                        {
                            var tagDto = JsonSerializer.Deserialize<TagDto>(group.GetRawText(), JsonUtility.GetDefaultOptions());
                            tagDto.TenantId = tenantId;
                            response = await _dynamicServiceInvoker.InvokeServiceAsync(
                                dropboxConfig.ServiceTypeName,
                                dropboxConfig.MethodName,
                                tenantId,
                                tagDto
                            );
                        }
                        else if (dropboxConfig.MethodName == "AliasLoyaltyAccountAsync")
                        {
                            var aliasRequest = JsonSerializer.Deserialize<AliasAccountRequest>(group.GetRawText(), JsonUtility.GetDefaultOptions());
                            response = await _dynamicServiceInvoker.InvokeServiceAsync(
                                dropboxConfig.ServiceTypeName,
                                dropboxConfig.MethodName,
                                tenantId,
                                aliasRequest
                            );
                        }
                        else
                        {
                            // Standard dynamic service call - matches ProcessEventAsync signature
                            // Parameters: tenantId, schemaName, jsonData, token, reprocessEvent
                            response = await _dynamicServiceInvoker.InvokeServiceAsync(
                                dropboxConfig.ServiceTypeName,
                                dropboxConfig.MethodName,
                                tenantId,
                                modelName,
                                group,
                                null,
                                batchJob.ReprocessEvent
                            );
                        }
                    }
                    else
                    {
                        // Fallback to existing event service
                        response = await _eventService.ProcessEventAsync(tenantId, modelName, group, null, batchJob.ReprocessEvent);
                    }

                    // Handle response using the response handler service with line number
                    resultLineDto = _responseHandlerService.HandleResponse(response, dropboxConfig, group, index - 1);
                }
                catch (APIErrorsException e)
                {

                    var errorcount = e.Errors.Count();
                    batchJob.TotalErrors = (batchJob.TotalErrors ?? 0) + errorcount;
                    Log.Error(e, "IngestService::RunAsync - Error while processing batch job {0}", batchJob.Id);
                    
                    resultLineDto = new BatchResultFileLineDto
                    {
                        LineKey = group.GetRawText(),
                        Success = false,
                        Errors = e.Errors?
                            .Select((err, i) => new KeyValuePair<string, string>($"Error_{i}", JsonSerializer.Serialize(err)))
                            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                        LineNumber = index - 1
                    };
                }
                catch (Exception e)
                {
                    Log.Error(e, "IngestService::RunAsync - Error while processing batch job {0}", batchJob.Id);
                
                    resultLineDto = new BatchResultFileLineDto
                    {
                        LineKey = group.GetRawText(),
                        Success = false,
                        Errors = new Dictionary<string, string> { { "Message", e.Message } },
                        LineNumber = index - 1
                    };
                }

                // Write result immediately
                var serializedLineDto = JsonSerializer.Serialize(resultLineDto);
                if (string.IsNullOrWhiteSpace(serializedLineDto))
                {
                    Log.Error("IngestService::RunAsync - Unable to serialize result");
                    serializedLineDto = "Result could not be serialized";
                }

                await batchResultFileStreamWriter.WriteLineAsync(serializedLineDto);
                writeCounter++;
                
                // Update progress tracking periodically (not after every record)
                if (writeCounter % writeFrequency == 0)
                {
                    try
                    {
                        await _resumabilityService.UpdateProgressAsync(batchJob, writeCounter, batchResultFileWriteStream.Position);
                        // Persist progress to database
                        batchJob.ETag = null;
                        await _batchJobService.UpsertBatchJobAsync(batchJob);
                        Log.Debug("Updated progress for batch job {BatchJobId}: {WriteCounter} lines processed", batchJob.Id, writeCounter);
                    }
                    catch (Exception progressEx)
                    {
                        Log.Warning(progressEx, "Failed to update progress for batch job {BatchJobId}, continuing processing", batchJob.Id);
                        // Continue processing even if progress update fails
                    }
                }

                // Note: We don't flush periodically because Azure Data Lake WriteStream
                // uses ETag-based conditional headers. Each flush commits and can cause
                // 412 ConditionNotMet errors on subsequent flushes. The stream will
                // automatically commit on disposal.

                //// Flush periodically for frequent writes
                //if (_options.Value.EnableFrequentWrites && writeCounter % writeFrequency == 0)
                //{
                //    await batchResultFileStreamWriter.FlushAsync();
                //    Log.Debug("Flushed {WriteCounter} results to file for batch {BatchJobId}", 
                //        writeCounter, batchJob.Id);
                //}
            });
            
            // Final progress update if not already done
            if (writeCounter % writeFrequency != 0)
            {
                try
                {
                    await _resumabilityService.UpdateProgressAsync(batchJob, writeCounter, batchResultFileWriteStream.Position);
                    await _batchJobService.UpsertBatchJobAsync(batchJob);
                    Log.Debug("Final progress update for batch job {BatchJobId}: {WriteCounter} lines processed", batchJob.Id, writeCounter);
                }
                catch (Exception progressEx)
                {
                    Log.Warning(progressEx, "Failed to update final progress for batch job {BatchJobId}", batchJob.Id);
                }
            }

            // Note: We don't manually flush here. Azure Data Lake WriteStream will automatically
            // commit all buffered data when disposed. Manual flushing causes ETag conflicts (412 errors)
            // because each flush is a commit operation that changes the file's ETag state.

            //// Final flush - handle 412 ConditionNotMet errors with retry
            //if (_options.Value.EnableFrequentWrites)
            //{
            //    await FlushWithRetryAsync(batchResultFileStreamWriter, batchJob.Id);
            //}

            Log.Debug("IngestService::RunAsync - Processing batch job {0} complete", batchJob.Id);

            // Mark the job as completed
            batchJob.Status = BatchJobStatusStrings.COMPLETE;
            await _batchJobService.UpsertBatchJobAsync(batchJob);
            Log.Debug("IngestService::RunAsync - Batch job {0} marked as completed", batchJob.Id);

        }
        catch (Exception e)
        {
            Log.Error(e, "IngestService::RunAsync - Error while processing batch job {0}", batchJob.Id);

            var resultLineDto = new BatchResultFileLineDto
            {
                LineKey = $"Batchfile line number - {index}",
                Success = false,
                Errors = new Dictionary<string, string> { { "Message", e.Message } }
            };

            var serializedLineDto = JsonSerializer.Serialize(resultLineDto);

            try
            {
                await batchResultFileStreamWriter.WriteLineAsync(serializedLineDto);

                // Note: We don't flush on error. Azure Data Lake WriteStream will automatically
                // commit buffered data on disposal. Manual flushing causes 412 ConditionNotMet errors.
            }
            catch (Exception writeEx)
            {
                Log.Warning(writeEx, "Error writing error line to result file for job {BatchJobId}", batchJob.Id);
            }

            throw;
        }
        finally
        {
            // Manually dispose streams and catch 412 errors that occur during disposal
            // Azure Data Lake WriteStream commits on disposal, which can fail with 412 ConditionNotMet
            // if ETag conditions don't match. We log but don't throw to avoid masking other errors.
            // StreamWriter.DisposeAsync will also dispose the underlying stream, so we only need to dispose the writer.

            if (batchResultFileStreamWriter != null)
            {
                try
                {
                    await batchResultFileStreamWriter.DisposeAsync();
                }
                catch (Exception ex) when (IsConditionNotMetError(ex))
                {
                    // 412 errors during disposal are common and expected - the data was likely already committed
                    // This happens when Azure Data Lake's internal ETag state doesn't match during final commit
                    Log.Debug("ConditionNotMet error during StreamWriter disposal for job {BatchJobId} - data may already be committed", batchJob.Id);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error disposing StreamWriter for job {BatchJobId}", batchJob.Id);
                }
            }
            else if (batchResultFileWriteStream != null)
            {
                // If StreamWriter was never created, dispose the underlying stream directly
                try
                {
                    await batchResultFileWriteStream.DisposeAsync();
                }
                catch (Exception ex) when (IsConditionNotMetError(ex))
                {
                    Log.Debug("ConditionNotMet error during stream disposal for job {BatchJobId} - data may already be committed", batchJob.Id);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error disposing stream for job {BatchJobId}", batchJob.Id);
                }
            }
        }
    }

    /// <summary>
    /// Flushes the stream writer with retry logic for 412 ConditionNotMet errors.
    /// These can occur when Azure Data Lake Storage ETags don't match due to concurrent access.
    /// </summary>
    private async Task FlushWithRetryAsync(StreamWriter writer, string batchJobId, int maxRetries = 3)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await writer.FlushAsync();
                return; // Success
            }
            catch (Exception ex) when (IsConditionNotMetError(ex) && attempt < maxRetries)
            {
                var delayMs = 200 * attempt; // Exponential backoff: 200ms, 400ms, 600ms
                Log.Warning(ex,
                    "ConditionNotMet error flushing result file for job {BatchJobId} on attempt {Attempt}. Retrying in {Delay}ms",
                    batchJobId, attempt, delayMs);
                await Task.Delay(delayMs);
            }
        }

        // Final attempt - if it fails, log and let it throw
        try
        {
            await writer.FlushAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to flush result file for job {BatchJobId} after {MaxRetries} retries", batchJobId, maxRetries);
            throw;
        }
    }

    private static bool IsConditionNotMetError(Exception ex)
    {
        return ex.Message.Contains("ConditionNotMet", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("412", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("conditional header", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<IngestFileResults> GetResultsAsync(string tenantId, BatchFile batchFile)
    {
        var response = new IngestFileResults
        {
            TenantId = tenantId,
            Errors = new Dictionary<string, string>(),
            IngestFileChunkResults = new List<IngestFileChunkResult>(),
            IngestFileErrors = new List<IngestFileError>()
        };

        try
        {
            // Get all batch jobs for this file
            var allJobs = new List<BatchJob>();
            var continuationToken = (string?)null;

            do
            {
                var jobs = await _batchJobService.GetBatchFileJobsAsync(tenantId, batchFile.Id, 1000, continuationToken);
                if (jobs?.Entities?.Any() == true)
                {
                    allJobs.AddRange(jobs.Entities);
                }
                continuationToken = jobs?.ContinuationToken;
            }
            while (!string.IsNullOrEmpty(continuationToken));

            if (!allJobs.Any())
            {
                response.Errors.Add("InvalidRequestError", $"No batch jobs found for tenant: {tenantId}, batch file id: {batchFile?.Id} - {batchFile?.OriginalFileName ?? "unknown file name"}");
                return response;
            }

            Log.Debug("Found {JobCount} batch jobs for file {FileName}", allJobs.Count, batchFile.OriginalFileName);

            // Create result file reader
            var resultFileReader = new ResultFileReader(_dataLakeAdapter);

            // Read result files in parallel for better performance
            //var readTasks = allJobs.Select(async job =>
            //{
            //    try
            //    {
            //        if (job.IsChunk)
            //        {
            //            return await resultFileReader.ReadChunkResultFileAsync(job);
            //        }
            //        else
            //        {
            //            return await resultFileReader.ReadResultFileAsync(job);
            //        }
            //    }
            //    catch (Exception ex)
            //    {
            //        Log.Warning(ex, "Failed to read result file for job {JobId}", job.Id);
            //        return new List<BatchResultFileLineDto>();
            //    }
            //}).ToArray();

            // Read result files with concurrency control
            var allResults = new List<BatchResultFileLineDto>();

            // Create concurrency controller for result file reading
            var resultFileConcurrencyController = new ConcurrencyController(
                _ingestOptions.Value.ResultAggregation.MaxConcurrency,
                _ingestOptions.Value.ResultAggregation.EnableParallelProcessing,
                Log.Logger
            );

            var readTasks = allJobs.Select(async job =>
            {
                return await resultFileConcurrencyController.ExecuteAsync(async () =>
                {
                    try
                    {
                        if (job.IsChunk)
                        {
                            return await resultFileReader.ReadChunkResultFileAsync(job);
                        }
                        else
                        {
                            return await resultFileReader.ReadResultFileAsync(job);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to read result file for job {JobId}", job.Id);
                        return new List<BatchResultFileLineDto>();
                    }
                });
            });

            var results = await Task.WhenAll(readTasks);
            foreach (var result in results)
            {
                if (result != null)
                {
                    allResults.AddRange(result);
                }
            }

            // Wait for all file reads to complete
            //var allResultArrays = await Task.WhenAll(readTasks);
            //var allResults = allResultArrays.SelectMany(results => results).ToList();

            Log.Debug("Read {TotalResultCount} result lines from {JobCount} jobs", allResults.Count, allJobs.Count);

            // Aggregate results using the ResultsAggregator with concurrency control
            var aggregationConcurrencyController = new ConcurrencyController(
                _ingestOptions.Value.ResultAggregation.MaxConcurrency,
                _ingestOptions.Value.ResultAggregation.EnableParallelProcessing,
                Log.Logger
            );
            var resultsAggregator = new ResultsAggregator(aggregationConcurrencyController, Log.Logger);
            response = await resultsAggregator.AggregateResultsAsync(allJobs, allResults, tenantId, batchFile);

            Log.Information("Successfully generated results report for file {FileName}: {ChunkCount} chunks, {ErrorCount} errors",
                batchFile.OriginalFileName, response.IngestFileChunkResults.Count, response.IngestFileErrors.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error generating results for file {FileName}", batchFile.OriginalFileName);
            response.Errors.Add("ReportGenerationError", $"Failed to generate results report: {ex.Message}");
        }

        return response;
    }
}