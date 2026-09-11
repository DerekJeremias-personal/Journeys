using Azure;
using Azure.Identity;
using Azure.Storage.Files.DataLake;
using Azure.Storage.Files.DataLake.Models;
using Azure.Storage.Sas;
using Journeys.Core.Interfaces.FileStorage;
using Microsoft.Extensions.Options;
using System.Net;

namespace Journeys.Infra.DataLake;

public class DataLakeAdapter : IDataLakeAdapter
{
    private readonly DataLakeServiceClient _dataLakeServiceClient;
    private DataLakeFileSystemClient _fileSystemClient;

    public DataLakeAdapter(IOptions<DataLakeAdapterConfig> options)
    {
        var config = options.Value;

        _dataLakeServiceClient = config.IsUri 
            ? new DataLakeServiceClient(new Uri(config.ConnectionString), new ManagedIdentityCredential()) 
            : new DataLakeServiceClient(config.ConnectionString);

        _fileSystemClient = _dataLakeServiceClient.GetFileSystemClient(config.DefaultFileSystem);
    }

    public async Task CreateDirectoryAsync(string directoryName, string? fileSystemName = null)
    {
        await ChangeFileSystem(fileSystemName);
        
        await _fileSystemClient.GetDirectoryClient(directoryName).CreateIfNotExistsAsync();
    }

    public async Task DeleteDirectoryAsync(string directoryName, string? fileSystemName = null)
    {
        await ChangeFileSystem(fileSystemName);
        
        await _fileSystemClient.DeleteDirectoryAsync(directoryName);
    }

    public async Task CreateFileAsync(string directoryName, string fileName, string? fileSystemName = null)
    {
        await ChangeFileSystem(fileSystemName);
        
        var directoryClient = _fileSystemClient.GetDirectoryClient(directoryName);
        var response = await directoryClient.CreateFileAsync(fileName);

        if (!response.HasValue)
        {
            throw new Exception($"File {fileName} could not be created");
        }
    }

    public async Task UploadFileAsync(string directoryName, string fileName, Stream fileStream, string? fileSystemName = null)
    {
        await ChangeFileSystem(fileSystemName);
        
        var fileClient = CreateFileClient(directoryName, fileName);
        var response = await fileClient.UploadAsync(fileStream);

        if (!response.HasValue)
        {
            throw new Exception($"File {fileName} not uploaded");
        }
    }

    public async Task<Stream> GetFileWriteStreamAsync(string directoryName, string fileName, string? fileSystemName = null, bool append = false)
    {
        await ChangeFileSystem(fileSystemName);

        var fileClient = CreateFileClient(directoryName, fileName);

        Stream writeStream;

        if (append)
        {
            // For append mode: if file exists, create a new uniquely named file instead of overwriting
            // This avoids ETag conflicts (412 errors) that occur when reading then rewriting
            var fileExists = await fileClient.ExistsAsync();
            if (fileExists.Value)
            {
                // File exists - create a new file with a unique name (timestamp suffix)
                // This preserves the original file and creates a new one for the additional content
                var fileExtension = System.IO.Path.GetExtension(fileName);
                var fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(fileName);
                var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
                var newFileName = $"{fileNameWithoutExtension}_{timestamp}{fileExtension}";

                // Create new file client with the unique name
                fileClient = CreateFileClient(directoryName, newFileName);
                writeStream = await fileClient.OpenWriteAsync(overwrite: false);
            }
            else
            {
                // File doesn't exist, create new
                writeStream = await fileClient.OpenWriteAsync(overwrite: false);
            }
        }
        else
        {
            // Normal write mode: if file exists, create a new file with unique name
            // This avoids ETag conflicts from overwriting existing files
            var fileExists = await fileClient.ExistsAsync();
            if (fileExists.Value)
            {
                // File exists - create a new file with a unique name (timestamp suffix)
                var fileExtension = System.IO.Path.GetExtension(fileName);
                var fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(fileName);
                var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
                var newFileName = $"{fileNameWithoutExtension}_{timestamp}{fileExtension}";

                // Create new file client with the unique name
                fileClient = CreateFileClient(directoryName, newFileName);
                writeStream = await fileClient.OpenWriteAsync(overwrite: false);
            }
            else
            {
                // File doesn't exist, create new
                writeStream = await fileClient.OpenWriteAsync(overwrite: false);
            }
        }

        if (writeStream == null)
        {
            throw new Exception($"Write stream for file {fileName} could not be created");
        }

        return writeStream;
    }

