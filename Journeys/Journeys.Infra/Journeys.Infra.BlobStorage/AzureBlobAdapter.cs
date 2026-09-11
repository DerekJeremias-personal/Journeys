using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Journeys.Core.Interfaces.FileStorage;
using Journeys.DTO.Models;
using Journeys.DTO.Responses;
using System.Text;
using System.Text.Json;

namespace Journeys.Infra.BlobStorage
{
    public class AzureBlobAdapter : IFileStorageAdapter
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _containerName;

        public AzureBlobAdapter(BlobServiceClient blobServiceClient, string containerName)
        {
            _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
            _containerName = containerName ?? throw new ArgumentNullException(nameof(containerName));
        }

        public async Task UploadAsync(string blobName, Stream fileStream, string contentType)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync();

            var blobClient = containerClient.GetBlobClient(blobName);
            await blobClient.UploadAsync(fileStream, new Azure.Storage.Blobs.Models.BlobHttpHeaders { ContentType = contentType });
        }

        public async Task<Stream> DownloadAsync(string blobName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            var response = await blobClient.DownloadAsync();
            return response.Value.Content;
        }

        public async Task DeleteAsync(string blobName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobName);
            await blobClient.DeleteIfExistsAsync();
        }

        public async Task<PagedResultSetResponse<FileSummaryDto>> GetFileAsync(string tenant,
          Dictionary<string, object?> parameters,
          int pageSize = 50,
          string? continuationToken = null)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);

            if (!parameters.TryGetValue("folderName", out var folderObj) ||
                string.IsNullOrWhiteSpace(folderObj?.ToString()))
                throw new ArgumentException("Folder name is required", nameof(parameters));

            string folderPrefix = $"{tenant}/{folderObj}/";

            string? searchValue = parameters.TryGetValue("searchValue", out var s)
                ? s?.ToString()
                : null;

            DateTime? startDate = parameters.TryGetValue("startDate", out var sd) &&
                                  DateTime.TryParse(sd?.ToString(), out var sdt)
                ? sdt
                : null;

            DateTime? endDate = parameters.TryGetValue("endDate", out var ed) &&
                                DateTime.TryParse(ed?.ToString(), out var edt)
                ? edt
                : null;

            var response = new PagedResultSetResponse<FileSummaryDto>
            {
                Entities = new List<FileSummaryDto>() // initialize to avoid null reference
            };
            var pages = containerClient
                .GetBlobsAsync(prefix: folderPrefix)
                .AsPages(continuationToken, pageSize);

            await foreach (var page in pages)
            {
                foreach (var blobItem in page.Values)
                {
                    var fileName = Path.GetFileName(blobItem.Name);

                    // CSV filter
                    if (!fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var size = blobItem.Properties.ContentLength;
                    if (size is null or 0)
                        continue;

                    if (!string.IsNullOrEmpty(searchValue) &&
                        !fileName.Contains(searchValue, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var dateIngested =
                        blobItem.Properties.CreatedOn?.UtcDateTime ??
                        blobItem.Properties.LastModified?.UtcDateTime ??
                        DateTime.MinValue;

                    if (startDate.HasValue && dateIngested < startDate.Value)
                        continue;

                    if (endDate.HasValue && dateIngested > endDate.Value)
                        continue;

                    response.Entities.Add(new FileSummaryDto
                    {
                        FileName = fileName,
                        FileSize = size >= 1024 * 1024
                            ? $"{size / (1024.0 * 1024.0):F2} MB"
                            : $"{size / 1024.0:F2} KB",
                        DateIngested = dateIngested,
                        FileType = "csv"
                    });

                    if (response.Entities.Count >= pageSize)
                        break;
                }

                response.ContinuationToken = page.ContinuationToken;
                break;
            }

            response.Entities = response.Entities
                .OrderByDescending(f => f.DateIngested)
                .ToList();

            return response;
        }

        public async Task<List<string>> GetFoldersAsync(string tenant)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var folders = new HashSet<string>();

            // Prefix pattern like: "tenant/"
            string prefix = $"{tenant}/";

            await foreach (var blobItem in containerClient.GetBlobsByHierarchyAsync(prefix: prefix, delimiter: "/"))
            {
                // The virtual folder will appear in blobPrefix items
                if (blobItem.IsPrefix && blobItem.Prefix != null)
                {
                    // Extract folder name after tenant/
                    var folderName = blobItem.Prefix
                        .Replace(prefix, string.Empty)
                        .TrimEnd('/');

                    if (!string.IsNullOrEmpty(folderName))
                        folders.Add(folderName);
                }
            }

            return folders.ToList();
        }
        public async Task<FileResponse> DownloadBlobAsync(string tenantId, string folder, string fileName)
        {
            string blobPath = string.IsNullOrWhiteSpace(folder)
                ? $"{tenantId}/{fileName}"
                : $"{tenantId}/{folder.TrimEnd('/')}/{fileName}";

            // container reference
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobPath);

            if (!await blobClient.ExistsAsync())
                throw new FileNotFoundException($"Blob '{blobPath}' not found in container '{_containerName}'.");

            // Download stream
            var downloadInfo = await blobClient.DownloadStreamingAsync();

            string contentType = downloadInfo.Value.Details.ContentType ?? "application/octet-stream";
            return new FileResponse
            {
                FileStream = downloadInfo.Value.Content,
                ContentType = contentType
            };
        }


        /// <inheritdoc />
        public async Task AppendNdjsonLineAsync(string blobName, string line, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(blobName))
                throw new ArgumentException("Blob name is required.", nameof(blobName));
            if (line is null)
                throw new ArgumentNullException(nameof(line));

            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

            var appendBlobClient = containerClient.GetAppendBlobClient(blobName);
            if (!await appendBlobClient.ExistsAsync(cancellationToken).ConfigureAwait(false))
            {
                await appendBlobClient.CreateAsync(
                    new AppendBlobCreateOptions
                    {
                        HttpHeaders = new BlobHttpHeaders { ContentType = "application/x-ndjson" }
                    },
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            var payload = line.EndsWith("\n", StringComparison.Ordinal) ? line : line + "\n";
            var bytes = Encoding.UTF8.GetBytes(payload);
            await using var ms = new MemoryStream(bytes, writable: false);
            await appendBlobClient.AppendBlockAsync(ms, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
