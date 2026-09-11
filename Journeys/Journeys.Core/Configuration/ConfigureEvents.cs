using Journeys.Core.Interfaces.Services;
using Journeys.Core.RulesEngine.Utility;
using Journeys.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Journeys.Core.Configuration;

public static class ConfigureEvents
{
    /// <summary>
    /// Configure services related to events for dependency injection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to configure services in.</param>
    public static IServiceCollection AddEventServices(this IServiceCollection services)
    {
        // Register EventService so both IEventService and concrete type resolve to same instance
        services.AddScoped<EventService>();
        services.AddScoped<IEventService>(sp => sp.GetRequiredService<EventService>());
        services.AddScoped<StateUtility>();

        return services;
    }
}
