using Journeys.Core.Interfaces.Services;
using Journeys.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Journeys.Core.Configuration;

public static class ConfigureWarehouseConfig
{
    public static IServiceCollection AddDatabricksConfigServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<IDatawarehouseService, DatawarehouseService>();
        services.AddScoped<IWarehouseAuthService, WarehouseAuthService>();
        services.AddScoped<IDatabricksQueryService, DatabricksQueryService>();
        services.AddScoped<IAnalyticsReportService, AnalyticsReportService>();
        return services;
    }
}
