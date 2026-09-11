using Journeys.DTO.Models;
using Journeys.DTO.Requests;
using Journeys.DTO.Responses;
using System.Text.Json;
using static Journeys.Core.Services.DatawarehouseService;

namespace Journeys.Core.Interfaces.Services;

public interface IDatawarehouseService
{
    Task<WarehouseConfigDto> UpsertWarehouseConfigAsync(string tenantId, WarehouseConfigDto request, CancellationToken cancellationToken = default);
    Task<PagedResultSetResponse<WarehouseConfigDto>> GetWarehouseConfig(string tenantId);
    Task<DatabricksNormalizedResult> Normalize(JsonElement root);
}
