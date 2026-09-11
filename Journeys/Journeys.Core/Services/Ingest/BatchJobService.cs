using System.Text.Json;
using Journeys.Core.Caching;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.FileStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Interfaces.Utilities;
using Journeys.Core.Models;
using Journeys.Core.Utility.BatchFileReaders;
using Journeys.Core.Utility;
using Serilog;

namespace Journeys.Core.Services.Ingest;

public class BatchJobService : IBatchJobService
{
    private readonly IBatchJobAdapter _batchJobAdapter;
    private readonly IBatchFileAdapter _batchFileAdapter;
    private readonly IDataLakeAdapter _dataLakeAdapter;
    private readonly IDropboxConfigCache _dropboxConfigCache;
    private readonly ResumabilityService _resumabilityService;
    private readonly IJobClaimService _jobClaimService;

    private BatchJob? _claimedBatchJob;
    private BatchFile? _claimedBatchFile;
    private DropboxConfig? _dropboxConfig;

    public BatchJobService(
        IBatchJobAdapter batchJobAdapter,
        IBatchFileAdapter batchFileAdapter,
        IDataLakeAdapter dataLakeAdapter,
        IDropboxConfigCache dropboxConfigCache,
        ResumabilityService resumabilityService,
        IJobClaimService jobClaimService)
    {
        _batchJobAdapter = batchJobAdapter;
        _batchFileAdapter = batchFileAdapter;
        _dataLakeAdapter = dataLakeAdapter;
        _dropboxConfigCache = dropboxConfigCache;
        _resumabilityService = resumabilityService;
        _jobClaimService = jobClaimService;

        _dataLakeAdapter.ChangeFileSystem(BatchFileService.FILE_SYSTEM_NAME).GetAwaiter().GetResult();
    }

    private static readonly SemaphoreSlim _claimLock = new SemaphoreSlim(1, 1);

    public async Task<BatchJob> GetJobAsync(string tenantId, string batchFileId, string jobId)
    {
        // New partition-scoped point read path
        var job = await _batchJobAdapter.FetchBatchJobAsync(tenantId, batchFileId, jobId);
        if (job == null)
        {
            Log.Debug($"BatchJobService::GetJobAsync - No job found for {jobId} in batchFile {batchFileId}");
        }
        return job;
    }

    public async Task<PagedResultSet<BatchJob>> GetBatchFileJobsAsync(string tenantId, string batchFileId, int pageSize = 1000, string continuationToken = null)
    {
        return await _batchJobAdapter.FetchBatchFileJobsAsync(tenantId, batchFileId, pageSize, continuationToken);
    }

    public async Task<BatchJob> ClaimPendingJobAsync(BatchJob unclaimedJob, string claimant)
    {
        _claimedBatchJob = unclaimedJob;
        if (!unclaimedJob.Status.Equals(BatchJobStatusStrings.PROCESSING))
            _claimedBatchJob = await SetBatchJobProcessingAsync(unclaimedJob, claimant);
        
        _claimedBatchFile = await GetBatchFileForBatchJobAsync(_claimedBatchJob);
        _dropboxConfig = await _dropboxConfigCache.GetDropboxConfigForBatchFileAsync(_claimedBatchFile);
        if (_dropboxConfig == null) throw new Exception("BatchJobService::ClaimPendingJobAsync - No dropbox configuration for the provided batch file.");

        _claimedBatchJob.ReprocessEvent = unclaimedJob.ReprocessEvent;
        return _claimedBatchJob;
    }

