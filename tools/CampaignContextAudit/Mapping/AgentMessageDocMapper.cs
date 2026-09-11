using CampaignContextAudit.Models;
using Journeys.Core.Models;

namespace CampaignContextAudit.Mapping;

public static class AgentMessageDocMapper
{
    public static AgentMessage ToAgentMessage(AgentMessageDoc doc, string? ownerUserId = null) =>
        new(
            doc.TenantId ?? "unknown",
            ownerUserId: ownerUserId ?? "audit",
            conversationId: doc.ConversationId ?? "audit",
            doc.Sequence,
            doc.Role,
            doc.Content,
            linkedCampaignId: null,
            doc.ToolCallId,
            doc.ToolName,
            doc.ToolArgumentsJson,
            doc.ToolResultJson,
            doc.Id,
            workflowPhase: doc.WorkflowPhase,
            workflowCampaignKind: doc.WorkflowCampaignKind,
            workflowUserSkippedEventModels: doc.WorkflowUserSkippedEventModels,
            workflowModelGatePassed: doc.WorkflowModelGatePassed,
            toolDurationMs: doc.ToolDurationMs,
            turnMetricsJson: doc.TurnMetricsJson);
}
