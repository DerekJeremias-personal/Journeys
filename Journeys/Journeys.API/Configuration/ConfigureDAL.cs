using Journeys.DAL.Adapters;
using Journeys.Core.Caching;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Interfaces.Notifications;
using Journeys.Notification.Factories;
using Journeys.Infra.Backend;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Journeys.Infrastructure.DataStorage;

namespace Journeys.API.Configuration;

public static class ConfigureDAL
{
    /// <summary>
    /// Configure adapters related to DAL for dependency injection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to configure services in.</param>
    /// <param name="config">IConfiguration for adding service config to DI</param>
    public static IServiceCollection AddDAL(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<KeyValueStorageConfig>(config.GetSection("KeyValueStorage"));

        // Register DAL adapters
        services.AddScoped<ICampaignAdapter, CampaignAdapter>();
        services.AddScoped<ILoyaltyAccountAdapter, LoyaltyAccountAdapter>();
        services.AddScoped<ITagAdapter, TagAdapter>();
        services.AddScoped<ILoyaltyAccountPointLedgerAdapter, LoyaltyAccountPointLedgerAdapter>();
        services.AddScoped<IExternalReferenceAdapter, ExternalReferenceAdapter>();
        services.AddScoped<IJourneyAdapter, JourneyAdapter>();
        //services.AddScoped<ILoyaltyAccountRuleStateAdapter, LoyaltyAccountRuleStateAdapter>();
        services.AddScoped<IHistoricalRuleStateTTLAdapter, HistoricalRuleStateTTLAdapter>();
        services.AddScoped<ILoyaltyAccountPointsDetailsAdapter, LoyaltyAccountPointsDetailsAdapter>();

        services.AddScoped<IAdminAuditAdapter, AdminAuditAdapter>();
        services.AddScoped<IAgentMessageAdapter, AgentMessageAdapter>();

        services.AddScoped<INotificationConfigAdapter, NotificationConfigAdapter>();
        services.AddScoped<INotificationAdapterFactory, NotificationAdapterFactory>();

        services.AddScoped<ILogicalEntityAdapter, LogicalEntityAdapter>();

        // Register taxonomy adapter
        services.AddScoped<ITaxonomyDataAdapter, BackendTaxonomyAdapter>();
        services.AddScoped<ITenantDataAdapter, TenantAdapter>();

        //services.AddSingleton<IEventAdapterFactory, EventAdapterFactory>(); // Register the factory
        //services.AddScoped<IEventAdapterFactory, EventAdapterFactory>(); // Register the factory

        services.AddMemoryCache();
        
        // Configure distributed cache (Redis) - optional, falls back gracefully if not configured
        var redisConnectionString = config.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "Journeys:";
            });
        }
        else
        {
            // Fallback to in-memory distributed cache if Redis not configured
            // This allows the app to run without Redis, but distributed cache won't work
            services.AddDistributedMemoryCache();
        }
        
        // Register hybrid cache service as Singleton (cache services don't hold request-specific state)
        services.AddSingleton<IHybridCacheService, HybridCacheService>();
        
        services.AddScoped<IPointAccountTypeAdapter, PointAccountTypeAdapter>();
        //services.AddSingleton<IPointAccountTypeCache, PointAccountTypeCache>();

        // Register the singleton instance that will be created
        services.AddSingleton<IPointAccountTypeCache>(sp =>
        {
            PointAccountTypeCache.Initialize(sp);
            return PointAccountTypeCache.Instance;
        });
        
        services.AddTransient<IModelAdapter, ModelAdapter>();
        services.AddTransient<IDynamicExternalReferenceAdapter, DynamicExternalReferenceAdapter>();
        services.AddSingleton<ModelCache>();

        services.AddScoped<IBatchFileAdapter, BatchFileAdapter>();
        services.AddScoped<IBatchJobAdapter, BatchJobAdapter>();
        services.AddScoped<IBatchResultFileAdapter, BatchResultFileAdapter>();
        services.AddScoped<IDropboxConfigAdapter, DropboxConfigAdapter>();
        services.AddScoped<IDropboxConfigCache, DropboxConfigCache>();

        services.AddScoped<IDashboardRoleAdapter, DashboardRoleAdapter>();
        services.AddScoped<IReportDataAdapter, ReportDataAdapter>();


        return services;
    }
}
