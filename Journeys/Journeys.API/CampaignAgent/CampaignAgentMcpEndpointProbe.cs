using System.Net.Http;
using System.Net.Sockets;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Journeys.API.CampaignAgent;

/// <summary>
/// Lightweight HTTP reachability probe for Campaign Agent MCP base URLs (GET; expects any HTTP response that proves the listener is up).
/// </summary>
internal static class CampaignAgentMcpEndpointProbe
{
    /// <summary>Result for one configured endpoint.</summary>
    public sealed record ProbeOutcome(string ConfigKey, string? Url, bool Skipped, bool Success, string Detail);

    public static Task<IReadOnlyList<ProbeOutcome>> ProbeConfiguredEndpointsAsync(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
        => ProbeConfiguredEndpointsAsync(configuration, httpClientFactory, cancellationToken, applyStartupRetries: false, startupRetryLogger: null);

    /// <param name="applyStartupRetries">When true, retries transient connection failures per <c>CampaignAgent:McpProbe:StartupRetryCount</c>.</param>
    /// <param name="startupRetryLogger">Optional logger for retry attempts (startup validation only).</param>
    public static async Task<IReadOnlyList<ProbeOutcome>> ProbeConfiguredEndpointsAsync(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken,
        bool applyStartupRetries,
        ILogger? startupRetryLogger)
    {
        if (!configuration.GetValue("CampaignAgent:McpProbe:Enabled", true))
        {
            return Array.Empty<ProbeOutcome>();
        }

        var timeoutSeconds = Math.Clamp(configuration.GetValue("CampaignAgent:McpProbe:TimeoutSeconds", 10), 2, 60);
        var results = new List<ProbeOutcome>();

        await ProbeOneAsync(
            configuration,
            httpClientFactory,
            "CampaignAgent:McpEndpointUrl",
            "CampaignAgentMcp",
            timeoutSeconds,
            results,
            cancellationToken,
            applyStartupRetries,
            startupRetryLogger).ConfigureAwait(false);

        await ProbeOneAsync(
            configuration,
            httpClientFactory,
            "CampaignAgent:BackendMcpEndpointUrl",
            "CampaignAgentBackendMcp",
            timeoutSeconds,
            results,
            cancellationToken,
            applyStartupRetries,
            startupRetryLogger).ConfigureAwait(false);

        return results;
    }

    private static async Task ProbeOneAsync(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        string configKey,
        string httpClientName,
        int timeoutSeconds,
        List<ProbeOutcome> results,
        CancellationToken cancellationToken,
        bool applyStartupRetries,
        ILogger? startupRetryLogger)
    {
        var url = configuration[configKey];
        if (string.IsNullOrWhiteSpace(url))
        {
            results.Add(new ProbeOutcome(configKey, null, Skipped: true, Success: true, Detail: "not configured"));
            return;
        }

        var baseUrl = url.TrimEnd('/');
        var extraRetries = applyStartupRetries
            ? Math.Clamp(configuration.GetValue("CampaignAgent:McpProbe:StartupRetryCount", 4), 0, 30)
            : 0;
        var delayMs = Math.Clamp(configuration.GetValue("CampaignAgent:McpProbe:StartupRetryDelayMilliseconds", 300), 50, 10_000);
        var maxAttempts = 1 + extraRetries;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var client = httpClientFactory.CreateClient(httpClientName);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                using var request = new HttpRequestMessage(HttpMethod.Get, baseUrl);
                var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                    .ConfigureAwait(false);
                var code = (int)response.StatusCode;
                var detail = $"HTTP {code} {response.ReasonPhrase}";
                if (attempt > 1)
                    detail += $" (after {attempt} attempts)";
                results.Add(new ProbeOutcome(
                    configKey,
                    baseUrl,
                    Skipped: false,
                    Success: true,
                    Detail: detail));
                return;
            }
            catch (OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw;

                // HttpClient timeout uses TaskCanceledException derived from OperationCanceledException
                var detail = $"Request timed out after {timeoutSeconds}s";
                if (attempt > 1)
                    detail += $" (after {attempt} attempts)";
                results.Add(new ProbeOutcome(configKey, baseUrl, Skipped: false, Success: false, Detail: detail));
                return;
            }
            catch (Exception ex)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var canRetry = applyStartupRetries
                    && attempt < maxAttempts
                    && IsTransientConnectionFailure(ex);

                if (!canRetry)
                {
                    var detail = ex.Message;
                    if (attempt > 1)
                        detail += $" (after {attempt} attempts)";
                    results.Add(new ProbeOutcome(configKey, baseUrl, Skipped: false, Success: false, Detail: detail));
                    return;
                }

                startupRetryLogger?.LogInformation(
                    "Campaign Agent MCP probe transient failure for {ConfigKey} -> {Url} (attempt {Attempt}/{Max}); retrying in {DelayMs}ms. {Message}",
                    configKey, baseUrl, attempt, maxAttempts, delayMs, ex.Message);

                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Connection refused (not yet listening) — safe to retry during host startup.</summary>
    private static bool IsTransientConnectionFailure(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is SocketException se && se.SocketErrorCode == SocketError.ConnectionRefused)
                return true;
        }

        return false;
    }

    public static HealthStatus WorstStatus(IReadOnlyList<ProbeOutcome> outcomes, bool treatFailureAsUnhealthy)
    {
        var hasFailure = outcomes.Any(o => !o.Skipped && !o.Success);
        if (!hasFailure)
            return HealthStatus.Healthy;
        return treatFailureAsUnhealthy ? HealthStatus.Unhealthy : HealthStatus.Degraded;
    }
}
