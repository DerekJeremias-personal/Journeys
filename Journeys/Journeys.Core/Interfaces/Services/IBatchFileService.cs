using Journeys.Core.Models;

namespace Journeys.Core.Interfaces.Services;

public interface IBatchFileService
{
    Task<(BatchFile, BatchJob)> CreateBatchFromFileStreamAsync(string tenancy, string tenantId, DropboxConfig dropboxConfig, string fileName, string path, Stream fileStream);
    Task<BatchFile?> GetBatchFileAsync(string tenantId, string batchFileId);
    
    /// <summary>
    /// Finds a BatchFile by its natural key (tenantId, originalFileName, jobDirectory).
    /// Used for idempotency checks to prevent duplicate file processing.
    /// </summary>
    Task<BatchFile?> FindBatchFileByNaturalKeyAsync(string tenantId, string originalFileName, string jobDirectory);
}
