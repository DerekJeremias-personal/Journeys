using Journeys.Core.Caching;
using Journeys.Core.Interfaces.FileStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Services;
using Journeys.Core.Utility;
using Journeys.DTO.Models;
using Journeys.DTO.Models.RulesEngine;
using Journeys.DTO.Requests;
using Journeys.DTO.Responses;
using Microsoft.AspNetCore.Mvc;
namespace Journeys.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FilesController : ControllerBase
    {
        private readonly IFileIngestionService _fileIngestionService;
        private readonly ILogger<FilesController> _logger;
        public FilesController(ILogger<FilesController> logger, IFileIngestionService fileIngestionSummaryService)
        {
            _logger = logger;
            _fileIngestionService = fileIngestionSummaryService;
        }

        [HttpGet("{tenantId}")]
        public async Task<IActionResult> GetFilesAsync(string tenantId, [FromQuery] string folderName, [FromQuery] string? searchValue, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] int pageSize, [FromQuery] string? continuationToken = null)
        {
            try
            {
                var parameters = new Dictionary<string, object?>
                {
                    { "folderName", folderName },
                    { "searchValue", searchValue },
                    { "startDate", startDate },
                    { "endDate", endDate }
                };

                var response = await _fileIngestionService.GetFiles(tenantId, parameters, pageSize, continuationToken);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                return BadRequest(ex.Message);
            }
        }
        
        [HttpGet("{tenantId}/folders")]
        public async Task<IActionResult> GetFolders(string tenantId)
        {
            try
            {
                var response = await _fileIngestionService.GetFolders(tenantId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                return BadRequest(ex.Message);
            }
        }


        [HttpGet("{tenantId}/download")]
        public async Task<IActionResult> DownloadFile(string tenantId, string folder, string fileName)
        {
            try
            {
                var fileResponse = await _fileIngestionService.DownloadFile(tenantId, folder, fileName);
                return File(fileResponse.FileStream, fileResponse.ContentType);
            }
            catch (FileNotFoundException)
            {
                return NotFound("File not found");
            }
        }


    }
}
