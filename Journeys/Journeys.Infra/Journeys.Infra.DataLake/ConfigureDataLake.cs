using Journeys.Core.Interfaces.FileStorage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Journeys.Infra.DataLake;

public static class ConfigureDataLake
{
    public static IServiceCollection AddDataLake(this IServiceCollection services, IConfiguration config)
    {
        if (!config.GetValue<bool?>("DisableDataLake") ?? true)
        {
            services.Configure<DataLakeAdapterConfig>(config.GetSection(DataLakeAdapterConfig.SECTION_NAME));
            services.AddScoped<IDataLakeAdapter, DataLakeAdapter>();
        }

        return services;
    }
}
