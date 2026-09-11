using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;

namespace Journeys.Tests.Stubs;

public class StubBatchFileAdapter : IBatchFileAdapter
{
    public Task<BatchFile?> FetchBatchFileAsync(string tenantId, string batchFileId)
    {
        return Task.FromResult<BatchFile?>(null);
    }

    public Task<BatchFile> UpsertBatchFileAsync(BatchFile batchFile)
    {
        return Task.FromResult(batchFile);
    }

    public Task DeleteBatchFileAsync(string tenantId, string batchFileId)
    {
        return Task.CompletedTask;
    }

    public Task<BatchFile?> FindBatchFileByNaturalKeyAsync(string tenantId, string originalFileName, string jobDirectory)
    {
        // Stub implementation - returns null (no existing file found)
        return Task.FromResult<BatchFile?>(null);
    }
} 