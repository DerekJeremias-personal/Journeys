using Microsoft.Extensions.Hosting;

namespace Journeys.API.CampaignAgent;

/// <summary>
/// After the host has started (Kestrel listening), probes configured MCP URLs and logs when unreachable.
/// </summary>
internal sealed class CampaignAgentMcpStartupValidationHostedService : IHostedService
{
    private readonly IConfiguration _configuration;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CampaignAgentMcpStartupValidationHostedService> _logger;

    public CampaignAgentMcpStartupValidationHostedService(
        IConfiguration configuration,
        IHostApplicationLifetime lifetime,
        IHttpClientFactory httpClientFactory,
        ILogger<CampaignAgentMcpStartupValidationHostedService> logger)
    {
        _configuration = configuration;
        _lifetime = lifetime;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_configuration.GetValue("CampaignAgent:McpProbe:Enabled", true))
            return Task.CompletedTask;
        if (!_configuration.GetValue("CampaignAgent:McpProbe:RunAtStartup", true))
            return Task.CompletedTask;

        _lifetime.ApplicationStarted.Register(RunStartupProbeFireAndForget);
        return Task.CompletedTask;
    }

    private void RunStartupProbeFireAndForget()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await RunStartupProbeAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Host stopping during probe
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Campaign Agent MCP startup probe failed unexpectedly.");
            }
        }, CancellationToken.None);
    }

    private async Task RunStartupProbeAsync()
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.ApplicationStopping);
        var outcomes = await CampaignAgentMcpEndpointProbe.ProbeConfiguredEndpointsAsync(
                _configuration,
                _httpClientFactory,
                linked.Token,
                applyStartupRetries: true,
                startupRetryLogger: _logger)
            .ConfigureAwait(false);

        foreach (var o in outcomes)
        {
            if (o.Skipped)
                continue;
            if (o.Success)
            {
                _logger.LogInformation(
                    "Campaign Agent MCP probe OK: {Key} -> {Url} ({Detail})",
                    o.ConfigKey,
                    o.Url,
                    o.Detail);
            }
            else
            {
                _logger.LogWarning(
                    "Campaign Agent MCP probe FAILED: {Key} -> {Url}. {Detail}. Campaign turns will not include tools from this MCP until it is reachable. Check scheme/port (e.g. http://host:5157 vs https://host:7155), TLS, and that the remote app is running.",
                    o.ConfigKey,
                    o.Url,
                    o.Detail);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
