using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;

namespace Journeys.DAL.Adapters;

public class BatchResultFileAdapter(IDynamicDataAdapter dataAdapter) : IBatchResultFileAdapter
{
    private const string MODEL_ID = "8ddfd33a-3e2e-48a5-a906-03d5d1184e7c";

    public async Task<BatchResultFile?> FetchEntityAsync(string tenantId, string batchResultFileId)
    {
        return await dataAdapter.GetEntityAsync<BatchResultFile>(tenantId, batchResultFileId, MODEL_ID);
    }

    public async Task<BatchResultFile> UpsertEntityAsync(BatchResultFile batchResultFile)
    {
        batchResultFile.ModelId = MODEL_ID;

        return await dataAdapter.SetEntityAsync(batchResultFile.TenantId, batchResultFile, MODEL_ID, typeof(BatchResultFile));
    }
}
