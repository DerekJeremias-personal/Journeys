using Journeys.DTO.Models;
using Journeys.DTO.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.FileStorage
{
    public interface IFileIngestionAdapter
    {
        Task<FileIngestionSummaryDto> GetChunkSummaryFromFileNameAsync(string tenant, string folder, string targetFileName);
        Task<FileResponse> MergeOutputFiles(string tenantId, string folder, string fileName);
        Task<bool> HasChunksAsync(string tenant, string folder, string targetFileName);
    }
}
