using Journeys.Core.Interfaces.FileStorage;
using Journeys.Core.Models;
using System.Text.Json;
using Serilog;

namespace Journeys.Core.Services.Ingest;

public class ResumabilityService
{
    private readonly IDataLakeAdapter _dataLakeAdapter;

    public ResumabilityService(IDataLakeAdapter dataLakeAdapter)
    {
        _dataLakeAdapter = dataLakeAdapter;
    }

    /// <summary>
    /// Checks if a batch job has already been processed by examining the output file
    /// </summary>
    public async Task<ResumeInfo> CheckResumeInfoAsync(BatchJob batchJob, BatchResultFile batchResultFile)
    {
        try
        {
            // Check if output file exists and has content
            if (!await _dataLakeAdapter.FileExistsAsync(batchResultFile.Directory, batchResultFile.FileName, batchJob.Tenancy))
            {
                return new ResumeInfo { CanResume = false, ProcessedLines = 0 };
            }

            // Get file size to check if it has content
            var fileSize = await _dataLakeAdapter.GetFileSizeAsync(batchResultFile.Directory, batchResultFile.FileName, batchJob.Tenancy);
            if (fileSize == 0)
            {
                return new ResumeInfo { CanResume = false, ProcessedLines = 0 };
            }

            // Count existing lines in the output file
            var processedLines = await CountLinesInOutputFileAsync(batchResultFile, batchJob.Tenancy);
            
            return new ResumeInfo 
            { 
                CanResume = true, 
                ProcessedLines = processedLines,
                OutputFileExists = true
            };
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error checking resume info for batch job {BatchJobId}, will start fresh", batchJob.Id);
            return new ResumeInfo { CanResume = false, ProcessedLines = 0 };
        }
    }

    /// <summary>
    /// Counts the number of lines in the output file
    /// </summary>
    private async Task<int> CountLinesInOutputFileAsync(BatchResultFile batchResultFile, string tenancy)
    {
        try
        {
            await using var stream = await _dataLakeAdapter.GetFileReadStreamAsync(batchResultFile.Directory, batchResultFile.FileName, tenancy);
            if (stream == null) return 0;

            using var reader = new StreamReader(stream);
            var lineCount = 0;
            while (await reader.ReadLineAsync() != null)
            {
                lineCount++;
            }
            return lineCount;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error counting lines in output file {FileName}", batchResultFile.FileName);
            return 0;
        }
    }

    /// <summary>
    /// Updates the progress tracking fields on a batch job
    /// </summary>
    public async Task UpdateProgressAsync(BatchJob batchJob, int processedLines, long processedBytes)
    {
        try
        {
            batchJob.ProcessedLines = processedLines;
            batchJob.ProcessedBytes = processedBytes;
            batchJob.LastProcessedAt = DateTimeOffset.UtcNow;
            batchJob.LastUpdated = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to update progress tracking for batch job {BatchJobId}", batchJob.Id);
            throw; // Re-throw to let caller handle it
        }
    }
}

public class ResumeInfo
{
    public bool CanResume { get; set; }
    public int ProcessedLines { get; set; }
    public bool OutputFileExists { get; set; }
}
