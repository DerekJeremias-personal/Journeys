using Journeys.Core.Interfaces.FileStorage;
using Journeys.Core.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Journeys.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FileIngestionController : ControllerBase
    {
        private readonly IFileIngestionService _fileIngestionService;
        private readonly ILogger<FilesController> _logger;
        public FileIngestionController(ILogger<FilesController> logger, IFileIngestionService fileIngestionService)
        {
            _logger = logger;
            _fileIngestionService = fileIngestionService;
        }

        [HttpPost("{tenantId}/summary")]
        public async Task<IActionResult> GetFileSummary(string tenantId, [FromQuery] string folder, [FromBody] List<string> fileName)
        {
            try
            {
                var response = await _fileIngestionService.GetFileSummaryQuery(tenantId, folder, fileName);
                return Ok(
                    response.Select(kvp => new {
                        fileName = kvp.Key,
                        status = kvp.Value.Status,
                        totalRows = kvp.Value.TotalRows,
                        dataFileProcessingStarted = kvp.Value.DateFileProcessingStarted,
                        dataFileProcessingFinished = kvp.Value.DateFileProcessingFinished,
                        totalErrors = kvp.Value.TotalErrors,
                        totalProcessedLines = kvp.Value.TotalProcessedLines
                    })
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{tenantId}/output/merge")]
        public async Task<IActionResult> DownloadOutputFile(string tenantId, string folder, string fileName)
        {
            var fileResponse = await _fileIngestionService.MergeOutputFiles(tenantId, folder, fileName);

            return File(fileResponse.FileStream, fileResponse.ContentType);
        }
    }
}
