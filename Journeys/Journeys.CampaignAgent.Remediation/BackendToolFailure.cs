namespace Journeys.CampaignAgent.Remediation;

public enum BackendToolFailureParseStatus
{
    NotAttempted,
    Success,
    NotJson,
    NotFailure
}

public sealed class BackendToolFailure
{
    public required string ToolName { get; init; }

    public BackendToolFailureParseStatus ParseStatus { get; init; }

    public IReadOnlyDictionary<string, string> ValidationErrors { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public string? Message { get; init; }

    public string? Code { get; init; }

    public string? RawJson { get; init; }
}
