using Journeys.Core.Interfaces.Services;
using Journeys.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Journeys.Core.Configuration
{
    public static class ConfigureFileIngestions
    {
        /// <summary>
        /// Configure services related to campaigns for dependency injection.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to configure services in.</param>
        public static IServiceCollection AddFileIngestionsServices(this IServiceCollection services)
        {
            // Register File Ingestion services
            services.AddScoped<IFileIngestionService, FileIngestionService>();

            return services;
        }
    }
}