    /// <summary>
    /// Initializes the job context for an already-claimed job without attempting to claim it.
    /// This is used when a job has been claimed via IJobClaimService and needs BatchJobService
    /// internal state (_claimedBatchFile, _dropboxConfig) to be set for processing.
    /// </summary>
    public async Task InitializeJobContext(BatchJob batchJob)
    {
        if (batchJob == null)
        {
            throw new ArgumentNullException(nameof(batchJob), "Cannot initialize job context with null batch job");
        }

        _claimedBatchJob = batchJob;
        _claimedBatchFile = await GetBatchFileForBatchJobAsync(_claimedBatchJob);
        _dropboxConfig = await _dropboxConfigCache.GetDropboxConfigForBatchFileAsync(_claimedBatchFile);
        if (_dropboxConfig == null) throw new Exception("BatchJobService::ClaimPendingJobAsync - No dropbox configuration for the provided batch file.");

        Log.Debug("Initialized job context for job {JobId} from batch file {BatchFileId}",
            batchJob.Id, batchJob.BatchFileId);
    }

    public async Task<BatchJob> ReprocessJobAsync(BatchJob job)
    {
        _claimedBatchJob = await MarkBatchJobUnclaimedAsync(job);

        return _claimedBatchJob;
    }

    public async Task ProcessBatchJobWithIngestServiceAsync(BatchJob batchJob, Func<JsonElement, Task> processGroup)
    {
        // Claim the job first (required for both chunk and regular jobs)
        //if (string.IsNullOrEmpty(batchJob?.ClaimedBy))
        //    await ClaimJobAsync(batchJob);

        const int progressUpdateInterval = 50; // Update progress every 10 lines

        if (batchJob.IsChunk)
        {
            var chunkReader = GetChunkBatchFileReader(_claimedBatchFile, batchJob);
            var chunkStream = await GetChunkFileStreamAsync(batchJob);
            var nxtGroup = chunkReader.GetNextGroupAsync(chunkStream);

            var processedCount = 0;
            await foreach (var group in nxtGroup)
            {
                // For chunked jobs, ProcessedLines represents lines processed within this chunk
                // Skip lines that were already processed in this chunk
                if (batchJob.ProcessedLines.HasValue && processedCount < batchJob.ProcessedLines.Value)
                {
                    processedCount++;
                    continue;
                }

                await processGroup(group);
                processedCount++;

                // Update progress every 10 lines (or at the end)
                if (processedCount % progressUpdateInterval == 0 || processedCount == batchJob.ChunkEndLine - batchJob.ChunkStartLine + 1)
                {
                    batchJob.ProcessedLines = processedCount;
                    await _batchJobAdapter.UpsertBatchJobAsync(batchJob);
                    Log.Debug("Updated progress for chunk job {JobId}: {ProcessedCount} lines processed", batchJob.Id, processedCount);
                }
            }

            // Final progress update if not already done
            if (processedCount % progressUpdateInterval != 0)
            {
                batchJob.ProcessedLines = processedCount;
                await _batchJobAdapter.UpsertBatchJobAsync(batchJob);
                Log.Debug("Final progress update for chunk job {JobId}: {ProcessedCount} lines processed", batchJob.Id, processedCount);
            }
        }
        else
        {
            var nxtGroup = ReadGroupAsync();
            var processedCount = 0;
            await foreach (var group in nxtGroup)
            {
                // For non-chunked jobs, ProcessedLines represents total lines processed
                // Skip lines that were already processed
                if (batchJob.ProcessedLines.HasValue && processedCount < batchJob.ProcessedLines.Value)
                {
                    processedCount++;
                    continue;
                }

                await processGroup(group);
                processedCount++;

                // Update progress every 10 lines
                if (processedCount % progressUpdateInterval == 0)
                {
                    batchJob.ProcessedLines = processedCount;
                    await _batchJobAdapter.UpsertBatchJobAsync(batchJob);
                    Log.Debug("Updated progress for job {JobId}: {ProcessedCount} lines processed", batchJob.Id, processedCount);
                }
            }

            // Final progress update if not already done
            if (processedCount % progressUpdateInterval != 0)
            {
                batchJob.ProcessedLines = processedCount;
                await _batchJobAdapter.UpsertBatchJobAsync(batchJob);
                Log.Debug("Final progress update for job {JobId}: {ProcessedCount} lines processed", batchJob.Id, processedCount);
            }
        }
    }

