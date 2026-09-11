using Journeys.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Journeys.Core.Configuration;

public static class ConfigureExports
{
    public static IServiceCollection AddExportServices(this IServiceCollection services)
    {
        services.AddScoped<IExportService, ExportService>();

        return services;
    }
}
