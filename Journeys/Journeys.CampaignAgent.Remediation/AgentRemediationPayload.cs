namespace Journeys.CampaignAgent.Remediation;

public sealed class AgentRemediationPayload
{
    public int Version { get; init; } = 1;

    public required string Tool { get; init; }

    public bool RetryRecommended { get; init; }

    public IReadOnlyList<string> MatchedRules { get; init; } = Array.Empty<string>();

    public IReadOnlyList<RemediationHint> Hints { get; init; } = Array.Empty<RemediationHint>();
}
