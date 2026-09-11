using Journeys.Core.Models;
using Journeys.DTO.Responses.BlobResponses;

namespace Journeys.Core.Interfaces.Services;

public interface IIngestService
{
    Task<BatchJob> GetJobAsync(string tenantId, string batchFileId, string batchJobId);
    //Task ClaimJobAsync(string tenantId, string batchFileId, string batchJobId);
    //Task ClaimJobAsync(string tenantId, BatchJob job);

    Task ReprocessBatchJob(string tenantId, string batchFileId, string batchJobId);

    Task<(DropboxConfig?, BatchJob?)> ImportBlobFromUriAsync(Uri blobUri);

    /// <summary>
    /// Creates batch jobs from a blob URI without processing them.
    /// Handles file chunking if needed and saves all jobs with PENDING status.
    /// Jobs will be processed by ChunkJobProcessor.
    /// </summary>
    /// <param name="blobUri">The URI of the blob to process</param>
    /// <returns>The number of batch jobs created (1 if not chunked, multiple if chunked)</returns>
    Task<int> CreateBatchJobsFromBlobUriAsync(Uri blobUri);


    Task RunJobAsync(BatchJob batchJob);

    Task<IngestFileResults> GetIngestResultsAsync(string tenantId, string batchFileId);

}
