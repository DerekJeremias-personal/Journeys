using Journeys.Core.Models;

namespace Journeys.API.CampaignAgent;

/// <summary>
/// Loads and saves campaign workflow state for the HTTP campaign agent.
/// </summary>
public interface ICampaignWorkflowStore
{
    /// <summary>Returns persisted state or a new default document (not written until <see cref="SaveAsync"/>).</summary>
    Task<CampaignWorkflowState> GetOrCreateAsync(
        string tenantId,
        string ownerUserId,
        string conversationId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(CampaignWorkflowState state, CancellationToken cancellationToken = default);
}
