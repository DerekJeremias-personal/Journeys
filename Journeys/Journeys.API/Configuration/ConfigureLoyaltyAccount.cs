using Journeys.Core.Interfaces.Services;
using Journeys.Core.Services;

namespace Journeys.API.Configuration
{
    public static class ConfigureLoyaltyAccount
    {
        /// <summary>
        /// Configure services related to loyalty accounts for dependency injection.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to configure services in.</param>
        public static IServiceCollection AddLoyaltyAccountServices(this IServiceCollection services)
        {
            // Register loyalty account services
            services.AddScoped<ILoyaltyAccountService, LoyaltyAccountService>();
            services.AddScoped<IAdminAuditService, AdminAuditService>();

            return services;
        }
    }
}
