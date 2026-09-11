using Journeys.Core.Models;

namespace Journeys.Core.Interfaces.DataStorage;

public interface IBatchResultFileAdapter
{
    Task<BatchResultFile?> FetchEntityAsync(string tenantId, string batchResultFileId);
    Task<BatchResultFile> UpsertEntityAsync(BatchResultFile batchResultFile);
}
