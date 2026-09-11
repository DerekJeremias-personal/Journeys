namespace Journeys.DTO.Models;

public sealed class CampaignShellDigest
{
    public int SchemaVersion { get; set; } = 1;
    public string CampaignId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ExtCampaignId { get; set; }
    public List<string> EventModelIds { get; set; } = new();
    public string? StartDateUtc { get; set; }
    public string? EndDateUtc { get; set; }
    public bool HasJourneyPayload { get; set; }
    public List<string> IneligibleEventModelIds { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public sealed class PointAccountManifestDigest
{
    public int SchemaVersion { get; set; } = 1;
    public List<PointAccountManifestItemDigest> Items { get; set; } = new();
}

public sealed class PointAccountManifestItemDigest
{
    public string Id { get; set; } = string.Empty;
    public string DisplayLabel { get; set; } = string.Empty;
    public string? LedgerType { get; set; }
    public bool? IsSpendable { get; set; }
    public string? Status { get; set; }
    public string Role { get; set; } = "other";
}

public sealed class CampaignJourneyArtifactDigest
{
    public int SchemaVersion { get; set; } = 1;
    public string CampaignId { get; set; } = string.Empty;
    public int JourneyNodeCount { get; set; }
    public int RuleSetCount { get; set; }
    public Dictionary<string, int> OutcomeKindCounts { get; set; } = new();
    public List<PointAccountReferenceDigest> ReferencedPointAccountTypes { get; set; } = new();
    public List<string> UnresolvedPatIds { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public sealed class PointAccountReferenceDigest
{
    public string PointAccountTypeId { get; set; } = string.Empty;
    public int UsageCount { get; set; }
    public List<string> OutcomeKinds { get; set; } = new();
    public bool InManifest { get; set; }
}
