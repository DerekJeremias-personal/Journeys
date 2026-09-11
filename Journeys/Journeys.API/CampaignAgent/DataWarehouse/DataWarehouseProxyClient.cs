using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Journeys.API.CampaignAgent.DataWarehouse;

public sealed class DataWarehouseProxyClient : IDataWarehouseProxyClient
{
    private readonly HttpClient _http;
    private readonly DataWarehouseProxyOptions _options;
    private readonly ILogger<DataWarehouseProxyClient> _logger;

    public DataWarehouseProxyClient(
        HttpClient http,
        IOptions<DataWarehouseProxyOptions> options,
        ILogger<DataWarehouseProxyClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public Task<string> GetProgramPerformanceSummaryAsync(
        string tenantId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default) =>
        GetAsync(tenantId, "program-performance", start, end, null, cancellationToken);

    public Task<string> GetCampaignOutcomeStatsAsync(
        string tenantId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default) =>
        GetAsync(tenantId, "campaign-outcomes", start, end, null, cancellationToken);

    public Task<string> GetMemberEngagementBandsAsync(
        string tenantId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default) =>
        GetAsync(tenantId, "member-engagement-bands", start, end, null, cancellationToken);

    public Task<string> GetProductCategoryLiftAsync(
        string tenantId, DateTimeOffset start, DateTimeOffset end, int topN, CancellationToken cancellationToken = default) =>
        GetAsync(tenantId, "product-category-lift", start, end, topN, cancellationToken);

    private async Task<string> GetAsync(
        string tenantId,
        string routeSegment,
        DateTimeOffset start,
        DateTimeOffset end,
        int? topN,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ProxyBaseUrl))
        {
            return JsonSerializer.Serialize(new
            {
                error = true,
                message = "Data warehouse proxy is not configured (CampaignAgent:DataWarehouseMcp:ProxyBaseUrl)."
            });
        }

        var baseUrl = _options.ProxyBaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/tenants/{Uri.EscapeDataString(tenantId)}/{routeSegment}" +
                  $"?start={Uri.EscapeDataString(start.ToString("O"))}&end={Uri.EscapeDataString(end.ToString("O"))}" +
                  (topN.HasValue ? $"&topN={topN.Value}" : "") +
                  $"&maxRows={_options.MaxRowsPerTool}";

        try
        {
            using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Data warehouse proxy {Route} returned {Status} for tenant {TenantId}",
                    routeSegment,
                    (int)response.StatusCode,
                    tenantId);
                return JsonSerializer.Serialize(new
                {
                    error = true,
                    status = (int)response.StatusCode,
                    message = Truncate(body, 500)
                });
            }

            return string.IsNullOrWhiteSpace(body)
                ? JsonSerializer.Serialize(new { ok = true, data = Array.Empty<object>() })
                : body;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Data warehouse proxy call failed for {Route} tenant {TenantId}", routeSegment, tenantId);
            return JsonSerializer.Serialize(new { error = true, message = ex.Message });
        }
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s.Substring(0, max) + "…";
}
