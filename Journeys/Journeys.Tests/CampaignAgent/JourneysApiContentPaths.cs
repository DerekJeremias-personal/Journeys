using System.Reflection;

namespace Journeys.Tests.CampaignAgent;

internal static class JourneysApiContentPaths
{
    public static string ContentRoot
    {
        get
        {
            var meta = typeof(JourneysApiContentPaths).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "JourneysApiContentRoot")
                ?.Value;
            if (!string.IsNullOrWhiteSpace(meta)
                && File.Exists(Path.Combine(meta, "appsettings.Development.json"))
                && File.Exists(Path.Combine(meta, "CampaignAgent", "SystemPrompt.txt")))
            {
                return meta;
            }

            throw new DirectoryNotFoundException(
                "Assembly metadata JourneysApiContentRoot does not point at Journeys.API source (need appsettings.Development.json and CampaignAgent/SystemPrompt.txt).");
        }
    }

    public static string DevelopmentJson => Path.Combine(ContentRoot, "appsettings.Development.json");

    public static string CampaignAgentDir => Path.Combine(ContentRoot, "CampaignAgent");
}