    public async Task<Stream> GetFileReadStreamAsync(string directoryName, string fileName, string? fileSystemName = null)
    {
        await ChangeFileSystem(fileSystemName);

        // Decode fileName before using
        fileName = WebUtility.UrlDecode(fileName);

        var fileClient = CreateFileClient(directoryName, fileName);
        var readStream = await fileClient.OpenReadAsync();

        if (readStream == null)
        {
            throw new Exception($"Read stream for file {fileName} could not be created");
        }
        
        return readStream;
    }

    public async Task<Stream> DownloadFileAsync(string directoryName, string fileName, string? fileSystemName = null)
    {
        await ChangeFileSystem(fileSystemName);
        
        var fileClient = CreateFileClient(directoryName, fileName);
        var response = await fileClient.ReadAsync();

        if (!response.HasValue)
        {
            throw new Exception($"File {fileName} not downloaded");
        }
        
        return response.Value.Content;
    }
    
    public async Task<Stream> DownloadFileAsync(string directoryName, string fileName, long start, long? length = null, string? fileSystemName = null)
    {
        await ChangeFileSystem(fileSystemName);
        
        var fileClient = CreateFileClient(directoryName, fileName);

        var response = await fileClient.ReadAsync(new DataLakeFileReadOptions
        {
            Range = new HttpRange(start, length)
        });

        if (!response.HasValue)
        {
            throw new Exception($"File {fileName} not downloaded");
        }
        
        return response.Value.Content;
    }

    public async Task<Stream> QueryFileAsync(string directoryName, string fileName, string query, string? fileSystemName = null)
    {
        await ChangeFileSystem(fileSystemName);
        
        var fileClient = CreateFileClient(directoryName, fileName);
        
        var response = await fileClient.QueryAsync(query, new DataLakeQueryOptions
        {
            InputTextConfiguration = new DataLakeQueryCsvTextOptions
            {
                HasHeaders = true, 
                RecordSeparator = "\n", 
                ColumnSeparator = ",", 
                EscapeCharacter = '\\', 
                QuotationCharacter = '"'
            },
            OutputTextConfiguration = new DataLakeQueryCsvTextOptions
            {
                HasHeaders = true, 
                RecordSeparator = "\n", 
                ColumnSeparator = ",", 
                EscapeCharacter = '\\', 
                QuotationCharacter = '"'
            }
        });

        if (!response.HasValue)
        {
            throw new Exception($"File {fileName} could not be queried");
        }
        
        return response.Value.Content;
    }

    public async Task DeleteFileAsync(string directoryName, string fileName, string? fileSystemName = null)
    {
        await ChangeFileSystem(fileSystemName);
        
        var fileClient = CreateFileClient(directoryName, fileName);
        var response = await fileClient.DeleteAsync();

        if (response.IsError)
        {
            throw new Exception($"File {fileName} not deleted");
        }
    }

    public async Task ChangeFileSystem(string? fileSystemName = null)
    {
        if (string.IsNullOrWhiteSpace(fileSystemName))
        {
            return;
        }

        // Get the container client and create if it doesn't exist
        _fileSystemClient = _dataLakeServiceClient.GetFileSystemClient(fileSystemName);
        await _fileSystemClient.CreateIfNotExistsAsync();
    }

    public async Task<bool> FileExistsAsync(string directoryName, string fileName, string? fileSystemName = null)
    {
        await ChangeFileSystem(fileSystemName);
        
        var fileClient = CreateFileClient(directoryName, fileName);
        var response = await fileClient.ExistsAsync();
        
        return response.Value;
    }

    public async Task<long> GetFileSizeAsync(string directoryName, string fileName, string? fileSystemName = null)
    {
        await ChangeFileSystem(fileSystemName);
        
        var fileClient = CreateFileClient(directoryName, fileName);
        
        try
        {
            var response = await fileClient.GetPropertiesAsync();
            return response.Value.ContentLength;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return 0;
        }
    }

    private DataLakeDirectoryClient CreateDirectoryClient(string directoryName)
    {
        return _fileSystemClient.GetDirectoryClient(directoryName);
    }

    private DataLakeFileClient CreateFileClient(string directoryName, string fileName)
    {
        var directoryClient = CreateDirectoryClient(directoryName);
        return directoryClient.GetFileClient(fileName);
    }

    public async Task<string> FileSasUriAsync(string directoryName, string fileName, string? fileSystemName = null, int expirationHours = 24)
    {
        await ChangeFileSystem(fileSystemName);

        var fileClient = CreateFileClient(directoryName, fileName);
        return fileClient.GenerateSasUri(DataLakeSasPermissions.Read, DateTimeOffset.UtcNow.AddHours(expirationHours)).AbsoluteUri;
    }
}
