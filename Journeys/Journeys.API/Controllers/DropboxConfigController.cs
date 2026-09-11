using System.Text.Json;
using Journeys.API.Models;
using Journeys.Core.Caching;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using Journeys.Core.Utility;
using Journeys.DTO.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Journeys.API.Controllers;

[ApiController]
[Route("api/[controller]/{tenantId}/[action]")]
public class DropboxConfigController(
    IDropboxConfigAdapter dropboxConfigAdapter,
    IDropboxConfigCache dropboxConfigCache,
    ILogger<DropboxConfigController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(string tenantId, [FromBody] CreateDropboxConfigRequest request)
    {
        try
        {
        var newConfig = new DropboxConfig
        (
            request.DirectoryName,
            request.FileType,
            request.ModelName,
            request.FileMap,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            request.ServiceTypeName,
            request.MethodName,
            tenantId,
            null,
            request.ResponseTypeName,
            request.ResponseLineKeyFormat
        );
        
        if (!request.FileType.Equals(DropboxConfigFileTypeStrings.CSV, StringComparison.InvariantCultureIgnoreCase))
        {
            var validator = new JsonValidator();
            validator.SetModelFromFile(JsonModelFilePathStrings.CSV);

            var configElement = validator.GetConfigDocument()?.RootElement;
            if (configElement == null)
            {
                return StatusCode(500, "Unknown error occured while validating dropbox config");
            }
            
            newConfig.FileMap = (JsonElement)configElement;
        }
        
            newConfig = await dropboxConfigAdapter.UpsertDropboxConfigAsync(newConfig);
            
            // Invalidate cache after create
            await dropboxConfigCache.InvalidateDropboxConfigAsync(tenantId);
            
            return Ok(newConfig);
        }
        catch (APIErrorsException)
        {
            // Let APIErrorsException bubble up to middleware
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating dropbox config for tenant {TenantId}", tenantId);
            return StatusCode(500, "Unknown error occurred while creating dropbox config");
        }
    }

    [HttpDelete("{dropboxConfigId}")]
    public async Task<IActionResult> Delete(string tenantId, string dropboxConfigId)
    {
        try
        {
            await dropboxConfigAdapter.DeleteDropboxConfigAsync(tenantId, dropboxConfigId);

            // Invalidate cache after delete
            await dropboxConfigCache.InvalidateDropboxConfigAsync(tenantId);

            return NoContent();
        }
        catch (APIErrorsException)
        {
            // Let APIErrorsException bubble up to middleware
            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while attempting to delete dropbox config");

            return StatusCode(500, "Unknown error occurred while attempting to delete dropbox config");
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(string tenantId)
    {
        try
        {
            var result = await dropboxConfigCache.GetAllDropboxConfigsAsync(tenantId);
            
            return Ok(result ?? []);
        }
        catch (APIErrorsException)
        {
            // Let APIErrorsException bubble up to middleware
            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while attempting to get all dropbox configs for tenant");
            
            return StatusCode(500, "Unknown error occurred while attempting to get dropbox configs");
        }
    }
}
