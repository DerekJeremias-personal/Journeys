# Task 1 review package (uncommitted; no SHA)

New files only.

## Journeys/Journeys.API/CampaignAgent/CampaignAgentLlmProvider.cs

```csharp
using Microsoft.Extensions.Configuration;

namespace Journeys.API.CampaignAgent;

public enum CampaignAgentLlmProviderKind
{
    Anthropic,
    OpenAICompatible
}

public static class CampaignAgentLlmProvider
{
    public static CampaignAgentLlmProviderKind Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var raw = configuration["CampaignAgent:Provider"];
        if (string.IsNullOrWhiteSpace(raw))
            raw = configuration["CAMPAIGN_AGENT_PROVIDER"];
        return Parse(raw);
    }

    public static CampaignAgentLlmProviderKind Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return CampaignAgentLlmProviderKind.Anthropic;

        return value.Trim() switch
        {
            var v when v.Equals("Anthropic", StringComparison.OrdinalIgnoreCase)
                => CampaignAgentLlmProviderKind.Anthropic,
            var v when v.Equals("OpenAICompatible", StringComparison.OrdinalIgnoreCase)
                => CampaignAgentLlmProviderKind.OpenAICompatible,
            var v when v.Equals("Ollama", StringComparison.OrdinalIgnoreCase)
                => CampaignAgentLlmProviderKind.OpenAICompatible,
            _ => throw new InvalidOperationException(
                "Unknown Campaign Agent LLM provider. Allowed values: Anthropic, OpenAICompatible, Ollama.")
        };
    }
}
```

## Journeys/Journeys.Tests/CampaignAgent/CampaignAgentLlmProviderTests.cs

(verbatim from plan Task 1 Step 1)

Controller test run: Passed 12 / Failed 0 (isolated -o TEMP\journeys-ollama-t1). Plan listed 13 cases; xUnit reports 12 (5+4+3).
