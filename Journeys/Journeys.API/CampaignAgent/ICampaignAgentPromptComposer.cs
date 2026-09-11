using Journeys.Core.Models;

namespace Journeys.API.CampaignAgent;

public interface ICampaignAgentPromptComposer
{
    /// <summary>Builds cached prompt fragments and merges session-specific lines. Safe to call every streaming turn.</summary>
    Task<CampaignAgentLlmPromptContext> BuildAsync(
        string tenantId,
        string? linkedCampaignId,
        IReadOnlyList<AgentMessage>? conversationMessages,
        string? workflowOrchestrationHint,
        CampaignWorkflowState workflowState,
        IReadOnlyList<string>? toolsThisTurn = null,
        CancellationToken cancellationToken = default);
}
