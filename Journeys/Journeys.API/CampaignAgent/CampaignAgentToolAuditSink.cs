using System.Threading.Channels;

namespace Journeys.API.CampaignAgent;

internal sealed class CampaignAgentToolAuditSink : ICampaignAgentToolAuditSink
{
    private readonly ChannelWriter<CampaignAgentToolAuditBatchItem> _writer;

    public CampaignAgentToolAuditSink(ChannelWriter<CampaignAgentToolAuditBatchItem> writer)
    {
        _writer = writer;
    }

    public bool TryEnqueue(CampaignAgentToolAuditBatchItem item) => _writer.TryWrite(item);
}
