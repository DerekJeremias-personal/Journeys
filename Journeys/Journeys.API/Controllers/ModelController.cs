using Backend.Dto.Requests;
using Journeys.Core.Caching;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace Journeys.API.Controllers
{
    [Controller()]
    [Route("api/[controller]")]
    public class ModelController : ControllerBase
    {
        private readonly ILogger<ModelController> _logger;
        private readonly ModelCache _modelCache;
        public ModelController(ModelCache modelCache, ILogger<ModelController> logger)
        {
            _logger = logger;
            _modelCache = modelCache;
        }


        [HttpPost]
        [Route("{tenantId}/GetMany")]
        public async Task<IActionResult> GetMany(string tenantId, [FromBody] GetManyModelsRequest? request)
        {
            var allModels = await _modelCache.GetOrLoadAll(tenantId);
            
            // If no request or no ModelIds specified, return all models
            if (request == null || request.ModelIds == null || request.ModelIds.Count == 0)
            {
                _logger.LogDebug("GetMany: Returning all {Count} models for tenant {TenantId}", allModels.Count, tenantId);
                return new JsonResult(allModels);
            }
            
            // Filter models by ModelIds
            var filteredModels = allModels
                .Where(m => request.ModelIds.Contains(m.ID, StringComparer.OrdinalIgnoreCase))
                .ToList();
            
            _logger.LogDebug(
                "GetMany: Filtered {FilteredCount} models from {TotalCount} total for tenant {TenantId} (requested {RequestedCount} model IDs)",
                filteredModels.Count,
                allModels.Count,
                tenantId,
                request.ModelIds.Count);
            
            return new JsonResult(filteredModels);
        }
    }
}
