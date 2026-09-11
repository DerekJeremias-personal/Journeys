using Journeys.Core.Interfaces.Services;
using Journeys.Core.Services;
using Journeys.Core.Services.Ingest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Journeys.Core.Configuration;

public static class ConfigureBatches
{
    public static IServiceCollection AddBatchServices(this IServiceCollection services, IConfiguration config)
    {
        if (!config.GetValue<bool?>("DisableDataLake") ?? true)
        {
            services.AddScoped<IBatchFileService, BatchFileService>();
            services.AddScoped<IBatchJobService, BatchJobService>();
            services.AddScoped<IJobClaimService, JobClaimService>();
            services.AddScoped<IJobQueryService, JobQueryService>();
            services.AddScoped<IBatchResultFileService, BatchResultFileService>();
            services.AddScoped<IIngestService, IngestService>();
            services.AddScoped<FileChunkingService>();
            services.AddScoped<ParallelBatchProcessorService>();
            services.AddScoped<DynamicServiceInvoker>();
            services.AddScoped<ResponseHandlerService>();
            services.AddScoped<ResumabilityService>();
            //services.AddScoped<JobClaimingService>();

            // Configure batch processing options
            services.Configure<BatchProcessingOptions>(config.GetSection("BatchProcessing"));

            // Configure ingest processing options
            services.Configure<IngestOptions>(config.GetSection("Ingest"));

        }

        return services;
    }
}
