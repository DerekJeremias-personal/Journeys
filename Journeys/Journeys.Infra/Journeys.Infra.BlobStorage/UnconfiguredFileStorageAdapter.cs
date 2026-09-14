using Journeys.Core.Interfaces.FileStorage;
using Journeys.DTO.Models;
using Journeys.DTO.Responses;

namespace Journeys.Infra.BlobStorage;

/// <summary>
/// Registered when <c>DataLake:ConnectionString</c> is unset so the host can start.
/// Campaign-agent tool audit appends are dropped; other blob calls fail at the call site.
/// </summary>
public sealed class UnconfiguredFileStorageAdapter : IFileStorageAdapter
{
    internal const string NotConfiguredMessage =
        "Azure blob storage is not configured. Set DataLake:ConnectionString via user secrets, environment variables, or appsettings.Local.json.";

    private static InvalidOperationException NotConfigured() => new(NotConfiguredMessage);

    public Task AppendNdjsonLineAsync(string blobName, string line, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task UploadAsync(string blobName, Stream fileStream, string contentType) =>
        throw NotConfigured();

    public Task<Stream> DownloadAsync(string blobName) =>
        throw NotConfigured();

    public Task DeleteAsync(string blobName) =>
        throw NotConfigured();

    public Task<List<string>> GetFoldersAsync(string tenant) =>
        throw NotConfigured();

    public Task<PagedResultSetResponse<FileSummaryDto>> GetFileAsync(
        string tenant,
        Dictionary<string, object?> parameters,
        int pageSize,
        string? continuationToken = null) =>
        throw NotConfigured();

    public Task<FileResponse> DownloadBlobAsync(string tenantId, string folder, string fileName) =>
        throw NotConfigured();
}
