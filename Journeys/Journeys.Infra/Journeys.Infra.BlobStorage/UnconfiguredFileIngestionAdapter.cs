using Journeys.Core.Interfaces.FileStorage;
using Journeys.DTO.Models;
using Journeys.DTO.Responses;

namespace Journeys.Infra.BlobStorage;

/// <summary>
/// Registered when <c>DataLake:ConnectionString</c> is unset so the host can start.
/// File-ingestion blob operations fail at the call site.
/// </summary>
public sealed class UnconfiguredFileIngestionAdapter : IFileIngestionAdapter
{
    private static InvalidOperationException NotConfigured() =>
        new(UnconfiguredFileStorageAdapter.NotConfiguredMessage);

    public Task<FileIngestionSummaryDto> GetChunkSummaryFromFileNameAsync(string tenant, string folder, string targetFileName) =>
        throw NotConfigured();

    public Task<FileResponse> MergeOutputFiles(string tenantId, string folder, string fileName) =>
        throw NotConfigured();

    public Task<bool> HasChunksAsync(string tenant, string folder, string targetFileName) =>
        throw NotConfigured();
}
