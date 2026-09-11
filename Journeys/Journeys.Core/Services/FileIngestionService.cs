

using Backend.Dto.Requests;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.FileStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.DTO.Models;
using Journeys.DTO.Requests;
using Journeys.DTO.Responses;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Services
{
    public class FileIngestionService : IFileIngestionService
    {
        private readonly ILogger<FileIngestionService> _logger;
        private readonly IDynamicDataAdapter _dynamicDataAdapter;
        private readonly IFileStorageAdapter _fileStorageAdapter;
        private readonly IFileIngestionAdapter _fileIngestionAdapter;

        private const string BATCH_JOB_MODEL_ID = "5aaeb5ae-518f-4b29-8a9a-c77f0dbd747f";
        private const string BATCH_FILE_MODEL_ID = "df37876d-4079-434a-8e2a-fb710faa8255";
        public FileIngestionService(ILogger<FileIngestionService> logger, IDynamicDataAdapter dynamicDataAdapter, IFileStorageAdapter fileStorageAdapter, IFileIngestionAdapter fileIngestionAdapter)
        {
            _logger = logger;
            _dynamicDataAdapter = dynamicDataAdapter;
            _fileStorageAdapter = fileStorageAdapter;
            _fileIngestionAdapter = fileIngestionAdapter;
        }

        public async Task<PagedResultSetResponse<FileSummaryDto>> GetFiles(string tenantId, Dictionary<string, object> parameters, int pageSize, string? continuationToken = null)
        {
            return await _fileStorageAdapter.GetFileAsync(tenantId, parameters,pageSize,continuationToken);
        }
        public async Task<FileIngestionSummaryDto> GetChunksSummaryByFileName(string tenantId, string folderName, string fileName)
        {
            try
            {
                return await _fileIngestionAdapter.GetChunkSummaryFromFileNameAsync(tenantId, folderName, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
        }

        public async Task<List<string>> GetFolders(string tenantId)
        {
            return await _fileStorageAdapter.GetFoldersAsync(tenantId);
        }

        public async Task<Dictionary<string, FileSummaryDto>> GetFileSummaryQuery(
         string tenantId, string folder, List<string> fileNames)
        {
            var request = new GetByQueryRequest();

            var tasks = fileNames.Select(async fileName =>
            {
                string fileNameLower = fileName.ToLowerInvariant();
                string batchFileQuery = $"STARTSWITH(c.originalfilename, '{fileNameLower}')";

                var batchFileTask = _dynamicDataAdapter.QueryEntitiesAsync<BatchFile>(
                    tenantId,
                    BATCH_FILE_MODEL_ID,
                    batchFileQuery,
                    parameters: request.Parameters,
                    sortBy: request.SortBy,
                    sortOrder: request.SortOrder,
                    pageSize: 10,
                    token: default(CancellationToken),
                    continuationToken: request.ContinuationToken,
                    includeChildModels: false
                );

                var batchJobQuery = $"STARTSWITH(c.filefriendlyname, '{fileNameLower}')";

                var batchJobsTask = _dynamicDataAdapter.QueryEntitiesAsync<BatchJob>(
                    tenantId,
                    BATCH_JOB_MODEL_ID,
                    batchJobQuery,
                    parameters: request.Parameters,
                    sortBy: request.SortBy,
                    sortOrder: request.SortOrder,
                    pageSize: 10000,
                    token: default(CancellationToken),
                    continuationToken: request.ContinuationToken,
                    includeChildModels: false
                );

                var hasChunksTask = _fileIngestionAdapter.HasChunksAsync(tenantId, folder, fileName);

                await Task.WhenAll(batchFileTask, batchJobsTask, hasChunksTask);

                var batchFile = batchFileTask.Result;
                var batchJobs = batchJobsTask.Result;
                var hasChunks = hasChunksTask.Result;

                var entities = batchJobs.Entities;
                // evaluate status
                string status = EvaluateBatchFileStatus(entities, hasChunks);

                var totalRows = entities.Any() ? entities.Max(b => b.ChunkEndLine) : 0;
                var totalProcessedLines = entities.Any() ? entities.Sum(b => b.ProcessedLines) : 0;
                var totalErrors = entities.Any() ? entities.Sum(b => b.TotalErrors ?? 0) : 0;
                var batchFileEntity = batchFile.Entities?.FirstOrDefault();
                DateTimeOffset? fileProcessingStarted = batchFileEntity?.CreateDate;
                DateTimeOffset? fileProcessingFinished = null;

                if (status.Equals(BatchJobStatusStrings.COMPLETE, StringComparison.OrdinalIgnoreCase) && entities.Any())
                {
                    fileProcessingFinished = entities.Max(b => b.LastUpdated);
                }

                return new KeyValuePair<string, FileSummaryDto>(
                    fileNameLower,
                    new FileSummaryDto
                    {
                        Status = status,
                        TotalRows = totalRows,
                        DateFileProcessingStarted = fileProcessingStarted,
                        DateFileProcessingFinished = fileProcessingFinished,
                        TotalErrors = totalErrors,
                        TotalProcessedLines = totalProcessedLines
                    });
            });

            var results = await Task.WhenAll(tasks);

            return results.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        }

        public async Task<FileResponse> DownloadFile(string tenantId, string folder, string fileName)
        {

            return await _fileStorageAdapter.DownloadBlobAsync(tenantId, folder, fileName);
        }

        public async Task<FileResponse> MergeOutputFiles(string tenantId, string folder, string fileName)
        {

            return await _fileIngestionAdapter.MergeOutputFiles(tenantId, folder, fileName);
        }
        private string EvaluateBatchFileStatus(IEnumerable<BatchJob> jobs, bool hasChunks)
        {
            if (!jobs.Any() && hasChunks)
                return BatchJobStatusStrings.COMPLETE;

            if (!jobs.Any() && !hasChunks)
                return BatchJobStatusStrings.PENDING;

            if (jobs.All(j => j.Status == BatchJobStatusStrings.COMPLETE))
                return BatchJobStatusStrings.COMPLETE;

            if (jobs.All(j => j.Status != BatchJobStatusStrings.COMPLETE))
                return BatchJobStatusStrings.FAILED;

            return BatchJobStatusStrings.PROCESSING;
        }
        
    }
}
