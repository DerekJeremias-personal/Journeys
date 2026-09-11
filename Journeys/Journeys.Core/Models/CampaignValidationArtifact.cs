namespace Journeys.Core.Models;

/// <summary>
/// Last <c>validate_campaign</c> outcome stored in workflow artifacts.
/// </summary>
public sealed class CampaignValidationArtifact
{
    public string? ValidatedAtUtc { get; set; }
    public string? PayloadFingerprint { get; set; }
    public bool IsValid { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public List<string> TopWarningCodes { get; set; } = new();
    public string? Phase { get; set; }
    public bool UpsertFailedSinceValidate { get; set; }
    public List<string> LastErrorCodes { get; set; } = new();
}
