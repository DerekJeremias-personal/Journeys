using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using Microsoft.Extensions.Configuration;

namespace Journeys.API.CampaignAgent;

public sealed class CampaignWorkflowStore : ICampaignWorkflowStore
{
    private readonly IAgentMessageAdapter _agentMessages;
    private readonly IConfiguration _configuration;

    public CampaignWorkflowStore(IAgentMessageAdapter agentMessages, IConfiguration configuration)
    {
        _agentMessages = agentMessages;
        _configuration = configuration;
    }

    public async Task<CampaignWorkflowState> GetOrCreateAsync(
        string tenantId,
        string ownerUserId,
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        var row = await _agentMessages
            .GetWorkflowOrchestrationRowAsync(tenantId, ownerUserId, conversationId, cancellationToken)
            .ConfigureAwait(false);
        var existing = CampaignWorkflowState.FromWorkflowRow(row);
        if (existing != null)
            return existing;

        var created = CampaignWorkflowState.CreateDefault(tenantId, ownerUserId, conversationId);
        ApplyDefaultKindFromConfig(created);
        return created;
    }

    public Task SaveAsync(CampaignWorkflowState state, CancellationToken cancellationToken = default)
    {
        state.TenantId = state.TenantId.ToLowerInvariant();
        var row = state.ToWorkflowRow();
        return _agentMessages.UpsertWorkflowOrchestrationRowAsync(state.TenantId, row, cancellationToken);
    }

    private void ApplyDefaultKindFromConfig(CampaignWorkflowState state)
    {
        var raw = _configuration["CampaignAgent:DefaultCampaignWorkflowKind"];
        if (string.IsNullOrWhiteSpace(raw))
            return;
        if (string.Equals(raw.Trim(), "TagFirst", StringComparison.OrdinalIgnoreCase))
        {
            state.CampaignKind = CampaignWorkflowKind.TagFirst;
            state.UserSkippedEventModels = true;
            state.Phase = CampaignWorkflowPhase.CampaignBuild;
            state.ModelGatePassed = true;
        }
    }
}
