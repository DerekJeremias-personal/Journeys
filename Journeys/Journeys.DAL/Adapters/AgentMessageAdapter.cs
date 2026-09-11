using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using Journeys.DTO.Requests;
using Microsoft.Extensions.Configuration;

namespace Journeys.DAL.Adapters;

public class AgentMessageAdapter : BaseAdapter<AgentMessage>, IAgentMessageAdapter
{
    private const string AgentMessageModelId = "12f6e8d4-5b3a-491c-9f2e-8a7d6c5b4a31";

    private readonly IConfiguration _configuration;

    public AgentMessageAdapter(IDynamicDataAdapter dynAdapter, IConfiguration configuration) : base(dynAdapter)
    {
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<AgentMessage>> ListMessagesAsync(
        string tenantId,
        string ownerUserId,
        string conversationId,
        int maxMessages = 500,
        CancellationToken cancellationToken = default)
    {
        maxMessages = Math.Clamp(maxMessages, 1, 1000);
        var all = new List<AgentMessage>();
        string? token = null;
        do
        {
            var page = await FetchEntityByKeyAsync(
                tenantId,
                ownerUserId,
                AgentMessageModelId,
                maxMessages,
                conversationId,
                token);
            if (page.Entities != null && page.Entities.Count > 0)
                all.AddRange(page.Entities.Where(m => !AgentMessage.IsWorkflowOrchestrationRow(m)));
            token = page.ContinuationToken;
            if (string.IsNullOrEmpty(token) || all.Count >= maxMessages)
                break;
        } while (!cancellationToken.IsCancellationRequested);

        return all.OrderBy(m => m.Sequence).ToList();
    }

    public async Task<AgentMessage?> GetWorkflowOrchestrationRowAsync(
        string tenantId,
        string ownerUserId,
        string conversationId,
        CancellationToken cancellationToken = default) =>
        await FetchEntityAsync(
            tenantId,
            AgentMessage.WorkflowOrchestrationDocumentId,
            AgentMessageModelId,
            ownerUserId,
            conversationId);

    public Task<AgentMessage> UpsertWorkflowOrchestrationRowAsync(
        string tenantId,
        AgentMessage workflowRow,
        CancellationToken cancellationToken = default) =>
        UpsertEntityAsync(tenantId, AgentMessageModelId, workflowRow, typeof(AgentMessage));

    public async Task<AgentMessage> AppendMessageAsync(string tenantId, AgentMessage message)
    {
        return await UpsertEntityAsync(tenantId, AgentMessageModelId, message, typeof(AgentMessage));
    }

    public async Task<IReadOnlyList<AgentMessage>> ListRecentMessagesForUserAsync(
        string tenantId,
        string ownerUserId,
        int maxMessages = 500,
        CancellationToken cancellationToken = default)
    {
        maxMessages = Math.Clamp(maxMessages, 1, 1000);
        var page = await GetEntitiesByFiltersAsync(
            tenantId,
            AgentMessageModelId,
            "c.ownerUserId = @owner AND c.tenantId = @tenant",
            new Dictionary<string, object>
            {
                { "owner", ownerUserId },
                { "tenant", tenantId.ToLowerInvariant() }
            },
            "lastUpdated",
            SortOrder.DESC,
            maxMessages,
            string.Empty);
        var rows = page.Entities ?? new List<AgentMessage>();
        return rows.Where(m => !AgentMessage.IsWorkflowOrchestrationRow(m)).ToList();
    }

    public async Task<AgentMessageConversationLoad> LoadConversationForAuditAsync(
        string tenantId,
        string conversationId,
        int maxMessages = 500,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        var normalizedConversationId = AgentMessageConversationIdNormalizer.Normalize(conversationId);

        var ownerUserId = await ResolveOwnerUserIdAsync(
            tenantId, normalizedConversationId, cancellationToken);

        var chatMessages = await ListMessagesAsync(
            tenantId, ownerUserId, normalizedConversationId, maxMessages, cancellationToken);

        var workflowRow = await GetWorkflowOrchestrationRowAsync(
            tenantId, ownerUserId, normalizedConversationId, cancellationToken);

        return new AgentMessageConversationLoad(
            tenantId,
            ownerUserId,
            normalizedConversationId,
            chatMessages,
            workflowRow);
    }

    private async Task<string> ResolveOwnerUserIdAsync(
        string tenantId,
        string normalizedConversationId,
        CancellationToken cancellationToken)
    {
        string? ownerUserId = null;
        string? token = null;
        do
        {
            var page = await GetEntitiesByFiltersAsync(
                tenantId,
                AgentMessageModelId,
                "c.conversationid = @conv",
                new Dictionary<string, object>
                {
                    { "@conv", normalizedConversationId }
                },
                "sequence",
                SortOrder.ASC,
                50,
                token ?? string.Empty);

            foreach (var row in page.Entities ?? [])
            {
                if (string.IsNullOrWhiteSpace(row.OwnerUserId))
                    continue;
                if (ownerUserId is null)
                    ownerUserId = row.OwnerUserId;
                else if (!string.Equals(ownerUserId, row.OwnerUserId, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"AgentMessage conversation '{normalizedConversationId}' has conflicting ownerUserId values '{ownerUserId}' and '{row.OwnerUserId}'.");
            }

            token = page.ContinuationToken;
            if (string.IsNullOrEmpty(token))
                break;
        } while (!cancellationToken.IsCancellationRequested);

        if (ownerUserId is null)
            throw new InvalidOperationException(
                $"No AgentMessage rows found for tenant '{tenantId}' and conversation '{normalizedConversationId}'.");

        return ownerUserId;
    }

    public override async Task<AgentMessage> UpsertEntityAsync(
        string tenantId,
        string modelId,
        AgentMessage entity,
        Type? entityType = default)
    {
        AgentMessageRetention.Apply(entity, _configuration);
        return await base.UpsertEntityAsync(tenantId, modelId, entity, entityType);
    }
}
