using Journeys.Core.Models;
using Journeys.DTO.Models;
using Journeys.DTO.Responses;
using Journeys.DTO.Responses.BlobResponses;
using Serilog;

namespace Journeys.Core.Services.Ingest;

/// <summary>
/// Service for aggregating batch job results into comprehensive IngestFileResults.
/// Handles both regular jobs and chunk jobs, providing detailed success/failure statistics.
/// </summary>
public class ResultsAggregator
{
    private readonly ConcurrencyController _concurrencyController;
    private readonly ILogger _logger;

    public ResultsAggregator(ConcurrencyController concurrencyController, ILogger logger)
    {
        _concurrencyController = concurrencyController;
        _logger = logger;
    }

    /// <summary>
    /// Aggregates results from multiple batch jobs into a comprehensive IngestFileResults.
    /// </summary>
    /// <param name="jobs">The batch jobs to aggregate</param>
    /// <param name="allResults">All result line DTOs from all jobs</param>
    /// <param name="tenantId">The tenant ID</param>
    /// <param name="batchFile">The original batch file</param>
    /// <returns>Comprehensive IngestFileResults</returns>
    public async Task<IngestFileResults> AggregateResultsAsync(List<BatchJob> jobs, List<BatchResultFileLineDto> allResults, string tenantId, BatchFile batchFile)
    {
        var response = new IngestFileResults
        {
            TenantId = tenantId,
            BatchFileId = batchFile.Id,
            Directory = $"{batchFile.Directory}",
            OriginalfileName = batchFile.OriginalFileName,
            Errors = new Dictionary<string, string>(),
            IngestFileChunkResults = new List<IngestFileChunkResult>(),
            IngestFileErrors = new List<IngestFileError>()
        };

        try
        {
            // Separate jobs into parent and chunk jobs
            var parentJobs = jobs.Where(j => !j.IsChunk).ToList();
            var chunkJobs = jobs.Where(j => j.IsChunk).ToList();

            Log.Debug("Aggregating results: {ParentJobCount} parent jobs, {ChunkJobCount} chunk jobs, {TotalResultCount} total results",
                parentJobs.Count, chunkJobs.Count, allResults.Count);

            // Process chunk jobs
            if (chunkJobs.Any())
            {
                await ProcessChunkJobsAsync(chunkJobs, allResults, response);
            }

            // Process parent jobs (if any - usually only when file is small enough for single chunk)
            if (parentJobs.Any())
            {
                ProcessParentJobs(parentJobs, allResults, response);
            }

            var minDt = response.IngestFileChunkResults.Select(x => x.ProcessDate).Min();
            var maxDt = response.IngestFileChunkResults.Select(x => x.ProcessDate).Max();
            var nameSplit = batchFile.OriginalFileName.Split(".");

            response.FileType = (nameSplit.Length > 1) ? nameSplit[1] : "unknown";
            response.ProcessDate = batchFile.LastUpdated;
            response.TotalFileLines = allResults.Count;
            response.TotalFileChunks = chunkJobs.Count;
            response.ErrorCount = allResults.Count(r => !r.Success);
            response.RunTimespanMinutes = (int)(maxDt - minDt).TotalMinutes;

            Log.Debug("Aggregation complete: {TotalChunkResults} chunk results, {TotalErrors} errors",
                response.IngestFileChunkResults.Count, response.IngestFileErrors.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during result aggregation");
            response.Errors.Add("AggregationError", $"Failed to aggregate results: {ex.Message}");
        }

        return response;
    }

    /// <summary>
    /// Processes chunk jobs and their results with concurrency control.
    /// </summary>
    private async Task ProcessChunkJobsAsync(List<BatchJob> chunkJobs, List<BatchResultFileLineDto> allResults, IngestFileResults response)
    {
        var chunkProcessingTasks = chunkJobs.OrderBy(j => j.ChunkIndex).Select(async chunkJob =>
        {
            return await _concurrencyController.ExecuteAsync(async () =>
            {
                try
                {
                    // Get results for this specific chunk
                    var chunkResults = allResults.Where(r => r.LineNumber >= (chunkJob.ChunkStartLine ?? 0) &&
                                                             r.LineNumber <= (chunkJob.ChunkEndLine ?? 0)).ToList();

                    var chunkResult = new IngestFileChunkResult
                    {
                        ChunkIndex = (int)(chunkJob.ChunkIndex ?? 0),
                        StartLineNumber = (int)(chunkJob.ChunkStartLine ?? 0),
                        EndLineNumber = (int)(chunkJob.ChunkEndLine ?? 0),
                        ProcessedLines = chunkResults.Count,
                        Status = GetChunkStatus(chunkJob, chunkResults),
                        BatchFileId = chunkJob.BatchFileId,
                        BatchJobId = chunkJob.Id,
                        OriginalfileName = chunkJob.FileFriendlyName,
                        ProcessDate = chunkJob.CreateDate,
                        ProcessedBytes = (decimal)(chunkJob.ProcessedBytes ?? 0)
                    };

                    // Add individual errors for this chunk
                    var chunkErrors = chunkResults.Where(r => !r.Success).ToList();
                    var errors = new List<IngestFileError>();
                    foreach (var error in chunkErrors)
                    {
                        errors.Add(new IngestFileError
                        {
                            OriginalFileLineNumber = error.LineNumber ?? 0,
                            LineKey = error.LineKey,
                            Error = string.Join("; ", error.Errors?.Values.ToList() ?? new List<string>()),
                            ErrorMessage = string.Join("; ", error.Errors?.Values.ToList() ?? new List<string>()),
                            LineData = error.LineKey
                        });
                    }

                    _logger.Debug("Processed chunk {ChunkIndex}: {ProcessedLines} lines processed, status: {Status}",
                        chunkResult.ChunkIndex, chunkResult.ProcessedLines, chunkResult.Status);

                    return (chunkResult, errors);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error processing chunk {ChunkIndex} for job {JobId}", chunkJob.ChunkIndex, chunkJob.Id);
                    throw new Exception($"Failed to process chunk {chunkJob.ChunkIndex}: {ex.Message}", ex);
                }
            });
        });

        var results = await Task.WhenAll(chunkProcessingTasks);

        foreach (var (chunkResult, errors) in results)
        {
            response.IngestFileChunkResults.Add(chunkResult);
            response.IngestFileErrors.AddRange(errors);
        }
    }

    /// <summary>
    /// Processes parent jobs (non-chunk jobs).
    /// </summary>
    private void ProcessParentJobs(List<BatchJob> parentJobs, List<BatchResultFileLineDto> allResults, IngestFileResults response)
    {
        foreach (var parentJob in parentJobs)
        {
            try
            {
                // For parent jobs, we might not have specific line ranges, so we'll process all results
                var parentResults = allResults; // This might need refinement based on your data structure

                var chunkResult = new IngestFileChunkResult
                {
                    ChunkIndex = 0, // Parent job is chunk 0
                    StartLineNumber = 1,
                    EndLineNumber = parentResults.Count,
                    ProcessedLines = parentResults.Count,
                    Status = GetChunkStatus(parentJob, parentResults),
                    BatchFileId = parentJob.BatchFileId,
                    BatchJobId = parentJob.Id,
                    OriginalfileName = parentJob.FileFriendlyName,
                    ProcessDate = parentJob.CreateDate,
                    ProcessedBytes = (decimal)(parentJob.ProcessedBytes ?? 0)
                };

                // Add individual errors
                var parentErrors = parentResults.Where(r => !r.Success).ToList();
                foreach (var error in parentErrors)
                {
                    response.IngestFileErrors.Add(new IngestFileError
                    {
                        OriginalFileLineNumber = error.LineNumber ?? 0,
                        LineKey = error.LineKey,
                        Error = string.Join("; ", error.Errors?.Values.ToList() ?? new List<string>()),
                        ErrorMessage = string.Join("; ", error.Errors?.Values.ToList() ?? new List<string>()),
                        LineData = error.LineKey
                    });
                }

                response.IngestFileChunkResults.Add(chunkResult);

                Log.Debug("Processed parent job: {ProcessedLines} lines processed, status: {Status}",
                    chunkResult.ProcessedLines, chunkResult.Status);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing parent job {JobId}", parentJob.Id);
                response.Errors.Add($"ParentJobProcessingError_{parentJob.Id}",
                    $"Failed to process parent job: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Calculates overall statistics for the entire batch.
    /// </summary>
    private void CalculateOverallStatistics(List<BatchResultFileLineDto> allResults, IngestFileResults response)
    {
        var totalLines = allResults.Count;
        var successfulLines = allResults.Count(r => r.Success);
        var failedLines = allResults.Count(r => !r.Success);

        Log.Information("Overall batch statistics: {SuccessfulLines}/{TotalLines} successful ({SuccessRate:P2}), {FailedLines} failed",
            successfulLines, totalLines, totalLines > 0 ? (double)successfulLines / totalLines : 0, failedLines);

        // Add overall statistics to errors dictionary for easy access
        response.Errors.Add("_TotalLines", totalLines.ToString());
        response.Errors.Add("_SuccessfulLines", successfulLines.ToString());
        response.Errors.Add("_FailedLines", failedLines.ToString());
        response.Errors.Add("_SuccessRate", totalLines > 0 ? ((double)successfulLines / totalLines).ToString("P2") : "0%");
    }

    /// <summary>
    /// Determines the status of a chunk based on the job and its results.
    /// </summary>
    private string GetChunkStatus(BatchJob job, List<BatchResultFileLineDto> results)
    {
        if (job.Status == BatchJobStatusStrings.COMPLETE)
        {
            return "Completed";
        }
        else if (job.Status == BatchJobStatusStrings.PROCESSING)
        {
            return "Processing";
        }
        else if (job.Status == BatchJobStatusStrings.FAILED)
        {
            return "Failed";
        }
        else if (results.Any())
        {
            return "Partial";
        }
        else
        {
            return "Pending";
        }
    }

}
