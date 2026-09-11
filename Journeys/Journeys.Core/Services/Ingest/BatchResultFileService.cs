using Journeys.Core.Interfaces;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.FileStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;

namespace Journeys.Core.Services.Ingest;

public class BatchResultFileService(IDataLakeAdapter dataLakeAdapter, IBatchResultFileAdapter batchResultFileAdapter) : IBatchResultFileService
{
    //private const string FILE_SYSTEM_NAME = "batchresults";

    public async Task<BatchResultFile> CreateResultFileForJobAsync(BatchJob batchJob)
    {
        var directory = $"{batchJob.Path}/output";
        var fileName = $"{batchJob.FileFriendlyName}|{batchJob.Id}.ndjson";

        // Only create file if it doesn't exist (handles resume scenarios)
        if (!await dataLakeAdapter.FileExistsAsync(directory, fileName, batchJob.Tenancy))
        {
            await dataLakeAdapter.CreateFileAsync(directory, fileName, batchJob.Tenancy);
        }

        return await batchResultFileAdapter.UpsertEntityAsync(new BatchResultFile
        (
            directory,
            batchJob.Id,
            fileName,
            DateTimeOffset.Now,
            DateTimeOffset.Now,
            batchJob.TenantId,
            null
        ));
    }
    
    public async Task<BatchResultFile> CreateResultFileForChunkAsync(BatchJob chunkJob)
    {
        if (!chunkJob.IsChunk)
        {
            throw new ArgumentException("CreateResultFileForChunkAsync can only be called for chunk jobs");
        }
        
        var directory = $"{chunkJob.Path}/output";

        // Create deterministic file name for resumability - same chunk always gets same file name
        var chunkIndex = chunkJob.ChunkIndex?.ToString("000") ?? "000";
        var fileName = $"{chunkJob.FileFriendlyName}_chunk{chunkIndex}_results.ndjson";

        // Only create file if it doesn't exist (handles resume scenarios)
        if (!await dataLakeAdapter.FileExistsAsync(directory, fileName, chunkJob.Tenancy))
        {
            await dataLakeAdapter.CreateFileAsync(directory, fileName, chunkJob.Tenancy);
        }

        return await batchResultFileAdapter.UpsertEntityAsync(new BatchResultFile
        (
            directory,
            chunkJob.Id,
            fileName,
            DateTimeOffset.Now,
            DateTimeOffset.Now,
            chunkJob.TenantId,
            null
        ));
    }

    public async Task<Stream> GetWriteStreamForResultFileAsync(BatchJob batchJob, BatchResultFile batchResultFile, bool append = false)
    {
        return await dataLakeAdapter.GetFileWriteStreamAsync(batchResultFile.Directory, batchResultFile.FileName, batchJob.Tenancy, append);
    }
}
