using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.DTO.Models;
using Journeys.DTO.Responses.BlobResponses;
using Microsoft.AspNetCore.Mvc;

namespace Journeys.API.Controllers;

[ApiController]
//[Route("api/[controller]/{tenantId}/[action]")]
[Route("api/[controller]")]
public class IngestController(
    IBatchFileService batchFileService,
    IBatchJobAdapter batchJobAdapter,
    IIngestService ingestService) : ControllerBase
{
    //[HttpPost("{tenantId}/uploadfile")]
    //public async Task<IActionResult> UploadFile(string tenantId, [FromForm] string schemaName, [FromForm] string tenancy,
    //    [FromForm] string path, [FromForm] IFormFile file)
    //{
    //    await using var stream = file.OpenReadStream();
    //    await batchFileService.CreateBatchFromFileStreamAsync(tenancy, tenantId, schemaName, file.FileName, path, stream);

    //    return Ok();
    //}

    //[HttpGet("{tenantId}/runjob/{batchFileId}")]
    //public async Task<IActionResult> RunJob(string tenantId, string batchFileId)
    //{
    //    await ingestService.RunJobAsync(tenantId, batchFileId);

    //    return Ok();
    //}

    [HttpGet("{tenantId}/results/{batchFileId}")]
    public async Task<ActionResult<IngestFileResults>> Results(string tenantId, string batchFileId)
    {
        var result = await ingestService.GetIngestResultsAsync(tenantId, batchFileId);

        return Ok(result);
    }

    // DELETE endpoint removed - we now use update-only pattern for BatchJob status transitions
    // Jobs are no longer deleted, only their status is updated
    [HttpDelete("{tenantId}/{jobId}/{batchFileId}")]
    public async Task<IActionResult> DeleteJob(string tenantId, string jobId, string batchFileId)
    {
        var isDeleted = await batchJobAdapter.DeleteBatchJobAsync(tenantId, jobId, batchFileId);

        return Ok(isDeleted);
    }
}
