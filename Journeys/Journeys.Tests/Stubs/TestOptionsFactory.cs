
using Journeys.Infra.Backend;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.IO;

namespace Journeys.Tests;

public static class TestOptionsFactory
{
    public static IOptions<KeyValueStorageConfig> GetKeyValueStorageConfigOptions()
    {
        // Try to read from appsettings.Testing.json first
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Testing.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var baseUrl = Environment.GetEnvironmentVariable("BACKEND_BASE_URL") 
            ?? config["KeyValueStorage:BaseUrl"] 
            ?? "https://localhost:7155"; // Default matches appsettings.Testing.json

        var key = Environment.GetEnvironmentVariable("BACKEND_KEY") 
            ?? config["KeyValueStorage:Key"] 
            ?? "Key$2Kingd0m!";

        return new OptionsWrapper<KeyValueStorageConfig>(new KeyValueStorageConfig
        {
            BaseUrl = baseUrl,
            Key = key
        });
    }
}
