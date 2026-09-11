namespace Journeys.CampaignAgent.Remediation;

public sealed class RemediationHint
{
    public string? Field { get; init; }

    public required string Issue { get; init; }

    public int? Limit { get; init; }

    public required string Action { get; init; }
}
