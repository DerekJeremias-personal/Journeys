using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using Journeys.DTO.Requests;
using System.Collections.Generic;
using System.Linq;

namespace Journeys.DAL.Adapters;

public class BatchFileAdapter(IDynamicDataAdapter dataAdapter) : IBatchFileAdapter
{
    private const string MODEL_ID = "df37876d-4079-434a-8e2a-fb710faa8255";

    public async Task<BatchFile?> FetchBatchFileAsync(string tenantId, string batchFileId)
    {
        return await dataAdapter.GetEntityAsync<BatchFile>(tenantId, batchFileId, MODEL_ID);
    }

    public async Task<BatchFile> UpsertBatchFileAsync(BatchFile batchFile)
    {
        batchFile.ModelId = MODEL_ID;

        return await dataAdapter.SetEntityAsync(batchFile.TenantId.ToLower(), batchFile, MODEL_ID, typeof(BatchFile));
    }

    public async Task DeleteBatchFileAsync(string tenantId, string batchFileId)
    {
        await dataAdapter.RemoveEntityAsync(tenantId, batchFileId, MODEL_ID);
    }

    public async Task<BatchFile?> FindBatchFileByNaturalKeyAsync(string tenantId, string originalFileName, string jobDirectory)
    {
        // Query for existing BatchFile by natural key: tenantId, originalFileName, jobDirectory
        // Use case-insensitive comparison by normalizing to lowercase
        var normalizedTenantId = tenantId.ToLowerInvariant();
        var normalizedOriginalFileName = originalFileName.ToLowerInvariant();
        var normalizedJobDirectory = jobDirectory.ToLowerInvariant();

        // Cosmos DB query: exact match on all three natural key fields
        var query = "c.tenantId = @tenantId AND c.originalfilename = @originalFileName AND c.jobdirectory = @jobDirectory";
        
        var parameters = new Dictionary<string, object>
        {
            { "@tenantId", normalizedTenantId },
            { "@originalFileName", normalizedOriginalFileName },
            { "@jobDirectory", normalizedJobDirectory }
        };

        var result = await dataAdapter.QueryEntitiesAsync<BatchFile>(
            normalizedTenantId,
            MODEL_ID,
            query,
            parameters,
            sortBy: "createdate",
            sortOrder: SortOrder.DESC,
            pageSize: 1,
            token: default,
            continuationToken: null,
            serializerOptions: null,
            includeChildModels: false
        );

        // Return the most recent match (if multiple exist due to race conditions, take the first one created)
        return result?.Entities?.FirstOrDefault();
    }
}