    //private async Task ClaimJobAsync(BatchJob batchJob)
    //{
    //    _claimedBatchJob = await SetBatchJobProcessingAsync(batchJob, GenerateClaimId());
    //    _claimedBatchFile = await GetBatchFileForBatchJobAsync(_claimedBatchJob);
    //    _dropboxConfig = await _dropboxConfigAdapter.FetchDropboxConfigForBatchFileAsync(_claimedBatchFile);
    //    _claimedBatchJob.ReprocessEvent = batchJob.ReprocessEvent;
    //}

    public async IAsyncEnumerable<JsonElement> ReadGroupAsync()
    {
        await _dataLakeAdapter.ChangeFileSystem(BatchFileService.FILE_SYSTEM_NAME);

        if (_claimedBatchFile == null)
        {
            Log.Debug($"BatchJobService::ReadGroupAsync - No claimed batch file found");
            throw new Exception("BatchJobService::ReadGroupAsync - No claimed batch file found");
        }

        await using var fileStream = await _dataLakeAdapter.GetFileReadStreamAsync(_claimedBatchFile.Directory, _claimedBatchFile.FileName);
        if (fileStream == null)
        {
            Log.Debug($"BatchJobService::ReadGroupAsync - Could not get file stream");
            throw new Exception("BatchJobService::ReadGroupAsync - Could not get file stream");
        }

        var batchFileReader = GetBatchFileReader(_claimedBatchFile, _dropboxConfig);
        var nxtGroup = batchFileReader.GetNextGroupAsync(fileStream);
        await foreach (var group in nxtGroup)
        {
            yield return group;
        }

        _claimedBatchJob = await SetBatchJobCompletedAsync(_claimedBatchJob!);
        Log.Debug($"BatchJobService::ReadGroupAsync - Batch job completed: {_claimedBatchJob.Id}");
    }

    private async Task<Stream> GetChunkFileStreamAsync(BatchJob chunkJob)
    {
        await _dataLakeAdapter.ChangeFileSystem(BatchFileService.FILE_SYSTEM_NAME);

        if (_claimedBatchFile == null)
        {
            throw new Exception("No claimed batch file found for chunk processing");
        }

        // Get a fresh stream for each chunk to avoid conflicts
        using var fullStream = await _dataLakeAdapter.GetFileReadStreamAsync(_claimedBatchFile.Directory, _claimedBatchFile.FileName);
        if (fullStream == null)
        {
            throw new Exception("Could not get file stream for chunk processing");
        }

        // Create a stream that reads specific lines for the chunk
        return CreateLineBasedChunkStream(fullStream, chunkJob);
    }

    private Stream CreateLineBasedChunkStream(Stream fullStream, BatchJob chunkJob)
    {
        Log.Debug("CreateLineBasedChunkStream: Chunk {ChunkIndex} processing lines {StartLine}-{EndLine}",
            chunkJob.ChunkIndex, chunkJob.ChunkStartLine, chunkJob.ChunkEndLine);

        var startLine = chunkJob.ChunkStartLine ?? 0;
        var endLine = chunkJob.ChunkEndLine ?? 0;

        // Create the chunk content as a string first
        var chunkContent = new System.Text.StringBuilder();
        var linesInChunk = 0;
        var currentLine = 0;

        // Reset stream position
        fullStream.Position = 0;
        using var reader = new StreamReader(fullStream);
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            // Always include header (line 0)
            if (currentLine == 0)
            {
                chunkContent.AppendLine(line);
                Log.Debug("CreateLineBasedChunkStream: Added header: {Header}",
                    line.Length > 100 ? line.Substring(0, 100) + "..." : line);
            }
            // Include data lines for this chunk
            else if (currentLine >= startLine && currentLine <= endLine)
            {
                chunkContent.AppendLine(line);
                linesInChunk++;

                // Log first few lines for debugging
                if (linesInChunk <= 3)
                {
                    Log.Debug("CreateLineBasedChunkStream: Line {LineNumber} in chunk: {LineContent}",
                        currentLine, line.Length > 100 ? line.Substring(0, 100) + "..." : line);
                }
            }

            currentLine++;

            // Stop reading if we've passed our chunk
            if (currentLine > endLine)
                break;
        }

