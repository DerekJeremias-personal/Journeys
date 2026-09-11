using Journeys.Core.Models;

namespace Journeys.Core.Interfaces.DataStorage;

public interface IBatchFileAdapter
{
    Task<BatchFile?> FetchBatchFileAsync(string tenantId, string batchFileId);
    Task<BatchFile> UpsertBatchFileAsync(BatchFile batchFile);
    Task DeleteBatchFileAsync(string tenantId, string batchFileId);
    
    /// <summary>
    /// Finds a BatchFile by its natural key (tenantId, originalFileName, jobDirectory).
    /// Used for idempotency checks to prevent duplicate file processing.
    /// </summary>
    Task<BatchFile?> FindBatchFileByNaturalKeyAsync(string tenantId, string originalFileName, string jobDirectory);
}
