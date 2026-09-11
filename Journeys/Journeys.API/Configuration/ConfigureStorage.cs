using Azure.Storage.Files.DataLake.Models;
using Azure.Storage.Files.DataLake;

namespace Journeys.API.Configuration
{
    public static class ConfigureStorage
    {
        public static IServiceCollection AddStorage(this IServiceCollection services, IConfiguration configuration, bool isDevelopment)
        {
            if (isDevelopment)
            {
                EnsureDevStorageSetup().GetAwaiter().GetResult();
            }
            return services;
        }

        private static async Task EnsureDevStorageSetup()
        {
            // Create container if it doesn't exist
            var serviceClient = new DataLakeServiceClient("UseDevelopmentStorage=true");
            var fileSystemClient = serviceClient.GetFileSystemClient("batchfiles");
            await fileSystemClient.CreateIfNotExistsAsync();
        }
    }
}
