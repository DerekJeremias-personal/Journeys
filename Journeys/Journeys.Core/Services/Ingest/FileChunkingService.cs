using Journeys.Core.Configuration;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.FileStorage;
using Journeys.Core.Models;
using Journeys.Core.Utilities;
using Journeys.Core.Utility;
using Journeys.DTO.Models;
using Microsoft.Extensions.Options;
using Serilog;
using System.Text.Json;

namespace Journeys.Core.Services.Ingest;

public class FileChunkingService
{
    private readonly IDataLakeAdapter _dataLakeAdapter;
    private readonly IBatchJobAdapter _batchJobAdapter;
    private readonly IBatchFileAdapter _batchFileAdapter;
    private readonly BatchProcessingOptions _options;

    public FileChunkingService(
        IDataLakeAdapter dataLakeAdapter,
        IBatchJobAdapter batchJobAdapter,
        IBatchFileAdapter batchFileAdapter,
        IOptions<BatchProcessingOptions> options)
    {
        _dataLakeAdapter = dataLakeAdapter;
        _batchJobAdapter = batchJobAdapter;
        _batchFileAdapter = batchFileAdapter;
        _options = options.Value;
    }

    public async Task<List<BatchJob>> CreateChunkedBatchJobsAsync(
        BatchJob originalJob,
        BatchFile originalFile,
        long chunkSize = 0)
    {
        if (chunkSize == 0)
            chunkSize = _options.ChunkSizeBytes;

        // Ensure minimum chunk size to prevent tiny chunks
        const long minimumChunkSize = 1024; // 1KB minimum
        if (chunkSize < minimumChunkSize)
        {
            Log.Debug("Chunk size {ChunkSize} is below minimum {MinimumChunkSize}, using minimum", 
                chunkSize, minimumChunkSize);
            chunkSize = minimumChunkSize;
        }

        var fileSize = await GetFileSizeAsync(originalFile);
        
        if (fileSize <= chunkSize)
        {
            // File is small enough, return original job
            Log.Debug("File size {FileSize} is smaller than chunk size {ChunkSize}, no chunking needed", 
                fileSize, chunkSize);
            return new List<BatchJob> { originalJob };
        }

        Log.Debug("Creating chunks for file {FileName} with size {FileSize}", 
            originalFile.FileName, fileSize);

        var chunks = await SplitFileIntoChunksAsync(originalFile, chunkSize);
        var chunkJobs = new List<BatchJob>();

        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            var chunkJob = CreateChunkJob(originalJob, chunk, i, chunks.Count);
            chunkJobs.Add(chunkJob);
        }

        Log.Debug("Created {ChunkCount} chunk jobs for file {FileName}", 
            chunkJobs.Count, originalFile.FileName);

