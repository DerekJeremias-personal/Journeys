using Journeys.Core.Interfaces.FileStorage;
using Journeys.Core.Models;
using Journeys.Core.Utilities;
using Journeys.Core.Utility;
using Journeys.DTO.Models;
using Serilog;
using System.Text.Json;

namespace Journeys.Core.Services.Ingest;

/// <summary>
/// Service for reading batch job result files with resilience against concurrent access.
/// Handles file locking, missing files, and corrupted data gracefully.
/// </summary>
public class ResultFileReader
{
    private readonly IDataLakeAdapter _dataLakeAdapter;
    private readonly int _maxRetries;
    private readonly TimeSpan _retryDelay;

    public ResultFileReader(IDataLakeAdapter dataLakeAdapter, int maxRetries = 3, TimeSpan? retryDelay = null)
    {
        _dataLakeAdapter = dataLakeAdapter;
        _maxRetries = maxRetries;
        _retryDelay = retryDelay ?? TimeSpan.FromMilliseconds(500);
    }

    /// <summary>
    /// Reads the result file for a regular (non-chunk) batch job.
    /// </summary>
    /// <param name="job">The batch job</param>
    /// <returns>List of result line DTOs, empty list if file doesn't exist or can't be read</returns>
    public async Task<List<BatchResultFileLineDto>> ReadResultFileAsync(BatchJob job)
    {
        var resultFileName = ChunkNamingUtility.BuildResultFileName(job.FileFriendlyName);
        return await ReadResultFileWithRetryAsync($"{job.Path}/output", resultFileName, job);
    }

    /// <summary>
    /// Reads the result file for a chunk batch job.
    /// </summary>
    /// <param name="chunkJob">The chunk batch job</param>
    /// <returns>List of result line DTOs, empty list if file doesn't exist or can't be read</returns>
    public async Task<List<BatchResultFileLineDto>> ReadChunkResultFileAsync(BatchJob chunkJob)
    {
        if (!chunkJob.IsChunk || chunkJob.ChunkIndex == null)
        {
            Log.Warning("Attempted to read chunk result file for non-chunk job {JobId}", chunkJob.Id);
            return new List<BatchResultFileLineDto>();
        }

        var path = $"{chunkJob.Path}/output";
        var resultFileName = ChunkNamingUtility.BuildChunkResultFileName(chunkJob.FileFriendlyName, (int)chunkJob.ChunkIndex);
        var results = await ReadResultFileWithRetryAsync(path, resultFileName, chunkJob);
        if (results?.Count == 0)
        {
            //TODO: remove this fallback asap
            //fall back to older naming convention
            resultFileName = ChunkNamingUtility.BuildChunkResultFileName(chunkJob.FileFriendlyName, (int)chunkJob.ChunkIndex, true);
            results = await ReadResultFileWithRetryAsync(path, resultFileName, chunkJob);
        }
        return results;
    }

    /// <summary>
    /// Reads a result file with retry logic to handle concurrent access.
    /// </summary>
    /// <param name="directory">The directory containing the result file</param>
    /// <param name="fileName">The result file name</param>
    /// <param name="jobId">The job ID for logging</param>
    /// <returns>List of result line DTOs</returns>
    private async Task<List<BatchResultFileLineDto>> ReadResultFileWithRetryAsync(string directory, string fileName, BatchJob job)
    {
        var results = new List<BatchResultFileLineDto>();
        var lastException = (Exception?)null;

        for (int attempt = 1; attempt <= _maxRetries; attempt++)
        {
            try
            {
                await _dataLakeAdapter.ChangeFileSystem($"{job.Tenancy}");

                // Check if file exists first
                var fileExists = await _dataLakeAdapter.FileExistsAsync(directory, fileName);
                if (!fileExists)
                {
                    Log.Debug("Result file {FileName} does not exist for job {JobId}", fileName, job.Id);
                    return results; // Return empty list for missing files
                }

                // Try to read the file
                using var stream = await _dataLakeAdapter.GetFileReadStreamAsync(directory, fileName);
                using var reader = new StreamReader(stream);

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
                        Log.Warning("Failed to parse result line {LineNumber} for job {JobId}: {Error}",
                            lineNumber, job.Id, jsonEx.Message);
                        // Continue processing other lines
                    }
                }

                Log.Debug("Successfully read {ResultCount} results from {FileName} for job {JobId}",
                    results.Count, fileName, job.Id);

                return results; // Success - return results
            }
            catch (Exception ex) when (IsRetryableException(ex))
            {
                lastException = ex;
                Log.Warning("Attempt {Attempt}/{MaxRetries} failed to read result file {FileName} for job {JobId}: {Error}",
                    attempt, _maxRetries, fileName, job.Id, ex.Message);

                if (attempt < _maxRetries)
                {
                    var delay = TimeSpan.FromMilliseconds(_retryDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                    Log.Debug("Retrying in {Delay}ms", delay.TotalMilliseconds);
                    await Task.Delay(delay);
                }
            }
            catch (Exception ex)
            {
                // Non-retryable exception
                Log.Error(ex, "Non-retryable error reading result file {FileName} for job {JobId}", fileName, job.Id);
                return results; // Return whatever we have so far
            }
        }

        // All retries failed
        Log.Error(lastException, "Failed to read result file {FileName} for job {JobId} after {MaxRetries} attempts",
            fileName, job.Id, _maxRetries);
        return results; // Return empty list on complete failure
    }

    /// <summary>
    /// Determines if an exception is retryable (e.g., file locked, temporary network issue).
    /// </summary>
    /// <param name="ex">The exception to check</param>
    /// <returns>True if the exception is retryable</returns>
    private static bool IsRetryableException(Exception ex)
    {
        return ex is IOException ||
               ex is UnauthorizedAccessException ||
               ex is TimeoutException ||
               (ex is InvalidOperationException && ex.Message.Contains("locked")) ||
               (ex is HttpRequestException && ex.Message.Contains("timeout"));
    }
}
