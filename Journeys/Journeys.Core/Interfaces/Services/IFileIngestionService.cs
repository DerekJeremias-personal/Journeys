using Backend.Dto.Requests;
using Journeys.DTO.Models;
using Journeys.DTO.Responses;

namespace Journeys.Core.Interfaces.Services
{
    public interface IFileIngestionService
    {
        Task<PagedResultSetResponse<FileSummaryDto>> GetFiles(string tenantId, Dictionary<string, object> parameters, int pageSize, string? continuationToken = null);
        Task<FileIngestionSummaryDto> GetChunksSummaryByFileName(string tenantId, string folderName, string fileName);
        Task<List<string>> GetFolders(string tenantId);
        Task<Dictionary<string, FileSummaryDto>> GetFileSummaryQuery(string tenantId,string folder, List<string> fileName);
        Task<FileResponse> DownloadFile(string tenantId, string folder, string fileName);
        Task<FileResponse> MergeOutputFiles(string tenantId, string folder, string fileName);
    }
}
