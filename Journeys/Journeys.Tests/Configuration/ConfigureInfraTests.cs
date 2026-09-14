using Journeys.API.Configuration;
using Journeys.Core.Interfaces.FileStorage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Journeys.Tests.Configuration;

public class ConfigureInfraTests
{
    [Fact]
    public async Task AddInfra_without_DataLake_connection_registers_storage_adapters_so_the_host_can_start()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataLake:DefaultFileSystem"] = "journeys-dev",
                ["DisableDataLake"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfra(config);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        var dataLake = sp.GetRequiredService<IDataLakeAdapter>();
        var files = sp.GetRequiredService<IFileStorageAdapter>();
        Assert.NotNull(sp.GetRequiredService<IFileIngestionAdapter>());

        await dataLake.ChangeFileSystem("reports");
        Assert.False(await dataLake.FileExistsAsync("tenant/AccountData", "id.json", "reports"));
        await dataLake.DeleteFileAsync("tenant/AccountData", "id.json", "reports");
        await files.AppendNdjsonLineAsync("audit.ndjson", "{}\n");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dataLake.CreateDirectoryAsync("tenant/AccountData", "reports"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            files.UploadAsync("blob", Stream.Null, "text/plain"));
    }
}
