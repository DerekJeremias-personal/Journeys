using Journeys.Infra.DataLake;
using Journeys.Infra.Backend;
using Journeys.API.Services;
using Azure.Storage.Blobs;
using Journeys.Core.Interfaces.FileStorage;
using Journeys.Infra.BlobStorage;

namespace Journeys.API.Configuration;

public static class ConfigureInfra
{
    public static IServiceCollection AddInfra(this IServiceCollection services, IConfiguration config)
    {
        services.AddBackend(config);
        config.GetValue<string>("");

        // Add Azure Blob Service Configuration
        var dataLakeSection = config.GetSection("DataLake");
        var connectionString = dataLakeSection.GetValue<string>("ConnectionString");
        var containerName = dataLakeSection.GetValue<string>("DefaultFileSystem");
        var hasDataLakeConnection = !string.IsNullOrWhiteSpace(connectionString);
        var dataLakeEnabled = !config.GetValue<bool?>("DisableDataLake") ?? true;

        if (hasDataLakeConnection)
        {
            services.AddDataLake(config);

            // Register BlobServiceClient as a singleton (thread-safe)
            services.AddSingleton(new BlobServiceClient(connectionString));

            // Register your custom file storage adapter (scoped per request)
            services.AddScoped<IFileStorageAdapter>(sp =>
            {
                var blobClient = sp.GetRequiredService<BlobServiceClient>();
                return new AzureBlobAdapter(blobClient, containerName);
            });

            services.AddScoped<IFileIngestionAdapter>(sp =>
            {
                var blobClient = sp.GetRequiredService<BlobServiceClient>();
                return new FileIngestionBlobAdapter(blobClient, containerName);
            });

            // Register ChunkJobProcessor BackgroundService if DataLake is enabled
            if (dataLakeEnabled)
            {
                services.AddHostedService<ChunkJobProcessor>();
            }

            services.AddHostedService<BlobArchiveJobProcessor>();
        }
        else
        {
            // Keep DI constructable without Azure storage. Blob/Data Lake jobs stay unregistered.
            services.AddScoped<IDataLakeAdapter, UnconfiguredDataLakeAdapter>();
            services.AddScoped<IFileStorageAdapter, UnconfiguredFileStorageAdapter>();
            services.AddScoped<IFileIngestionAdapter, UnconfiguredFileIngestionAdapter>();
        }

        return services;
    }
}
