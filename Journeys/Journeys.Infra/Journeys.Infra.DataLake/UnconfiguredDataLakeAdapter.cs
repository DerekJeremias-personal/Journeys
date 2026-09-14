using Journeys.Core.Interfaces.FileStorage;

namespace Journeys.Infra.DataLake;

/// <summary>
/// Registered when <c>DataLake:ConnectionString</c> is unset so the host can start.
/// Account delete can skip a missing report file; other Data Lake calls fail at the call site.
/// </summary>
public sealed class UnconfiguredDataLakeAdapter : IDataLakeAdapter
{
    internal const string NotConfiguredMessage =
        "Azure Data Lake is not configured. Set DataLake:ConnectionString via user secrets, environment variables, or appsettings.Local.json.";

    private static InvalidOperationException NotConfigured() => new(NotConfiguredMessage);

    public Task ChangeFileSystem(string? fileSystemName = null) => Task.CompletedTask;

    public Task<bool> FileExistsAsync(string directoryName, string fileName, string? fileSystemName = null) =>
        Task.FromResult(false);

    public Task DeleteFileAsync(string directoryName, string fileName, string? fileSystemName = null) =>
        Task.CompletedTask;

    public Task CreateDirectoryAsync(string directoryName, string? fileSystemName = null) =>
        throw NotConfigured();

    public Task DeleteDirectoryAsync(string directoryName, string? fileSystemName = null) =>
        throw NotConfigured();

    public Task UploadFileAsync(string directoryName, string fileName, Stream fileStream, string? fileSystemName = null) =>
        throw NotConfigured();

    public Task<Stream> GetFileWriteStreamAsync(string directoryName, string fileName, string? fileSystemName = null, bool append = false) =>
        throw NotConfigured();

    public Task<Stream> GetFileReadStreamAsync(string directoryName, string fileName, string? fileSystemName = null) =>
        throw NotConfigured();

    public Task CreateFileAsync(string directoryName, string fileName, string? fileSystemName = null) =>
        throw NotConfigured();

    public Task<Stream> DownloadFileAsync(string directoryName, string fileName, string? fileSystemName = null) =>
        throw NotConfigured();

    public Task<Stream> DownloadFileAsync(string directoryName, string fileName, long start, long? length = null, string? fileSystemName = null) =>
        throw NotConfigured();

    public Task<Stream> QueryFileAsync(string directoryName, string fileName, string query, string? fileSystemName = null) =>
        throw NotConfigured();

    public Task<long> GetFileSizeAsync(string directoryName, string fileName, string? fileSystemName = null) =>
        throw NotConfigured();

    public Task<string> FileSasUriAsync(string directoryName, string fileName, string? fileSystemName = null, int expirationHours = 24) =>
        throw NotConfigured();
}
