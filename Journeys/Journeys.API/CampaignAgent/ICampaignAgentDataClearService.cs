namespace Journeys.API.CampaignAgent;

public interface ICampaignAgentDataClearService
{
    /// <summary>
    /// Deletes entities inferred from persisted <see cref="Journeys.Core.Models.AgentMessage"/> rows for one conversation.
    /// Order: SaveModel (Backend MCP, when delete tool exists) → campaigns → point account types.
    /// </summary>
    Task<ClearDataResult> ClearSessionAsync(
        string tenantId,
        string ownerUserId,
        string conversationId,
        ClearCategoryFlags categories,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tenant-wide delete: pages campaigns and/or point account types and deletes each. SaveModel path is best-effort
    /// via Backend MCP list + delete tools when present; otherwise adds a warning and skips that category.
    /// </summary>
    Task<ClearDataResult> ClearTenantAsync(
        string tenantId,
        ClearCategoryFlags categories,
        CancellationToken cancellationToken = default);
}
