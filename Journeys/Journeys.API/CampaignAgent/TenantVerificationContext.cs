namespace Journeys.API.CampaignAgent;

public sealed record TenantVerificationContext(
    bool LoadSucceeded,
    bool LoadFailed,
    bool BlockedNoAllowlist,
    IReadOnlyList<string> Allowlist);
