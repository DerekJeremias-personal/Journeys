using Journeys.Core.Models;

namespace Journeys.Core.Interfaces.DataStorage;

public interface IAgentMessageAdapter
{
    /// <summary>All messages for a thread (PK = userId, PK2 = conversationId).</summary>
    Task<IReadOnlyList<AgentMessage>> ListMessagesAsync(
        string tenantId,
        string ownerUserId,
        string conversationId,
        int maxMessages = 500,
        CancellationToken cancellationToken = default);

    Task<AgentMessage> AppendMessageAsync(string tenantId, AgentMessage message);

    /// <summary>
    /// Loads the reserved workflow row for a conversation (same model id as chat messages), if present.
    /// </summary>
    Task<AgentMessage?> GetWorkflowOrchestrationRowAsync(
        string tenantId,
        string ownerUserId,
        string conversationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the workflow row (upsert). Caller must set <see cref="AgentMessage.Role"/> to <see cref="AgentMessage.WorkflowOrchestrationRole"/> and id to <see cref="AgentMessage.WorkflowOrchestrationDocumentId"/>.
    /// </summary>
    Task<AgentMessage> UpsertWorkflowOrchestrationRowAsync(
        string tenantId,
        AgentMessage workflowRow,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Recent messages for a user (cross-conversation) to derive thread list; caller groups by ConversationId.
    /// </summary>
    Task<IReadOnlyList<AgentMessage>> ListRecentMessagesForUserAsync(
        string tenantId,
        string ownerUserId,
        int maxMessages = 500,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves partition keys from tenant + conversation id, then loads chat rows and workflow row for offline audit.
    /// </summary>
    Task<AgentMessageConversationLoad> LoadConversationForAuditAsync(
        string tenantId,
        string conversationId,
        int maxMessages = 500,
        CancellationToken cancellationToken = default);
}
