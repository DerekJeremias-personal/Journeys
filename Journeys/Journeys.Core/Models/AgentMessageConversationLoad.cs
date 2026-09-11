namespace Journeys.Core.Models;

public sealed record AgentMessageConversationLoad(
    string TenantId,
    string OwnerUserId,
    string ConversationId,
    IReadOnlyList<AgentMessage> ChatMessages,
    AgentMessage? WorkflowRow);
