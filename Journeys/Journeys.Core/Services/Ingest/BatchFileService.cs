using Journeys.Core.Configuration;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.FileStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Microsoft.Extensions.Options;
using Serilog;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Journeys.Core.Services.Ingest;

public class BatchFileService(
    IBatchFileAdapter batchFileAdapter,
    IDataLakeAdapter dataLakeAdapter,
    IBatchJobAdapter batchJobAdapter,
    IOptions<BatchProcessingOptions> options) : IBatchFileService
{
    public const string FILE_SYSTEM_NAME = "batchfiles";

    public async Task<(BatchFile, BatchJob)> CreateBatchFromFileStreamAsync(string tenancy, string tenantId, DropboxConfig dropboxConfig, string fileName, string path, Stream fileStream)
    {
        try
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                Log.Debug($"BatchFileService::CreateBatchFromFileStreamAsync - Blob Uri must contain a valid tenant id (name)");
                throw new Exception("BatchFileService::CreateBatchFromFileStreamAsync - Blob Uri must contain a valid tenant id (name)");
            }

            if (string.IsNullOrEmpty(dropboxConfig.ModelName) || string.IsNullOrEmpty(fileName))
            {
                Log.Debug($"BatchFileService::CreateBatchFromFileStreamAsync - Blob Uri must contain a valid Path and OriginalFileName");
                throw new Exception("BatchFileService::CreateBatchFromFileStreamAsync - Invalid Blob storage ingest request.");
            }

            // Idempotency check: Check if BatchFile already exists for this blob
            var normalizedTenantId = tenantId.ToLower();
            var normalizedOriginalFileName = fileName.ToLower();
            var normalizedJobDirectory = dropboxConfig.Directory.ToLower();

            var existingBatchFile = await FindBatchFileByNaturalKeyAsync(
                normalizedTenantId,
                normalizedOriginalFileName,
                normalizedJobDirectory);

            if (existingBatchFile != null)
            {
                // Check if file is stale (older than OrphanDetectionTimeout)
                var orphanTimeout = options.Value.OrphanDetectionTimeout;
                var fileAge = DateTimeOffset.UtcNow - existingBatchFile.CreateDate;

                if (fileAge <= orphanTimeout)
                {
                    // File exists and is not stale - this is likely a duplicate event
                    Log.Warning(
                        "BatchFileService::CreateBatchFromFileStreamAsync - Duplicate file creation detected. " +
                        "BatchFile {BatchFileId} already exists for tenant {TenantId}, file {FileName}, directory {Directory} (created {CreateDate}). " +
                        "Skipping duplicate processing and returning existing file.",
                        existingBatchFile.Id, normalizedTenantId, normalizedOriginalFileName, normalizedJobDirectory, existingBatchFile.CreateDate);

                    // Try to find or create the associated BatchJob
                    BatchJob? existingBatchJob = null;
                    try
                    {
                        // Get all jobs for this batch file
                        var jobsResult = await batchJobAdapter.FetchBatchFileJobsAsync(
                            normalizedTenantId,
                            existingBatchFile.Id,
                            pageSize: 100);

                        // Find the first non-chunk job (parent job)
                        existingBatchJob = jobsResult?.Entities?
                            .FirstOrDefault(j => !j.IsChunk && j.ParentBatchJobId == null);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex,
                            "BatchFileService::CreateBatchFromFileStreamAsync - Error fetching existing BatchJobs for BatchFile {BatchFileId}",
                            existingBatchFile.Id);
                    }

                    // If no BatchJob exists, create one
                    if (existingBatchJob == null)
                    {
                        try
                        {
                            int totalLines = GetTotalLinesForSmallFile(fileStream);

                            existingBatchJob = await CreateBatchJobAsync(tenancy, normalizedTenantId, existingBatchFile.Id, fileName, path, totalLines);
                            Log.Information(
                                   "BatchFileService::CreateBatchFromFileStreamAsync - Created missing BatchJob {BatchJobId} for existing BatchFile {BatchFileId}",
                                   existingBatchJob.Id, existingBatchFile.Id);
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex,
                                "BatchFileService::CreateBatchFromFileStreamAsync - Error creating BatchJob for existing BatchFile {BatchFileId}. " +
                                "This may indicate a concurrent creation. Returning existing file without job.",
                                existingBatchFile.Id);
                            // Return existing file even if job creation failed
                            throw new Exception($"BatchFileService::CreateBatchFromFileStreamAsync - Failed to create BatchJob for existing BatchFile {existingBatchFile.Id}", ex);
                        }
                    }

                    return (existingBatchFile, existingBatchJob);
                }
                else
                {
                    // File is stale - log warning but allow reprocessing
                    Log.Warning(
                        "BatchFileService::CreateBatchFromFileStreamAsync - Found existing BatchFile {BatchFileId} that is {Age} old (older than OrphanDetectionTimeout {Timeout}). " +
                        "This may indicate a duplicate event or stale file. Proceeding with new file creation.",
                        existingBatchFile.Id, fileAge, orphanTimeout);
                }
            }

            // No existing BatchFile found (or it's stale) - proceed with creation
            var directory = $"{tenantId}";

            await dataLakeAdapter.CreateDirectoryAsync(directory.ToLower(), FILE_SYSTEM_NAME.ToLower());

            var newFileName = await GetNewFileNameAsync(tenantId.ToLower(), fileName.ToLower());
            if (string.IsNullOrEmpty(newFileName))
            {
                Log.Debug($"BatchFileService::CreateBatchFromFileStreamAsync - Failed to create temporary batch file name");
                throw new Exception("BatchFileService::CreateBatchFromFileStreamAsync - Failed to create temporary batch file name");
            }

            int totalLinesNew = GetTotalLinesForSmallFile(fileStream);

            var bf = new BatchFile
            (
                directory.ToLower() ?? "unk_directory",
                newFileName.ToLower() ?? "unk_file",
                fileName.ToLower() ?? "unk_orig_file",
                dropboxConfig.ModelName,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                tenantId.ToLower(),
                Guid.NewGuid().ToString(),
                dropboxConfig.Directory
            );

            var batchFile = await batchFileAdapter.UpsertBatchFileAsync(bf);
            if (string.IsNullOrEmpty(tenantId))
            {
                Log.Debug($"BatchFileService::CreateBatchFromFileStreamAsync - Failed to create temporary batch file");
                throw new Exception("BatchFileService::CreateBatchFromFileStreamAsync - Failed to create temporary batch file");
            }

            var batchJob = await CreateBatchJobAsync(tenancy, tenantId.ToLower(), batchFile.Id, fileName, path, totalLinesNew);
            if (string.IsNullOrEmpty(tenantId))
            {
                Log.Debug($"BatchFileService::CreateBatchFromFileStreamAsync - Failed to create batch job(s)");
                throw new Exception("BatchFileService::CreateBatchFromFileStreamAsync - Failed to create batch job(s)");
            }

            //Finally, upload output file
            await dataLakeAdapter.UploadFileAsync(directory.ToLower(), newFileName.ToLower(), fileStream, FILE_SYSTEM_NAME.ToLower());

            return (batchFile, batchJob);
        }
        catch (Exception ex)
        {
            Log.Debug($"BatchFileService::CreateBatchFromFileStreamAsync Exception - {ex.ToString()}");
            throw;
        }
    }

    /// <summary>
    /// Generates a deterministic ID for a parent BatchJob based on BatchFileId.
    /// This ensures only one parent BatchJob can exist per BatchFile, preventing duplicates.
    /// </summary>
    private static string GenerateDeterministicBatchJobId(string batchFileId)
    {
        // Normalize input to ensure consistency
        var normalizedBatchFileId = batchFileId?.ToLowerInvariant().Trim() ?? string.Empty;

        // Create composite key: batchFileId + constant for "parent job"
        // This makes it explicit that this is for the parent (non-chunk) job
        var compositeKey = $"{normalizedBatchFileId}|parent";

        // Generate SHA256 hash and convert to GUID-like string format
        using (var sha256 = SHA256.Create())
        {
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(compositeKey));
            // Convert to GUID format (32 hex characters with hyphens)
            var guidString = new Guid(hashBytes.Take(16).ToArray()).ToString();
            return guidString;
        }
    }

    /// <summary>
    /// Creates a new BatchJob for the given BatchFile.
    /// Centralized method to avoid code duplication.
    /// Uses deterministic ID to prevent duplicate parent jobs.
    /// </summary>
    private async Task<BatchJob> CreateBatchJobAsync(string tenancy, string tenantId, string batchFileId, string fileName, string path, int totalLines)
    {
        var fn = WebUtility.UrlDecode(fileName);

        // Generate deterministic ID for parent BatchJob to prevent duplicates
        var deterministicId = GenerateDeterministicBatchJobId(batchFileId);

        var batchJob = new BatchJob(
            BatchJobStatusStrings.PENDING,
            batchFileId,
            tenancy,
            fn.ToLower(),
            path,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            tenantId,
            deterministicId, // Use deterministic ID instead of null
            null, // claimedBy
            null, // batchResultFileId
            null, // parentBatchJobId
            null, // chunkIndex
            null, // totalChunks
            totalLines > 0 ? 1 : 0,// chunkStartLine
            totalLines, // chunkEndLine
            false, // isChunk
            null, // processedBytes
            null, // processedLines
            null // lastProcessedAt
        );

        // Use UpsertBatchJobAsync which handles ETag-based optimistic concurrency
        // With deterministic ID, if two nodes try to create the same job, they'll both use the same ID
        // and the upsert will either update the existing one or handle the conflict via ETag
        return await batchJobAdapter.UpsertBatchJobAsync(batchJob);
    }
    private int GetTotalLinesForSmallFile(Stream fileStream)
    {
        int totalLines = 0;
        long fileSize = 0;

        try
        {
            if (fileStream.CanSeek)
                fileSize = fileStream.Length;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not determine file stream length");
        }

        if (fileSize > 0 && fileSize <= options.Value.ChunkSizeBytes)
        {
            totalLines = CountLinesFromStream(fileStream);

            Log.Debug(
                "Counted {TotalLines} data lines (Size: {Size} bytes)",
                totalLines,
                fileSize);

            if (fileStream.CanSeek)
                fileStream.Position = 0;
        }
        else
        {
            Log.Debug("Skipping line count (Size: {Size} bytes)", fileSize);
        }

        return totalLines;
    }
    private int CountLinesFromStream(Stream stream)
    {
        try
        {
            using var reader = new StreamReader(stream, leaveOpen: true);

            var headerLine = reader.ReadLine();
            if (headerLine == null)
                return 0;

            int lineCount = 0;
            while (reader.ReadLine() != null)
                lineCount++;

            return lineCount;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error counting lines from stream");
            return 0;
        }
    }

    private async Task<string> GetNewFileNameAsync(string tenantId, string fileName)
    {
        var extension = Path.GetExtension(fileName);
        var newFileName = $"{Guid.NewGuid()}{extension}";

        while (await dataLakeAdapter.FileExistsAsync(tenantId, newFileName, FILE_SYSTEM_NAME))
        {
            newFileName = $"{Guid.NewGuid()}{extension}";
        }

        return newFileName;
    }

    public async Task<BatchFile?> GetBatchFileAsync(string tenantId, string batchFileId)
    {
        return await batchFileAdapter.FetchBatchFileAsync(tenantId, batchFileId);
    }

    public async Task<BatchFile?> FindBatchFileByNaturalKeyAsync(string tenantId, string originalFileName, string jobDirectory)
    {
        return await batchFileAdapter.FindBatchFileByNaturalKeyAsync(tenantId, originalFileName, jobDirectory);
    }
}
