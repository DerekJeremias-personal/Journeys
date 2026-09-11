namespace Journeys.API.CampaignAgent;

/// <summary>
/// Entity IDs inferred from persisted campaign-agent conversation rows for optional cleanup.
/// </summary>
public sealed class AgentConversationClearManifest
{
    public List<CampaignDeleteRef> Campaigns { get; } = new();

    public List<string> PointAccountTypeIds { get; } = new();

    /// <summary>Backend MCP SaveModel targets (model name + entity id when discoverable).</summary>
    public List<SaveModelEntityRef> SaveModelEntities { get; } = new();
}

public sealed class CampaignDeleteRef : IEquatable<CampaignDeleteRef>
{
    public CampaignDeleteRef(string campaignId, string status)
    {
        CampaignId = campaignId;
        Status = status;
    }

    public string CampaignId { get; }
    public string Status { get; }

    public bool Equals(CampaignDeleteRef? other) =>
        other is not null
        && string.Equals(CampaignId, other.CampaignId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(Status, other.Status, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as CampaignDeleteRef);

    public override int GetHashCode() =>
        HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(CampaignId), StringComparer.OrdinalIgnoreCase.GetHashCode(Status));
}

public sealed class SaveModelEntityRef : IEquatable<SaveModelEntityRef>
{
    public SaveModelEntityRef(string? modelName, string? entityId)
    {
        ModelName = modelName;
        EntityId = entityId;
    }

    public string? ModelName { get; }
    public string? EntityId { get; }

    public bool Equals(SaveModelEntityRef? other) =>
        other is not null
        && string.Equals(ModelName, other.ModelName, StringComparison.OrdinalIgnoreCase)
        && string.Equals(EntityId, other.EntityId, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as SaveModelEntityRef);

    public override int GetHashCode() => HashCode.Combine(ModelName, EntityId);
}
