using CampaignContextAudit.Mapping;
using Journeys.Core.Interfaces.DataStorage;

namespace CampaignContextAudit.Transcript;

public static class CosmosTranscriptLoader
{
    public static async Task<LoadedTranscript> LoadAsync(
        IAgentMessageAdapter adapter,
        string tenantId,
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        var load = await adapter.LoadConversationForAuditAsync(
            tenantId, conversationId, cancellationToken: cancellationToken);

        var chat = load.ChatMessages.Select(AgentMessageToDocMapper.ToDoc).ToList();
        var workflow = load.WorkflowRow is null
            ? new List<Models.AgentMessageDoc>()
            : new List<Models.AgentMessageDoc> { AgentMessageToDocMapper.ToDoc(load.WorkflowRow) };

        var source = $"cosmos:{load.TenantId}/{load.ConversationId}";
        return new LoadedTranscript(source, chat, workflow, load.OwnerUserId);
    }
}
