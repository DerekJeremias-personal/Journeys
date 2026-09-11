namespace Journeys.API.CampaignAgent;

/// <summary>
/// Subset of tenant registry data merged into campaign-agent LLM instructions (not persisted as chat).
/// </summary>
public sealed record CampaignAgentTenantLlmSlice(
    string? MarketingContext,
    string? EndConsumerContext,
    string? MarketingContextAsOf,
    string? EndConsumerContextAsOf,
    string CacheVersion,
    IReadOnlyList<string>? CampaignTestAccountExtIds = null)
{
    public bool HasVerificationAllowlistConfigured => CampaignTestAccountExtIds != null;

    public bool HasAnyContext =>
        !string.IsNullOrWhiteSpace(MarketingContext)
        || !string.IsNullOrWhiteSpace(EndConsumerContext)
        || HasVerificationAllowlistConfigured;
}
