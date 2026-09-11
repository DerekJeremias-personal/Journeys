namespace Journeys.API.CampaignAgent;

public interface ICampaignAgentToolAuditSink
{
    /// <summary>Returns false if the queue dropped the item (bounded channel full).</summary>
    bool TryEnqueue(CampaignAgentToolAuditBatchItem item);
}
