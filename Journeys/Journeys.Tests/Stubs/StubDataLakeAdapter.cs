using Journeys.Core.Interfaces.FileStorage;
using System.Text;

namespace Journeys.Tests.Stubs;

public class StubDataLakeAdapter : IDataLakeAdapter
{
    public Task CreateDirectoryAsync(string directoryName, string? fileSystemName = null)
    {
        return Task.CompletedTask;
    }

    public Task DeleteDirectoryAsync(string directoryName, string? fileSystemName = null)
    {
        return Task.CompletedTask;
    }

    public Task UploadFileAsync(string directoryName, string fileName, Stream fileStream, string? fileSystemName = null)
    {
        return Task.CompletedTask;
    }

    public Task<Stream> GetFileWriteStreamAsync(string directoryName, string fileName, string? fileSystemName = null, bool append = false)
    {
        var stream = new MemoryStream();
        return Task.FromResult<Stream>(stream);
    }

    public Task<Stream> GetFileReadStreamAsync(string directoryName, string fileName, string? fileSystemName = null)
    {
        // Try to read the actual test file if it exists
        var testDataPath = Path.Combine(directoryName, "TestData", fileName);
            
        if (File.Exists(testDataPath))
        {
            var fileStream = File.OpenRead(testDataPath);
            return Task.FromResult<Stream>(fileStream);
        }
        
        // Fallback to mock data if file doesn't exist
        var testData = "test,data,here\nline1,value1,value2\nline2,value3,value4";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(testData));
        return Task.FromResult<Stream>(stream);
    }

    public Task CreateFileAsync(string directoryName, string fileName, string? fileSystemName = null)
    {
        return Task.CompletedTask;
    }

    public Task<Stream> DownloadFileAsync(string directoryName, string fileName, string? fileSystemName = null)
    {
        var testData = "test,data,here\nline1,value1,value2\nline2,value3,value4";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(testData));
        return Task.FromResult<Stream>(stream);
    }

    public Task<Stream> DownloadFileAsync(string directoryName, string fileName, long start, long? length = null, string? fileSystemName = null)
    {
        var testData = "test,data,here\nline1,value1,value2\nline2,value3,value4";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(testData));
        return Task.FromResult<Stream>(stream);
    }

    public Task<Stream> QueryFileAsync(string directoryName, string fileName, string query, string? fileSystemName = null)
    {
        var testData = "test,data,here\nline1,value1,value2\nline2,value3,value4";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(testData));
        return Task.FromResult<Stream>(stream);
    }

    public Task DeleteFileAsync(string directoryName, string fileName, string? fileSystemName = null)
    {
        return Task.CompletedTask;
    }

    public Task ChangeFileSystem(string? fileSystemName = null)
    {
        return Task.CompletedTask;
    }

    public Task<bool> FileExistsAsync(string directoryName, string fileName, string? fileSystemName = null)
    {
        var testDataPath = Path.Combine(directoryName, "TestData", fileName);
        return Task.FromResult(File.Exists(testDataPath));
    }

    public Task<long> GetFileSizeAsync(string directoryName, string fileName, string? fileSystemName = null)
    {
        var testDataPath = Path.Combine(directoryName, "TestData", fileName);
        if (File.Exists(testDataPath))
        {
            var fileInfo = new FileInfo(testDataPath);
            return Task.FromResult(fileInfo.Length);
        }
        
        // Return different sizes based on filename for testing
        if (fileName.Contains("small"))
        {
            return Task.FromResult(500L * 1024); // 500KB for small files
        }
        else if (fileName.Contains("LargeFileTest"))
        {
            return Task.FromResult(3L * 1024 * 1024); // 3MB for large files to trigger chunking
        }
        else
        {
            return Task.FromResult(500L * 1024); // Default to small size
        }
    }

    Task<string> IDataLakeAdapter.FileSasUriAsync(string directoryName, string fileName, string? fileSystemName, int expirationHours = 24)
    {
        return Task.FromResult("");
    }
} 