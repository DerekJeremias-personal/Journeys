namespace Journeys.API.CampaignAgent;

/// <summary>
/// Runs a single campaign-agent chat turn with persistence and SSE output.
/// </summary>
public interface ICampaignAgentOrchestrator
{
    /// <summary>
    /// Writes Server-Sent Events to <paramref name="responseBody"/> (event + data lines, UTF-8).
    /// When <paramref name="conversationId"/> is null or whitespace, a new thread id is generated and emitted on the <c>started</c> event.
    /// </summary>
    Task RunStreamingTurnAsync(
        string tenantId,
        string userId,
        string? conversationId,
        string userMessage,
        string? linkedCampaignId,
        string? clientMessageId,
        Stream responseBody,
        CancellationToken cancellationToken = default);
}
