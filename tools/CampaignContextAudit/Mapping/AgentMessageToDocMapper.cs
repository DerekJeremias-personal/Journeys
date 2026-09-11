using CampaignContextAudit.Models;
using Journeys.Core.Models;

namespace CampaignContextAudit.Mapping;

public static class AgentMessageToDocMapper
{
    public static AgentMessageDoc ToDoc(AgentMessage m) => new()
    {
        Id = m.Id,
        TenantId = m.TenantId,
        ConversationId = m.ConversationId,
        Sequence = m.Sequence,
        Role = m.Role,
        Content = m.Content,
        ToolCallId = m.ToolCallId,
        ToolName = m.ToolName,
        ToolArgumentsJson = m.ToolArgumentsJson,
        ToolResultJson = m.ToolResultJson,
        WorkflowPhase = m.WorkflowPhase,
        WorkflowCampaignKind = m.WorkflowCampaignKind,
        WorkflowUserSkippedEventModels = m.WorkflowUserSkippedEventModels,
        WorkflowModelGatePassed = m.WorkflowModelGatePassed,
        ToolDurationMs = m.ToolDurationMs,
        TurnMetricsJson = m.TurnMetricsJson,
        CosmosTimestamp = (m.CreateDate ?? m.LastUpdated)?.ToUnixTimeSeconds()
    };
}
