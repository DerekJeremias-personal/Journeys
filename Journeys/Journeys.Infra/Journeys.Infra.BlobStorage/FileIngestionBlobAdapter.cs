using Azure.Storage.Blobs;
using Journeys.Core.Interfaces.FileStorage;
using Journeys.DTO.Models;
using Journeys.DTO.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Journeys.Infra.BlobStorage
{
    public class FileIngestionBlobAdapter : IFileIngestionAdapter
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _containerName;

        public FileIngestionBlobAdapter(BlobServiceClient blobServiceClient, string containerName)
        {
            _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
            _containerName = containerName ?? throw new ArgumentNullException(nameof(containerName));
        }
        public async Task<FileIngestionSummaryDto> GetChunkSummaryFromFileNameAsync(string tenant, string folder, string targetFileName)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);

                string outputPrefix = $"{tenant}/{folder}/output/";

                int totalRows = 0;
                int successCount = 0;
                int errorCount = 0;

                DateTimeOffset? earliestDropped = null;
                DateTimeOffset? latestProcessed = null;

                await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: outputPrefix))
                {
                    var fileName = Path.GetFileName(blobItem.Name);

                    if (!fileName.StartsWith(targetFileName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (blobItem.Name.EndsWith("/") || blobItem.Properties.ContentLength is null or 0)
                        continue;

                    var blobClient = containerClient.GetBlobClient(blobItem.Name);
                    var download = await blobClient.DownloadStreamingAsync();

                    using var reader = new StreamReader(download.Value.Content);
                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        totalRows++;

                        if (line.Contains("\"Success\":true", StringComparison.OrdinalIgnoreCase))
                        {
                            successCount++;
                            continue;
                        }

                        if (line.Contains("\"Success\":false", StringComparison.OrdinalIgnoreCase))
                        {
                            errorCount++;
                            continue;
                        }

                        using var doc = JsonDocument.Parse(line);
                        if (doc.RootElement.TryGetProperty("Success", out var val))
                        {
                            if (val.GetBoolean()) successCount++;
                            else errorCount++;
                        }
                    }

                    if (blobItem.Properties.CreatedOn.HasValue)
                    {
                        if (earliestDropped == null || blobItem.Properties.CreatedOn < earliestDropped)
                            earliestDropped = blobItem.Properties.CreatedOn;
                    }

                    if (blobItem.Properties.LastModified.HasValue)
                    {
                        if (latestProcessed == null || blobItem.Properties.LastModified > latestProcessed)
                            latestProcessed = blobItem.Properties.LastModified;
                    }
                }

                //Compute completion duration
                TimeSpan? completionDuration = null;
                if (earliestDropped.HasValue && latestProcessed.HasValue)
                {
                    completionDuration = latestProcessed - earliestDropped;
                }

                return new FileIngestionSummaryDto
                {
                    TenantId = tenant,
                    FileName = targetFileName,
                    DirectoryLoaded = $"{tenant}/order/output",
                    TotalRows = totalRows,
                    TotalSuccess = successCount,
                    TotalErrors = errorCount,
                    DateFileDropped = earliestDropped,
                    DateFileProcessed = latestProcessed,
                    CompletionTime = completionDuration.ToString(),
                    FileSize = "2 MB",
                    Source = "Azure Storage Account",
                    ReferenceId = "TXN - 125432",
                    FileType = "filetype",
                    Status = "Status",
                    Checked = false

                };
            }
            catch
            {
                throw;
            }
        }

        public async Task<FileResponse> MergeOutputFiles(string tenantId, string folder, string fileName)
        {
            string folderPath = $"{tenantId}/{folder}/output";
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);

            var mergedStream = new MemoryStream();

            await foreach (var blob in containerClient.GetBlobsAsync(prefix: folderPath))
            {
                string nameOnly = Path.GetFileName(blob.Name);

                if (!nameOnly.StartsWith(fileName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var blobClient = containerClient.GetBlobClient(blob.Name);

                var properties = await blobClient.GetPropertiesAsync();
                bool isEmpty = properties.Value.ContentLength == 0;

                if (isEmpty)
                {
                    string placeholder =
                        $@"{{""LineKey"":"""",""Success"":false,""Errors"":{{""FileName"":""{nameOnly}"",""Message"":""Empty file""}},""LineNumber"":""""}}";

                    var bytes = Encoding.UTF8.GetBytes(placeholder + "\n");
                    await mergedStream.WriteAsync(bytes, 0, bytes.Length);
                }
                else
                {
                    var download = await blobClient.DownloadStreamingAsync();
                    await download.Value.Content.CopyToAsync(mergedStream);

                    await mergedStream.WriteAsync(new byte[] { (byte)'\n' });
                }
            }

            mergedStream.Position = 0;

            return new FileResponse
            {
                FileStream = mergedStream,
                ContentType = "application/x-ndjson"
            };
        }

        public async Task<bool> HasChunksAsync(string tenant, string folder, string targetFileName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);

            string outputPrefix = $"{tenant}/{folder}/output/";

            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: outputPrefix))
            {
                var fileName = Path.GetFileName(blobItem.Name);

                if (!fileName.StartsWith(targetFileName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (blobItem.Properties.ContentLength > 0)
                    return true;
            }

            return false;
        }

    }
}