        Log.Debug("CreateLineBasedChunkStream: Chunk {ChunkIndex} created with header + {LinesInChunk} data lines",
            chunkJob.ChunkIndex, linesInChunk);

        // Convert string content to memory stream
        var content = chunkContent.ToString();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var memoryStream = new MemoryStream(bytes);

        return memoryStream;
    }

    private IBatchFileReader GetChunkBatchFileReader(BatchFile batchFile, BatchJob chunkJob)
    {
        // For now, use the same reader but with chunk constraints
        // You might want to implement chunk-aware readers in the future
        return GetBatchFileReader(batchFile, _dropboxConfig);
    }

    public string GetModelName()
    {
        if (_claimedBatchFile == null)
        {
            Log.Debug($"BatchJobService::GetModelName - No claimed batch file found");
            throw new Exception("BatchJobService::GetModelName - No claimed batch file found");
        }

        return _claimedBatchFile.ModelName;
    }


    public DropboxConfig? GetDropboxConfig()
    {
        return _dropboxConfig;
    }

    public async Task<BatchJob> UpsertBatchJobAsync(BatchJob batchJob)
    {
        return await _batchJobAdapter.UpsertBatchJobAsync(batchJob);
    }

    public static string GenerateClaimId()
    {
        return $"{Environment.ProcessId}-{Thread.CurrentThread.ManagedThreadId}";
    }

    private async Task<BatchJob> SetBatchJobProcessingAsync(BatchJob batchJob, string claimant)
    {
        // Delegate to JobClaimService
        var claimedJob = await _jobClaimService.ProcessJobAsync(
            batchJob.TenantId,
            batchJob.BatchFileId,
            batchJob.Id,
            claimant);

        if (claimedJob == null)
        {
            throw new InvalidOperationException($"Failed to claim batch job {batchJob.Id}");
        }

        return claimedJob;
    }

    private async Task<BatchJob> MarkBatchJobUnclaimedAsync(BatchJob batchJob)
    {
        batchJob.Status = BatchJobStatusStrings.PENDING;
        batchJob.ClaimedBy = null;
        var updatedBatchJob = await _batchJobAdapter.UpsertBatchJobAsync(batchJob);

        // No delete needed - we now use update-only pattern
        return updatedBatchJob;
    }

    private async Task<BatchJob> SetBatchJobCompletedAsync(BatchJob batchJob)
    {
        batchJob.Status = BatchJobStatusStrings.COMPLETE;
        var updatedBatchJob = await _batchJobAdapter.UpsertBatchJobAsync(batchJob);

        // No delete needed - we now use update-only pattern
        return updatedBatchJob;
    }

    private async Task<BatchFile> GetBatchFileForBatchJobAsync(BatchJob batchJob)
    {
        var batchFile = await _batchFileAdapter.FetchBatchFileAsync(batchJob.TenantId, batchJob.BatchFileId);
        if (batchFile == null)
        {
            Log.Debug($"BatchJobService::GetBatchFileForBatchJobAsync - BatchFile record with {batchJob.BatchFileId} not found");
            throw new Exception($"BatchJobService::GetBatchFileForBatchJobAsync - BatchFile record with {batchJob.BatchFileId} not found");
        }

        return batchFile;
    }

    private static IBatchFileReader GetBatchFileReader(BatchFile batchFile, DropboxConfig? dropboxConfig = null)
    {
        return Path.GetExtension(batchFile.FileName) switch
        {
            ".csv" => new CsvBatchFileReader(dropboxConfig),
            ".ndjson" => new NdjsonBatchFileReader(dropboxConfig),
            ".json" => new JsonBatchFileReader(dropboxConfig),
            _ => throw new Exception("Batch file format is not supported")
        };
    }
}
