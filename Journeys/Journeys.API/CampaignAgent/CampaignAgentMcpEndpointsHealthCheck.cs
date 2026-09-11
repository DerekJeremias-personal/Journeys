using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Journeys.API.CampaignAgent;

/// <summary>
/// Reports reachability of configured Campaign Agent MCP HTTP endpoints (Journeys + Backend).
/// </summary>
public sealed class CampaignAgentMcpEndpointsHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public CampaignAgentMcpEndpointsHealthCheck(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var outcomes = await CampaignAgentMcpEndpointProbe.ProbeConfiguredEndpointsAsync(
            _configuration,
            _httpClientFactory,
            cancellationToken).ConfigureAwait(false);

        if (outcomes.Count == 0)
            return HealthCheckResult.Healthy("Campaign Agent MCP probe disabled (CampaignAgent:McpProbe:Enabled=false).");

        var data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var o in outcomes)
        {
            var label = o.ConfigKey.Replace("CampaignAgent:", "", StringComparison.OrdinalIgnoreCase);
            data[label] = o.Skipped
                ? "not configured"
                : $"{(o.Success ? "ok" : "fail")}: {o.Detail} ({o.Url})";
        }

        var treatAsUnhealthy = _configuration.GetValue("CampaignAgent:McpProbe:TreatUnreachableAsUnhealthy", false);
        var status = CampaignAgentMcpEndpointProbe.WorstStatus(outcomes, treatAsUnhealthy);

        if (status == HealthStatus.Healthy)
            return HealthCheckResult.Healthy("All configured Campaign Agent MCP endpoints responded.", data);

        var failed = outcomes.Where(o => !o.Skipped && !o.Success).Select(o => $"{o.ConfigKey}: {o.Detail}");
        var msg = string.Join("; ", failed);
        return new HealthCheckResult(status, msg, data: data);
    }
}
