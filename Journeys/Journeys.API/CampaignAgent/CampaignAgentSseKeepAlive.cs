namespace Journeys.API.CampaignAgent;

/// <summary>
/// Emits SSE comment heartbeats during idle gaps so proxies/gateways see bytes on long tool turns.
/// </summary>
internal sealed class CampaignAgentSseKeepAlive : IAsyncDisposable
{
    private readonly Func<Task> _writeKeepAliveAsync;
    private readonly Func<DateTimeOffset> _lastWriteUtc;
    private readonly TimeSpan _interval;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;

    public CampaignAgentSseKeepAlive(
        Func<Task> writeKeepAliveAsync,
        Func<DateTimeOffset> lastWriteUtc,
        TimeSpan? interval = null)
    {
        _writeKeepAliveAsync = writeKeepAliveAsync;
        _lastWriteUtc = lastWriteUtc;
        _interval = interval ?? TimeSpan.FromSeconds(15);
        _loop = RunAsync(_cts.Token);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                if (DateTimeOffset.UtcNow - _lastWriteUtc() < _interval)
                    continue;

                await _writeKeepAliveAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            /* expected on dispose */
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            /* expected */
        }

        _cts.Dispose();
    }
}
