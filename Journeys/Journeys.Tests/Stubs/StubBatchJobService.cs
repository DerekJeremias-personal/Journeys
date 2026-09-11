using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using System.Text.Json;
using System.Linq;

namespace Journeys.Tests.Stubs;

public class StubBatchJobService : IBatchJobService
{
    public Task<PagedResultSet<BatchJob>> GetBatchFileJobsAsync(string tenantId, string batchFileId, int pageSize = 1000, string continuationToken = null)
    {
        return Task.FromResult((PagedResultSet<BatchJob>)null);
    }

    public Task<BatchJob> ClaimPendingJobAsync(string tenantId, string claimant, string? jobId = null)
    {
        return Task.FromResult<BatchJob>(null!);
    }

    public Task<BatchJob> ClaimPendingJobAsync(BatchJob unclaimedJob, string claimant)
    {
        return Task.FromResult(unclaimedJob);
    }

    public IAsyncEnumerable<JsonElement> ReadGroupAsync()
    {
        return AsyncEnumerable.Empty<JsonElement>();
    }

    public string GetModelName()
    {
        return "test-model";
    }

    public Task ProcessBatchJobWithIngestServiceAsync(BatchJob batchJob, Func<JsonElement, Task> processGroup)
    {
        return Task.CompletedTask;
    }

    public DropboxConfig? GetDropboxConfig()
    {
        throw new NotImplementedException();
    }

    public Task<BatchJob> UpsertBatchJobAsync(BatchJob batchJob)
    {
        return Task.FromResult(batchJob);
    }

    public Task<BatchJob> GetJobAsync(string tenantId, string batchFileId, string jobId)
    {
        throw new NotImplementedException();
    }

    public Task<BatchJob> ReprocessJobAsync(BatchJob job)
    {
        throw new NotImplementedException();
    }

    public Task<BatchJob?> ClaimJobAsync(string tenantId, string batchFileId, string jobId, string claimant)
    {
        return Task.FromResult<BatchJob?>(null);
    }

    public Task InitializeJobContext(BatchJob batchJob)
    {
        throw new NotImplementedException();
    }
} 