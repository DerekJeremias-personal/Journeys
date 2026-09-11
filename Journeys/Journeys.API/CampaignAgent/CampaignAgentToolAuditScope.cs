namespace Journeys.API.CampaignAgent;

/// <summary>
/// Per-turn context for tool audit (blob path + correlation fields).
/// </summary>
internal sealed class CampaignAgentToolAuditScope
{
    public CampaignAgentToolAuditScope(
        bool enabled,
        string rootPrefix,
        string tenantId,
        string ownerUserId,
        string conversationId,
        string requestId,
        string? modelId,
        DateTime auditDayUtc,
        int maxPreviewChars)
    {
        Enabled = enabled;
        RootPrefix = rootPrefix.Trim().TrimEnd('/');
        TenantId = tenantId;
        OwnerUserId = ownerUserId;
        ConversationId = conversationId;
        RequestId = requestId;
        ModelId = modelId;
        AuditDayUtc = auditDayUtc.Date;
        MaxPreviewChars = Math.Clamp(maxPreviewChars, 256, 16_384);
    }

    public bool Enabled { get; }
    public string RootPrefix { get; }
    public string TenantId { get; }
    public string OwnerUserId { get; }
    public string ConversationId { get; }
    public string RequestId { get; }
    public string? ModelId { get; }
    public DateTime AuditDayUtc { get; }
    public int MaxPreviewChars { get; }
}
