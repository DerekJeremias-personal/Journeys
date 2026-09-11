using Journeys.DTO.Models;
using static Journeys.Core.Services.DatawarehouseService;

namespace Journeys.Core.Interfaces.Services;

public interface IDatabricksQueryService
{
    Task<DatabricksNormalizedResult> RunQueryAsync(
        string tenantId,
        string query,
        CancellationToken cancellationToken = default);

    Task<DatabricksNormalizedResult> RunQueryAsync(
        string tenantId,
        WarehouseConfigDto warehouseConfig,
        string query,
        CancellationToken cancellationToken = default);
}
