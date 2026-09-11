using System.Threading.Channels;
using Journeys.Core.Interfaces.FileStorage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Journeys.API.CampaignAgent;

/// <summary>
/// Drains the tool-audit channel and appends NDJSON lines via <see cref="IFileStorageAdapter.AppendNdjsonLineAsync"/>.
/// </summary>
internal sealed class CampaignAgentToolAuditBackgroundService : BackgroundService
{
    private readonly ChannelReader<CampaignAgentToolAuditBatchItem> _reader;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CampaignAgentToolAuditBackgroundService> _logger;

    public CampaignAgentToolAuditBackgroundService(
        ChannelReader<CampaignAgentToolAuditBatchItem> reader,
        IServiceScopeFactory scopeFactory,
        ILogger<CampaignAgentToolAuditBackgroundService> logger)
    {
        _reader = reader;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            CampaignAgentToolAuditBatchItem item;
            try
            {
                item = await _reader.ReadAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var blob = scope.ServiceProvider.GetRequiredService<IFileStorageAdapter>();
                await blob.AppendNdjsonLineAsync(item.CompactBlobName, item.CompactLine, stoppingToken).ConfigureAwait(false);
                await blob.AppendNdjsonLineAsync(item.PreviewBlobName, item.PreviewLine, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Campaign agent tool audit failed for blobs {Compact} / {Preview}.", item.CompactBlobName, item.PreviewBlobName);
            }
        }
    }
}
