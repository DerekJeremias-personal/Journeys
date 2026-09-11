using Journeys.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Journeys.Core.Caching;

/// <summary>
/// Cache service for DropboxConfig with hybrid memory + distributed caching.
/// Supports cache bypass for UI/testing scenarios.
/// </summary>
public interface IDropboxConfigCache
{
    /// <summary>
    /// Gets a DropboxConfig for a specific batch file by matching ModelName, FileType, and Directory.
    /// </summary>
    Task<DropboxConfig?> GetDropboxConfigForBatchFileAsync(BatchFile batchFile, bool bypassCache = false);

    /// <summary>
    /// Gets a DropboxConfig for a specific directory.
    /// </summary>
    Task<DropboxConfig?> GetDropboxConfigForDirectoryAsync(string tenantId, string directory, bool bypassCache = false);

    /// <summary>
    /// Gets all DropboxConfigs for a tenant (for controller/UI use).
    /// </summary>
    Task<List<DropboxConfig>?> GetAllDropboxConfigsAsync(string tenantId, bool bypassCache = false);

    /// <summary>
    /// Invalidates cache for a specific DropboxConfig or all configs for a tenant.
    /// </summary>
    Task InvalidateDropboxConfigAsync(string tenantId, string? dropboxConfigId = null);

    /// <summary>
    /// Invalidates all DropboxConfig caches for a tenant.
    /// </summary>
    Task InvalidateTenantDropboxConfigsAsync(string tenantId);
}

