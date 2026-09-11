namespace Journeys.Core.Interfaces.FileStorage;

public interface IDataLakeAdapter
{
    Task CreateDirectoryAsync(string directoryName, string? fileSystemName = null);
    Task DeleteDirectoryAsync(string directoryName, string? fileSystemName = null);
    Task UploadFileAsync(string directoryName, string fileName, Stream fileStream, string? fileSystemName = null);
    Task<Stream> GetFileWriteStreamAsync(string directoryName, string fileName, string? fileSystemName = null, bool append = false);
    Task<Stream> GetFileReadStreamAsync(string directoryName, string fileName, string? fileSystemName = null);
    Task CreateFileAsync(string directoryName, string fileName, string? fileSystemName = null);
    Task<Stream> DownloadFileAsync(string directoryName, string fileName, string? fileSystemName = null);
    Task<Stream> DownloadFileAsync(string directoryName, string fileName, long start, long? length = null, string? fileSystemName = null);
    Task<Stream> QueryFileAsync(string directoryName, string fileName, string query, string? fileSystemName = null);
    Task DeleteFileAsync(string directoryName, string fileName, string? fileSystemName = null);
    //void ChangeFileSystem(string? fileSystemName = null);
    Task ChangeFileSystem(string? fileSystemName = null);
    Task<bool> FileExistsAsync(string directoryName, string fileName, string? fileSystemName = null);
    Task<long> GetFileSizeAsync(string directoryName, string fileName, string? fileSystemName = null);
    Task<string> FileSasUriAsync(string directoryName, string fileName, string? fileSystemName = null, int expirationHours = 24);
}
