using Journeys.Infra.Backend;
using Microsoft.Extensions.Configuration;

namespace CampaignContextAudit.Configuration;

public static class AuditBackendConfiguration
{
    public const string ApiUserSecretsId = "e6effa1a-0f1c-4ec0-b2a4-aa1e6bdf8368";

    public static string ResolveApiProjectDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "Journeys", "Journeys.API");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "appsettings.json")))
                return candidate;
            candidate = Path.Combine(dir.FullName, "Journeys.API");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "appsettings.json")))
                return candidate;
            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate Journeys.API project directory (appsettings.json). Run from repo layout containing ELP/Journeys/Journeys.API.");
    }

    public static IConfiguration Build()
    {
        var apiDir = ResolveApiProjectDirectory();
        return new ConfigurationBuilder()
            .SetBasePath(apiDir)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            // Load Development after env vars so local appsettings.Development.json wins in dev shells
            // that export KeyValueStorage__BaseUrl (same effective config as Visual Studio F5).
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets(ApiUserSecretsId)
            .Build();
    }

    public static void Validate(IConfiguration config)
    {
        var section = config.GetSection(KeyValueStorageConfig.SECTION_NAME);
        var baseUrl = section["BaseUrl"];
        var key = section["Key"];
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(
                "KeyValueStorage:BaseUrl and KeyValueStorage:Key must be set (check Journeys.API appsettings.Development.json and user secrets).");
    }
}
