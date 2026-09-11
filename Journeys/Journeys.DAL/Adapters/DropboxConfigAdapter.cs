using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;

namespace Journeys.DAL.Adapters;

public class DropboxConfigAdapter(IDynamicDataAdapter dataAdapter) : IDropboxConfigAdapter
{
    private const string MODEL_ID = "2e131363-1833-48c0-8712-7e9b7c3f58d4";

    public async Task<List<DropboxConfig>?> FetchAllDropboxConfigsAsync(string tenantId)
    {
        var results = await dataAdapter.GetAllEntitiesAsync<DropboxConfig>(tenantId, MODEL_ID, 250);

        return results.Entities;
    }

    public async Task<DropboxConfig?> FetchDropboxConfigAsync(string tenantId, string dropboxConfigId)
    {
        return await dataAdapter.GetEntityAsync<DropboxConfig>(tenantId, dropboxConfigId, MODEL_ID);
    }

    public async Task<DropboxConfig?> FetchDropboxConfigForDirectoryAsync(string tenantId, string directory)
    {
        var resultSet = await dataAdapter.GetAllEntitiesAsync<DropboxConfig>(tenantId, MODEL_ID, 250);
        
        return resultSet.Entities?.Find(dropboxConfig => dropboxConfig.Directory.Equals(directory, StringComparison.InvariantCultureIgnoreCase));
    }

    public async Task<DropboxConfig?> FetchDropboxConfigForBatchFileAsync(BatchFile batchFile)
    {
        var resultSet = await dataAdapter.GetAllEntitiesAsync<DropboxConfig>(batchFile.TenantId, MODEL_ID, 250);
        
        return resultSet.Entities?.Find(dropboxConfig => 
            dropboxConfig.ModelName.Equals(batchFile.ModelName, StringComparison.InvariantCultureIgnoreCase)
            && dropboxConfig.FileType.Equals(Path.GetExtension(batchFile.FileName)[1..], StringComparison.InvariantCultureIgnoreCase)
            && dropboxConfig.Directory.Equals(batchFile.JobDirectory, StringComparison.InvariantCultureIgnoreCase)
        );
    }

    public async Task<DropboxConfig> UpsertDropboxConfigAsync(DropboxConfig dropboxConfig)
    {
        dropboxConfig.ModelId = MODEL_ID;
        dropboxConfig.Id ??= Guid.NewGuid().ToString();

        return await dataAdapter.SetEntityAsync(dropboxConfig.TenantId, dropboxConfig, MODEL_ID, typeof(DropboxConfig));
    }

    public async Task DeleteDropboxConfigAsync(string tenantId, string dropboxConfigId)
    {
        await dataAdapter.RemoveEntityAsync(tenantId, dropboxConfigId, MODEL_ID);
    }
}
