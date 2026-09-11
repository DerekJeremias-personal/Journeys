using Journeys.DTO.Models;
using Journeys.DTO.Responses;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.FileStorage
{
    public interface IFileStorageAdapter
    {
        Task UploadAsync(string blobName, Stream fileStream, string contentType);
        Task<Stream> DownloadAsync(string blobName);
        Task DeleteAsync(string blobName);
        Task<List<string>> GetFoldersAsync(string tenant);

        Task<PagedResultSetResponse<FileSummaryDto>> GetFileAsync(string tenant, Dictionary<string, object?> parameters, int pageSize, string? continuationToken = null);

        Task<FileResponse> DownloadBlobAsync(string tenantId, string folder, string fileName);

        /// <summary>
        /// Appends a single UTF-8 line (caller supplies trailing newline) to an append blob, creating the append blob if needed.
        /// Used for NDJSON-style append-only logs (e.g. Campaign Agent tool audit).
        /// </summary>
        Task AppendNdjsonLineAsync(string blobName, string line, CancellationToken cancellationToken = default);


    }
}
