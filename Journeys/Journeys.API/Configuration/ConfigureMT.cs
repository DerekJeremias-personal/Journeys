using Azure.Identity;
using Journeys.API.Consumers;
using Journeys.API.Models;
using Journeys.Core.Models;
using Journeys.Core.Services.Ingest;
using Journeys.Core.Utility;
using Journeys.DTO.Models;
using MassTransit;
using MassTransit.Contracts.JobService;

namespace Journeys.API.Configuration;

public static class ConfigureMT
{
    public static IServiceCollection AddMT(this IServiceCollection services, IConfiguration configuration, bool isDevelopment)
    {
        var massTransitConfig = configuration.GetSection(MassTransitConfig.SECTION_NAME).Get<MassTransitConfig>();
        massTransitConfig = ValidateConfig(massTransitConfig, isDevelopment);

        if (isDevelopment && massTransitConfig.UseLocalServices)
        {
            Console.WriteLine($"Local so run in memory");
            return AddInMemory(services, configuration);
        }

        return AddServices(services, massTransitConfig, configuration);
    }

    private static IServiceCollection AddServices(IServiceCollection services, MassTransitConfig massTransitConfig, IConfiguration configuration)
    {
        var jobTimeout = TimeSpan.FromMinutes(massTransitConfig.JobOptions.JobTimeoutMinutes);
        var concurrentJobLimit = Math.Max(massTransitConfig.JobOptions.ConcurrentJobLimit, 4);
        var disableDataLakeRaw = configuration["DisableDataLake"];
        var disableDataLake = configuration.GetValue<bool?>("DisableDataLake");
        Console.WriteLine($"DisableDataLake raw: {disableDataLakeRaw}, parsed: {disableDataLake}");

        //if (true) //!configuration.GetValue<bool?>("DisableDataLake") ?? true)
        //{
        //    services.AddMassTransit(x =>
        //    {
        //        x.AddConsumer<BlobCreatedJobConsumer>(cfg =>
        //        {
        //            cfg.Options<JobOptions<SubmitJob<BlobCreated>>>(options => options
        //                .SetJobTimeout(jobTimeout)
        //                .SetConcurrentJobLimit(concurrentJobLimit)
        //            );
        //        });

        //        x.AddConsumer<EventProcessedJobConsumer>(cfg2 =>
        //        {
        //            cfg2.Options<JobOptions<SubmitJob<ProcessedEventDto>>>(options => options
        //                .SetJobTimeout(jobTimeout)
        //                .SetConcurrentJobLimit(concurrentJobLimit)
        //            );
        //        });

        //        x.AddConsumer<PointsChangedJobConsumer>(cfg3 =>
        //        {
        //            cfg3.Options<JobOptions<SubmitJob<PointLedgerDto>>>(options => options
        //                .SetJobTimeout(jobTimeout)
        //                .SetConcurrentJobLimit(concurrentJobLimit)
        //            );
        //        });

        //        // Add chunk job consumer with configurable concurrency
        //        // Increased to 10 per node � 6 nodes = 60 concurrent chunks capacity
        //        // This maximizes throughput while handling 412 errors gracefully
        //        x.AddConsumer<ProcessChunkJobConsumer>(cfg4 =>
        //        {
        //            cfg4.Options<JobOptions<SubmitJob<ProcessChunkJobMessage>>>(options => options
        //                .SetJobTimeout(jobTimeout)
        //                .SetConcurrentJobLimit(concurrentJobLimit)
        //            );
        //            cfg4.ConcurrentMessageLimit = Math.Max(concurrentJobLimit, 10);  // Process up to X messages concurrently
        //        });

        //        x.AddDelayedMessageScheduler();
        //        x.SetKebabCaseEndpointNameFormatter();

        //        if (massTransitConfig.SagaRepository.IsEmulator)
        //        {
        //            x
        //                .AddJobSagaStateMachines(options =>
        //                {
        //                    // CRITICAL: This allows jobs to be finalized when completed
        //                    options.FinalizeCompleted = true;
        //                })
        //                .CosmosRepository(r =>
        //                {
        //                    r.ConfigureEmulator();
        //                    r.DatabaseId = "test";
        //                });
        //        }
        //        else
        //        {
        //            if (!string.IsNullOrEmpty(massTransitConfig.SagaRepository.Key))
        //            {
        //                x.AddJobSagaStateMachines(options =>
        //                {
        //                    // CRITICAL: This allows jobs to be finalized when completed
        //                    options.FinalizeCompleted = true;
        //                })
        //                 .CosmosRepository(massTransitConfig.SagaRepository.Host, massTransitConfig.SagaRepository.Key, r =>
        //                 {
        //                     r.DatabaseId = massTransitConfig.SagaRepository.Database;
        //                     r.CollectionId = massTransitConfig.SagaRepository.Collection;
        //                 });
        //            }
        //            else
        //            {
        //                var credential = new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned);
        //                x.AddJobSagaStateMachines(options =>
        //                {
        //                    // CRITICAL: This allows jobs to be finalized when completed
        //                    options.FinalizeCompleted = true;
        //                })
        //                 .CosmosRepository(massTransitConfig.SagaRepository.Host, credential, r =>
        //                 {
        //                     r.DatabaseId = massTransitConfig.SagaRepository.Database;
        //                     r.CollectionId = massTransitConfig.SagaRepository.Collection;
        //                 });
        //            }
        //        }

        //        x.UsingAzureServiceBus((context, cfg) =>
        //        {
        //            if (massTransitConfig.Transport.IsUri)
        //            {
        //                cfg.Host(new Uri(massTransitConfig.Transport.Host));
        //            }
        //            else
        //            {
        //                cfg.Host(massTransitConfig.Transport.Host);
        //            }

        //            cfg.PrefetchCount = 60;

        //            cfg.UseServiceBusMessageScheduler();
        //            cfg.ConfigureEndpoints(context);

        //        });
        //    });
        //}
        //else
        //{

        //}
        return services;
    }

