using Journeys.Core.Interfaces.Services;
using Journeys.Core.Services;
using Journeys.Core.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Journeys.Core.Configuration;

public static class ConfigureCampaigns
{
    /// <summary>
    /// Configure services related to campaigns for dependency injection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to configure services in.</param>
    /// <param name="configuration">Application configuration (binds <see cref="CampaignUpsertValidationOptions"/>).</param>
    public static IServiceCollection AddCampaignServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CampaignUpsertValidationOptions>(
            configuration.GetSection(CampaignUpsertValidationOptions.SectionName));

        // Register Campaign cache
        services.AddScoped<ICampaignCache, CampaignCache>();

        // Register Campaign services
        services.AddScoped<ICampaignService, CampaignService>();
        services.AddScoped<CampaignTaxonomyRuleValidator>();
        services.AddScoped<CampaignJourneyPointOutcomePatValidator>();
        services.AddScoped<CampaignDefinitionValidator>();
        services.AddScoped<CampaignValidationOrchestrator>();
        services.AddScoped<ICampaignAssistantContextService, CampaignAssistantContextService>();

        // Register Rule services
        services.AddScoped<IRulesService, RulesService>();

        return services;
    }
}