        return chunkJobs;
    }

    private async Task<long> GetFileSizeAsync(BatchFile batchFile)
    {
        try
        {
            await _dataLakeAdapter.ChangeFileSystem(BatchFileService.FILE_SYSTEM_NAME);
            
            // Check if file exists first
            var fileExists = await _dataLakeAdapter.FileExistsAsync(batchFile.Directory, batchFile.FileName);
            if (!fileExists)
            {
                Log.Warning("File {FileName} does not exist, returning 0 size", batchFile.FileName);
                return 0;
            }
            
            // Get the actual file size
            return await _dataLakeAdapter.GetFileSizeAsync(batchFile.Directory, batchFile.FileName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting file size for {FileName}", batchFile.FileName);
            throw;
        }
    }

    public async Task<List<FileChunk>> SplitFileIntoChunksAsync(BatchFile batchFile, long chunkSize)
    {
        Log.Debug("SplitFileIntoChunksAsync: Starting CSV-aware chunking for file {FileName}", batchFile.FileName);
        
        var chunks = new List<FileChunk>();
        
        // Get file size first
        var fileSize = await GetFileSizeAsync(batchFile);
        Log.Debug("SplitFileIntoChunksAsync: File size is {FileSize} bytes", fileSize);
        
        if (fileSize == 0)
        {
            // Empty file - create single chunk
            chunks.Add(new FileChunk(0, 0, batchFile.FileName));
            return chunks;
        }
        
        // Sample rows to calculate average record size
        var averageRecordSize = await CalculateAverageRecordSizeAsync(batchFile);
        Log.Debug("SplitFileIntoChunksAsync: Average record size: {AverageRecordSize} bytes", averageRecordSize);
        
        if (averageRecordSize == 0)
        {
            // No valid records found
            chunks.Add(new FileChunk(1, 1, batchFile.FileName));
            return chunks;
        }
        
        // Calculate target chunk count based on file size
        var targetChunkCount = Math.Max(1, (int)Math.Ceiling((double)fileSize / chunkSize));
        
        // Calculate target records per chunk
        var targetRecordsPerChunk = Math.Max(1, (int)Math.Ceiling((double)fileSize / (averageRecordSize * targetChunkCount)));
        
        Log.Debug("SplitFileIntoChunksAsync: Target chunks {TargetChunkCount}, target records per chunk {TargetRecordsPerChunk}", 
            targetChunkCount, targetRecordsPerChunk);
        
        // Create chunks with complete record boundaries
        chunks = await CreateChunksWithCompleteRecordsAsync(batchFile, targetRecordsPerChunk);
        
        Log.Debug("SplitFileIntoChunksAsync: Created {ChunkCount} chunks", chunks.Count);
        
        return chunks;
    }
    
    private async Task<long> CalculateAverageRecordSizeAsync(BatchFile batchFile)
    {
        const int sampleSize = 100; // Sample first 100 records
        var totalSize = 0L;
        var recordCount = 0;
        
        await _dataLakeAdapter.ChangeFileSystem(BatchFileService.FILE_SYSTEM_NAME);
        using var stream = await _dataLakeAdapter.GetFileReadStreamAsync(batchFile.Directory, batchFile.FileName);
        using var reader = new StreamReader(stream);
        
        // Skip header
        var headerLine = await reader.ReadLineAsync();
        if (headerLine == null) return 0;
        
        // Sample records to calculate average size
        for (int i = 0; i < sampleSize; i++)
        {
            var line = await reader.ReadLineAsync();
            if (line == null) break;
            
            totalSize += line.Length;
            recordCount++;
        }
        
        return recordCount > 0 ? totalSize / recordCount : 0;
    }
    
    private async Task<List<FileChunk>> CreateChunksWithCompleteRecordsAsync(BatchFile batchFile, int targetRecordsPerChunk)
    {
        var chunks = new List<FileChunk>();
        var chunkIndex = 0;
        var currentLine = 1; // Start after header
        
        await _dataLakeAdapter.ChangeFileSystem(BatchFileService.FILE_SYSTEM_NAME);
        using var stream = await _dataLakeAdapter.GetFileReadStreamAsync(batchFile.Directory, batchFile.FileName);
        using var reader = new StreamReader(stream);
        
        // Skip header
        await reader.ReadLineAsync();
        
        while (!reader.EndOfStream)
        {
            var startLine = currentLine;
            var recordsInChunk = 0;
            
            // Read target number of records, ensuring complete record boundaries
            while (recordsInChunk < targetRecordsPerChunk && !reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (line == null) break;
                
                currentLine++;
                recordsInChunk++;
                
                // Check for embedded newlines (bad CSV)
                if (line.Contains('\n') || line.Contains('\r'))
                {
                    Log.Warning("SplitFileIntoChunksAsync: Bad CSV detected - embedded newlines in line {LineNumber}, skipping", currentLine - 1);
                    // Continue processing other records
                }
            }
            
            if (recordsInChunk > 0)
            {
                var endLine = currentLine - 1; // -1 because we incremented currentLine
                var chunkFileName = $"{Path.GetFileNameWithoutExtension(batchFile.FileName)}_chunk_{chunkIndex:D4}{Path.GetExtension(batchFile.FileName)}";
                
                chunks.Add(new FileChunk(startLine, endLine, chunkFileName));
                
                Log.Debug("SplitFileIntoChunksAsync: Created chunk {ChunkIndex} with records {StartLine}-{EndLine} ({RecordsInChunk} records)",
                    chunkIndex, startLine, endLine, recordsInChunk);
                
                chunkIndex++;
            }
        }
        
        return chunks;
    }

    private BatchJob CreateChunkJob(BatchJob originalJob, FileChunk chunk, int chunkIndex, int totalChunks)
    {
        var chunkJob = new BatchJob(
            BatchJobStatusStrings.PENDING,
            originalJob.BatchFileId,
            originalJob.Tenancy,
            $"{originalJob.FileFriendlyName}_chunk_{chunkIndex:D4}",
            originalJob.Path,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            originalJob.TenantId,
            null,
            null,
            null,
            originalJob.Id, // ParentBatchJobId
            chunkIndex,
            totalChunks,
            chunk.StartLine, // ChunkStartLine
            chunk.EndLine, // ChunkEndLine
            true, // IsChunk
            null, // ProcessedBytes
            0, // ProcessedLines - start with 0 lines processed
            null // LastProcessedAt
        );

        return chunkJob;
    }

    /// <summary>
    /// Reads the result file for a chunk job and returns all result line DTOs.
    /// This method is used for generating comprehensive reports of chunk processing results.
    /// </summary>
    /// <param name="chunkJob">The chunk batch job</param>
    /// <returns>List of result line DTOs from the chunk's result file</returns>
    public async Task<List<BatchResultFileLineDto>> ReadChunkResultFileAsync(BatchJob chunkJob)
    {
        if (!chunkJob.IsChunk || chunkJob.ChunkIndex == null)
        {
            Log.Warning("Attempted to read chunk result file for non-chunk job {JobId}", chunkJob.Id);
            return new List<BatchResultFileLineDto>();
        }

        try
        {
            await _dataLakeAdapter.ChangeFileSystem("output"); // Result files are in output directory

            var resultFileName = ChunkNamingUtility.BuildChunkResultFileName(chunkJob.FileFriendlyName, (int)chunkJob.ChunkIndex);

            // Check if file exists
            var fileExists = await _dataLakeAdapter.FileExistsAsync(chunkJob.Path, resultFileName);
            if (!fileExists)
            {
                Log.Debug("Result file {ResultFileName} does not exist for chunk job {JobId}", resultFileName, chunkJob.Id);
                return new List<BatchResultFileLineDto>();
            }

            using var stream = await _dataLakeAdapter.GetFileReadStreamAsync(chunkJob.Path, resultFileName);
            using var reader = new StreamReader(stream);

            var results = new List<BatchResultFileLineDto>();
            string? line;
            int lineNumber = 0;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                lineNumber++;

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    var resultDto = JsonSerializer.Deserialize<BatchResultFileLineDto>(line, JsonUtility.GetDefaultOptions());
                    if (resultDto != null)
                    {
                        results.Add(resultDto);
                    }
                }
                catch (JsonException jsonEx)
                {
                    Log.Warning("Failed to parse result line {LineNumber} for chunk job {JobId}: {Error}",
                        lineNumber, chunkJob.Id, jsonEx.Message);
                    // Continue processing other lines
                }
            }

            Log.Debug("Successfully read {ResultCount} results from chunk {ChunkIndex} result file {ResultFileName}",
                results.Count, chunkJob.ChunkIndex, resultFileName);

            return results;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error reading chunk result file for job {JobId}", chunkJob.Id);
            return new List<BatchResultFileLineDto>();
        }
    }

}

public class FileChunk
{
    public int StartLine { get; }
    public int EndLine { get; }
    public string FileName { get; }

    public FileChunk(int startLine, int endLine, string fileName)
    {
        StartLine = startLine;
        EndLine = endLine;
        FileName = fileName;
    }
} 