    private static IServiceCollection AddInMemory(IServiceCollection services, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool?>("DisableDataLake") ?? true)
        {
            services.AddMassTransit(x =>
            {
                x.AddConsumer<BlobCreatedJobConsumer>(cfg =>
                {
                    cfg.Options<JobOptions<SubmitJob<BlobCreated>>>(options => options
                        .SetJobTimeout(TimeSpan.FromMinutes(15))
                        .SetConcurrentJobLimit(10)
                    );
                });

                x.AddConsumer<EventProcessedJobConsumer>(cfg2 =>
                {
                    cfg2.Options<JobOptions<SubmitJob<ProcessedEventDto>>>(options => options
                        .SetJobTimeout(TimeSpan.FromMinutes(15))
                        .SetConcurrentJobLimit(10)
                    );
                });

                // Add chunk job consumer with configurable concurrency
                var chunkJobConcurrencyLimit = configuration.GetValue<int>("BatchProcessing:ChunkJobConcurrencyLimit", 5);
                x.AddConsumer<ProcessChunkJobConsumer>(cfg4 =>
                {
                    cfg4.Options<JobOptions<SubmitJob<ProcessChunkJobMessage>>>(options => options
                        .SetJobTimeout(TimeSpan.FromMinutes(15))
                        .SetConcurrentJobLimit(chunkJobConcurrencyLimit)
                    );
                });

                x.AddDelayedMessageScheduler();
                x.SetKebabCaseEndpointNameFormatter();

                x.SetInMemorySagaRepositoryProvider();
                x.AddJobSagaStateMachines(options =>
                {
                    // Allow jobs to be finalized when completed
                    options.FinalizeCompleted = true;
                });

                x.UsingInMemory((context, cfg) =>
                {
                    cfg.UseDelayedMessageScheduler();
                    cfg.ConfigureEndpoints(context);
                });
            });
        }
        return services;
    }

    private static MassTransitConfig ValidateConfig(MassTransitConfig? config, bool isDevelopment)
    {
        switch (isDevelopment)
        {
            case true when config == null:
                return new MassTransitConfig();
            case true when !config.UseLocalServices:
                return config;
            case false when config == null:
                throw new ApplicationException("Missing masstransit configuration");
        }

        if (string.IsNullOrWhiteSpace(config.Transport.Host))
        {
            throw new ApplicationException("Missing masstransit transport host configuration");
        }

        if (string.IsNullOrWhiteSpace(config.SagaRepository.Host))
        {
            throw new ApplicationException("Missing masstransit saga repository host configuration");
        }

        return config;
    }
}

