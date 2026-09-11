using System.Text.Json;
using Journeys.Core.Models;

namespace Journeys.Core.Interfaces.Services;

public interface IBatchJobService
{
    Task<BatchJob> GetJobAsync(string tenantId, string batchFileId, string jobId);
    Task<PagedResultSet<BatchJob>> GetBatchFileJobsAsync(string tenantId, string batchFileId, int pageSize = 1000, string continuationToken = null);

    //Task<BatchJob?> ClaimJobAsync(string tenantId, string claimant, string batchFileId, string? jobId = null);
    Task<BatchJob?> ClaimPendingJobAsync(BatchJob unclaimedJob, string claimant);
    Task InitializeJobContext(BatchJob batchJob);

    Task<BatchJob> ReprocessJobAsync(BatchJob job);

    IAsyncEnumerable<JsonElement> ReadGroupAsync();
    string GetModelName();
    Task ProcessBatchJobWithIngestServiceAsync(BatchJob batchJob, Func<JsonElement, Task> processGroup);
    DropboxConfig? GetDropboxConfig();
    Task<BatchJob> UpsertBatchJobAsync(BatchJob batchJob);
}
