using System.ComponentModel;
using System.Text.Json;
using Journeys.API.CampaignAgent.DataWarehouse;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace Journeys.API.Mcp;

/// <summary>
/// Read-only MCP tools for campaign data analysis (data warehouse proxy).
/// </summary>
[McpServerToolType]
public class DataWarehouseMcpTools
{
    private readonly IDataWarehouseProxyClient _client;
    private readonly DataWarehouseProxyOptions _options;

    public DataWarehouseMcpTools(
        IDataWarehouseProxyClient client,
        IOptions<DataWarehouseProxyOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    [McpServerTool(Name = "GetProgramPerformanceSummary")]
    [Description("Read-only: program earn/burn, members, redemption summary for a tenant and date window.")]
    public Task<string> GetProgramPerformanceSummary(
        [Description("Tenant id")] string tenantId,
        [Description("ISO start (optional)")] string? startDate = null,
        [Description("ISO end (optional)")] string? endDate = null,
        CancellationToken cancellationToken = default) =>
        RunWindowed(tenantId, startDate, endDate, (t, s, e, ct) =>
            _client.GetProgramPerformanceSummaryAsync(t, s, e, ct), cancellationToken);

    [McpServerTool(Name = "GetCampaignOutcomeStats")]
    [Description("Read-only: campaign outcome statistics for a tenant and date window.")]
    public Task<string> GetCampaignOutcomeStats(
        string tenantId,
        string? startDate = null,
        string? endDate = null,
        CancellationToken cancellationToken = default) =>
        RunWindowed(tenantId, startDate, endDate, (t, s, e, ct) =>
            _client.GetCampaignOutcomeStatsAsync(t, s, e, ct), cancellationToken);

    [McpServerTool(Name = "GetMemberEngagementBands")]
    [Description("Read-only: member engagement band distribution for a tenant and date window.")]
    public Task<string> GetMemberEngagementBands(
        string tenantId,
        string? startDate = null,
        string? endDate = null,
        CancellationToken cancellationToken = default) =>
        RunWindowed(tenantId, startDate, endDate, (t, s, e, ct) =>
            _client.GetMemberEngagementBandsAsync(t, s, e, ct), cancellationToken);

    [McpServerTool(Name = "GetProductCategoryLift")]
    [Description("Read-only: product/category lift for a tenant and date window.")]
    public Task<string> GetProductCategoryLift(
        string tenantId,
        string? startDate = null,
        string? endDate = null,
        [Description("Max categories")] int topN = 10,
        CancellationToken cancellationToken = default) =>
        RunWindowed(tenantId, startDate, endDate, async (t, s, e, ct) =>
            await _client.GetProductCategoryLiftAsync(t, s, e, topN, ct).ConfigureAwait(false), cancellationToken);

    private async Task<string> RunWindowed(
        string tenantId,
        string? startDate,
        string? endDate,
        Func<string, DateTimeOffset, DateTimeOffset, CancellationToken, Task<string>> call,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return JsonSerializer.Serialize(new
            {
                error = true,
                message = "Data warehouse MCP is disabled (CampaignAgent:DataWarehouseMcp:Enabled)."
            });
        }

        var end = ParseDate(endDate) ?? DateTimeOffset.UtcNow;
        var start = ParseDate(startDate)
                    ?? end.AddDays(-Math.Max(1, _options.DefaultAnalysisWindowDays));
        return await call(tenantId.Trim(), start, end, cancellationToken).ConfigureAwait(false);
    }

    private static DateTimeOffset? ParseDate(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? null : DateTimeOffset.TryParse(raw, out var d) ? d : null;
}
