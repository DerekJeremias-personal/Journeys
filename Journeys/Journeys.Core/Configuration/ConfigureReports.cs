using Journeys.Core.Interfaces.Services;
using Journeys.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Journeys.Core.Configuration
{
    public static class ConfigureReports
    {
        /// <summary>
        /// Configure services related to campaigns for dependency injection.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to configure services in.</param>
        /// 
        public static IServiceCollection AddReportServices(this IServiceCollection services)
        {
            // Report services
            services.AddScoped<IReportService, ReportService>();
            return services;
        }
    }
}
