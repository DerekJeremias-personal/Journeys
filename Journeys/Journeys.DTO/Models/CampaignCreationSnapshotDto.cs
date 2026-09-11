namespace Journeys.DTO.Models;

/// <summary>Compact post-creation facts persisted in workflow artifacts for SESSION coaching.</summary>
public sealed class CampaignCreationSnapshotDto
{
    public int SchemaVersion { get; set; } = 1;

    public bool CreationComplete { get; set; }

    public string? CampaignId { get; set; }

    public string? CampaignExternalId { get; set; }

    public string? CampaignStatus { get; set; }

    public List<PointAccountSnapshotItemDto> PointAccountTypes { get; set; } = [];

    public int JourneyRuleSetCount { get; set; }

    public int JourneyOutcomeCount { get; set; }
}

public sealed class PointAccountSnapshotItemDto
{
    public string Id { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string? LedgerType { get; set; }
}
