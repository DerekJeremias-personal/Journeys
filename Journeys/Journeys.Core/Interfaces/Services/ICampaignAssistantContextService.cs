using Journeys.DTO.Models;

namespace Journeys.Core.Interfaces.Services;

/// <summary>
/// Builds <see cref="CampaignAssistantContextDto"/> for agent grounding (HTTP + MCP).
/// </summary>
public interface ICampaignAssistantContextService
{
    /// <summary>
    /// Loads the campaign and event models; returns null if the campaign does not exist.
    /// </summary>
    Task<CampaignAssistantContextDto?> GetContextAsync(
        string tenantId,
        string campaignId,
        string? status,
        bool includeSampleTemplate,
        CancellationToken cancellationToken = default);
}
