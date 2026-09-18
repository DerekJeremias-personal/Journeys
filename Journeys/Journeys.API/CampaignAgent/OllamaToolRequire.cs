using Journeys.Core.Models;
using Microsoft.Extensions.AI;

namespace Journeys.API.CampaignAgent;

public static class OllamaToolRequire
{
    public static bool ShouldRequire(
        CampaignAgentLlmProviderKind provider,
        int filteredToolCount,
        CampaignWorkflowPhase phase)
    {
        if (provider != CampaignAgentLlmProviderKind.OpenAICompatible)
            return false;
        if (filteredToolCount <= 0)
            return false;
        if (phase == CampaignWorkflowPhase.Done)
            return false;
        return true;
    }

    public static void Apply(
        ChatOptions options,
        CampaignAgentLlmProviderKind provider,
        CampaignWorkflowPhase phase)
    {
        ArgumentNullException.ThrowIfNull(options);
        var count = options.Tools?.Count ?? 0;
        if (ShouldRequire(provider, count, phase))
            options.ToolMode = ChatToolMode.RequireAny;
    }
}
