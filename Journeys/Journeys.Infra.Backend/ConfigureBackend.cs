using Journeys.Core.Interfaces.DataStorage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Infra.Backend
{
    public static class ConfigureBackend
    {
        public static IServiceCollection AddBackend(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<KeyValueStorageConfig>(config.GetSection(KeyValueStorageConfig.SECTION_NAME));

            services.AddScoped<IDynamicDataAdapter, BackendAdapter>();

            return services;
        }
    }
}
