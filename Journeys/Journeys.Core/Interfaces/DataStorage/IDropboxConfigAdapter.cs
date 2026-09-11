using Journeys.Core.Models;

namespace Journeys.Core.Interfaces.DataStorage;

public interface IDropboxConfigAdapter
{
    Task<List<DropboxConfig>?> FetchAllDropboxConfigsAsync(string tenantId);
    Task<DropboxConfig?> FetchDropboxConfigAsync(string tenantId, string batchFileId);
    Task<DropboxConfig?> FetchDropboxConfigForDirectoryAsync(string tenantId, string directory);
    Task<DropboxConfig?> FetchDropboxConfigForBatchFileAsync(BatchFile batchFile);
    Task<DropboxConfig> UpsertDropboxConfigAsync(DropboxConfig dropboxConfig);
    Task DeleteDropboxConfigAsync(string tenantId, string dropboxConfigId);
}
