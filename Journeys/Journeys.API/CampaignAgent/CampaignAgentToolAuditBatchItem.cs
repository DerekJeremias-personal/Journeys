namespace Journeys.API.CampaignAgent;

/// <summary>
/// One logical tool invocation: two append-blob lines (compact + preview sidecar).
/// </summary>
public sealed class CampaignAgentToolAuditBatchItem
{
    public CampaignAgentToolAuditBatchItem(string compactBlobName, string previewBlobName, string compactLine, string previewLine)
    {
        CompactBlobName = compactBlobName;
        PreviewBlobName = previewBlobName;
        CompactLine = compactLine;
        PreviewLine = previewLine;
    }

    public string CompactBlobName { get; }
    public string PreviewBlobName { get; }
    public string CompactLine { get; }
    public string PreviewLine { get; }
}
