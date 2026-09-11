using Journeys.Core.Models;

namespace Journeys.Core.Interfaces.Services;

public interface IBatchResultFileService
{
    Task<BatchResultFile> CreateResultFileForJobAsync(BatchJob batchJob);
    Task<BatchResultFile> CreateResultFileForChunkAsync(BatchJob chunkJob);
    Task<Stream> GetWriteStreamForResultFileAsync(BatchJob batchJob, BatchResultFile batchResultFile, bool append = false);
}
