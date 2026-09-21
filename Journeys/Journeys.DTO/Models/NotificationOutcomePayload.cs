namespace Journeys.DTO.Models;

public sealed class NotificationOutcomePayload
{
    public string TenantId { get; set; } = string.Empty;
    public string LoyaltyAccountId { get; set; } = string.Empty;
    public string? ExtAccountId { get; set; }
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string IssuingOutcomeId { get; set; } = string.Empty;
    public string IssuingOutcomeKind { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? EventModelId { get; set; }
    public DateTimeOffset AwardedAtUtc { get; set; }
}
