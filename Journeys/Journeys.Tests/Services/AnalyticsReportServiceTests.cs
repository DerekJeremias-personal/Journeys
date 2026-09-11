using Journeys.Core.Interfaces.Services;
using Journeys.Core.Services;
using Journeys.DTO.Models;
using Journeys.DTO.Models.Analytics;
using Journeys.DTO.Responses;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using static Journeys.Core.Services.DatawarehouseService;

namespace Journeys.Tests.Services;

public class AnalyticsReportServiceTests
{
    [Fact]
    public async Task GetReportAsync_maps_databricks_rows_to_analytics_report()
    {
        var queryService = new StubDatabricksQueryService(new DatabricksNormalizedResult(
            StatementId: "statement-1",
            State: "SUCCEEDED",
            Truncated: false,
            TotalRowCount: 2,
            Entities:
            [
                new Dictionary<string, object?>
                {
                    ["storeId"] = "Store-101",
                    ["channel"] = "curbside",
                    ["orderCount"] = "10",
                    ["profileCount"] = "9",
                    ["revenue"] = "1234.50",
                    ["averageOrderValue"] = "123.45",
                    ["totalOrders"] = "12",
                    ["distinctProfiles"] = "10",
                    ["totalRevenue"] = "1934.50",
                    ["totalAverageOrderValue"] = "161.21",
                    ["hasEcomChannel"] = "0",
                    ["matrixRowCount"] = "2",
                    ["dataAsOf"] = "2026-06-16T13:49:58.647Z"
                },
                new Dictionary<string, object?>
                {
                    ["storeId"] = "Store-202",
                    ["channel"] = "store",
                    ["orderCount"] = 2,
                    ["profileCount"] = 2,
                    ["revenue"] = 700m,
                    ["averageOrderValue"] = 350m,
                    ["totalOrders"] = 12,
                    ["distinctProfiles"] = 10,
                    ["totalRevenue"] = 1934.50m,
                    ["totalAverageOrderValue"] = 161.21m,
                    ["hasEcomChannel"] = 0,
                    ["matrixRowCount"] = 2,
                    ["dataAsOf"] = "2026-06-16T13:49:58.647Z"
                }
            ]));

        var service = CreateService(queryService);

        var result = await service.GetReportAsync(
            "mericantires",
            AnalyticsReportService.SalesByChannelAndStoreReportKey,
            new AnalyticsReportRequest { Limit = 1000 });

        Assert.Equal(AnalyticsReportService.SalesByChannelAndStoreReportKey, result.ReportKey);
        Assert.Equal(2, result.RowCount);
        Assert.False(result.Truncated);
        Assert.Equal("UTC", result.Timezone);
        Assert.Contains(result.Columns, column => column.Key == "revenue" && column.Format == "currency");
        Assert.DoesNotContain(result.Warnings, warning => warning.Contains("ecom", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Store-101", result.Rows[0]["storeId"]);
        Assert.Equal(1234.50m, result.Rows[0]["revenue"]);
        Assert.Equal(12L, result.Summary["totalOrders"]);
        Assert.Equal(10L, result.Summary["distinctProfiles"]);
        Assert.Equal(1934.50m, result.Summary["totalRevenue"]);
        Assert.Contains("`dev-catalog`.`mericantires`.`mericantires_orders_delta_table`", queryService.LastQuery);
        Assert.Contains("row_number() OVER", queryService.LastQuery, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("summary AS", queryService.LastQuery, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("max(event_channel)", queryService.LastQuery, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("117003-99-18", queryService.LastQuery, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("117003-99-20", queryService.LastQuery, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetReportAsync_maps_channel_mix_report()
    {
        var queryService = new StubDatabricksQueryService(new DatabricksNormalizedResult(
            StatementId: "statement-1",
            State: "SUCCEEDED",
            Truncated: false,
            TotalRowCount: 1,
            Entities:
            [
                new Dictionary<string, object?>
                {
                    ["channel"] = "online",
                    ["orderCount"] = 10,
                    ["profileCount"] = 8,
                    ["revenue"] = 1000m,
                    ["averageOrderValue"] = 100m,
                    ["revenueShare"] = 50m,
                    ["totalOrders"] = 20,
                    ["distinctProfiles"] = 15,
                    ["totalRevenue"] = 2000m,
                    ["totalAverageOrderValue"] = 100m,
                    ["hasEcomChannel"] = 0,
                    ["matrixRowCount"] = 1,
                    ["dataAsOf"] = "2026-06-16T13:49:58.647Z"
                }
            ]));

        var service = CreateService(queryService);

        var result = await service.GetReportAsync(
            "mericantires",
            AnalyticsReportService.ChannelMixReportKey,
            new AnalyticsReportRequest { Limit = 1000 });

        Assert.Equal(AnalyticsReportService.ChannelMixReportKey, result.ReportKey);
        Assert.Contains(result.Columns, column => column.Key == "revenueShare" && column.Format == "percent");
        Assert.Equal("online", result.Rows[0]["channel"]);
        Assert.Equal(50m, result.Rows[0]["revenueShare"]);
        Assert.Equal(2000m, result.Summary["totalRevenue"]);
        Assert.Contains("channel_mix AS", queryService.LastQuery, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("117003-99-18", queryService.LastQuery, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetReportAsync_maps_store_leaderboard_report()
    {
        var queryService = new StubDatabricksQueryService(new DatabricksNormalizedResult(
            StatementId: "statement-1",
            State: "SUCCEEDED",
            Truncated: false,
            TotalRowCount: 1,
            Entities:
            [
                new Dictionary<string, object?>
                {
                    ["rank"] = 1,
                    ["storeId"] = "Online",
                    ["orderCount"] = 10,
                    ["profileCount"] = 8,
                    ["revenue"] = 1000m,
                    ["averageOrderValue"] = 100m,
                    ["totalOrders"] = 20,
                    ["distinctProfiles"] = 15,
                    ["totalRevenue"] = 2000m,
                    ["totalAverageOrderValue"] = 100m,
                    ["hasEcomChannel"] = 0,
                    ["matrixRowCount"] = 1,
                    ["dataAsOf"] = "2026-06-16T13:49:58.647Z"
                }
            ]));

        var service = CreateService(queryService);

        var result = await service.GetReportAsync(
            "mericantires",
            AnalyticsReportService.StoreLeaderboardReportKey,
            new AnalyticsReportRequest { Limit = 1000 });

        Assert.Equal(AnalyticsReportService.StoreLeaderboardReportKey, result.ReportKey);
        Assert.Contains(result.Columns, column => column.Key == "rank" && column.Format == "integer");
        Assert.Equal(1L, result.Rows[0]["rank"]);
        Assert.Equal("Online", result.Rows[0]["storeId"]);
        Assert.Equal(1000m, result.Rows[0]["revenue"]);
        Assert.Contains("store_leaderboard AS", queryService.LastQuery, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("117003-99-20", queryService.LastQuery, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetReportAsync_rejects_unsupported_report_key()
    {
        var service = CreateService(new StubDatabricksQueryService());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetReportAsync("mericantires", "unknownReport", null));
    }

    [Fact]
    public async Task GetReportAsync_caps_limit_and_marks_truncated()
    {
        var queryService = new StubDatabricksQueryService(new DatabricksNormalizedResult(
            StatementId: "statement-1",
            State: "SUCCEEDED",
            Truncated: false,
            TotalRowCount: 1,
            Entities:
            [
                new Dictionary<string, object?>
                {
                    ["storeId"] = "Online",
                    ["channel"] = "online",
                    ["orderCount"] = 1,
                    ["profileCount"] = 1,
                    ["revenue"] = 100m,
                    ["averageOrderValue"] = 100m,
                    ["hasEcomChannel"] = 0,
                    ["matrixRowCount"] = 1
                }
            ]));

        var service = CreateService(queryService);

        var result = await service.GetReportAsync(
            "mericantires",
            AnalyticsReportService.SalesByChannelAndStoreReportKey,
            new AnalyticsReportRequest { Limit = 20000 });

        Assert.Contains("LIMIT 10000", queryService.LastQuery);
        Assert.Contains(result.Warnings, warning => warning.Contains("Source freshness", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(result.DataAsOf);
    }

    [Fact]
    public async Task GetReportAsync_uses_summary_flags_for_truncation()
    {
        var queryService = new StubDatabricksQueryService(new DatabricksNormalizedResult(
            StatementId: "statement-1",
            State: "SUCCEEDED",
            Truncated: false,
            TotalRowCount: 1,
            Entities:
            [
                new Dictionary<string, object?>
                {
                    ["storeId"] = "Store-101",
                    ["channel"] = "store",
                    ["orderCount"] = 1,
                    ["profileCount"] = 1,
                    ["revenue"] = 100m,
                    ["averageOrderValue"] = 100m,
                    ["totalOrders"] = 3,
                    ["distinctProfiles"] = 3,
                    ["totalRevenue"] = 300m,
                    ["totalAverageOrderValue"] = 100m,
                    ["hasEcomChannel"] = 0,
                    ["matrixRowCount"] = 2,
                    ["dataAsOf"] = "2026-06-16T13:49:58.647Z"
                }
            ]));

        var service = CreateService(queryService);

        var result = await service.GetReportAsync(
            "mericantires",
            AnalyticsReportService.SalesByChannelAndStoreReportKey,
            new AnalyticsReportRequest { Limit = 1 });

        Assert.True(result.Truncated);
        Assert.Equal(2, result.RowCount);
        Assert.Contains(result.Warnings, warning => warning.Contains("limited", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Warnings, warning => warning.Contains("ecom", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetReportAsync_uses_local_warehouse_config_only_in_development()
    {
        var queryService = new StubDatabricksQueryService();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["AnalyticsReports:LocalWarehouseConfig:Enabled"] = "true",
                ["AnalyticsReports:LocalWarehouseConfig:mericantires:SchemaFullName"] = "dev-catalog.mericantires",
                ["AnalyticsReports:LocalWarehouseConfig:mericantires:KeyVaultSecretAppId"] = "app-id",
                ["AnalyticsReports:LocalWarehouseConfig:mericantires:KeyVaultSecretPassword"] = "password"
            })
            .Build();
        var service = CreateService(queryService, new EmptyDatawarehouseService(), configuration);

        await service.GetReportAsync(
            "mericantires",
            AnalyticsReportService.SalesByChannelAndStoreReportKey,
            new AnalyticsReportRequest { Limit = 25 });

        Assert.Contains("`dev-catalog`.`mericantires`.`mericantires_orders_delta_table`", queryService.LastQuery);
    }

    private static AnalyticsReportService CreateService(StubDatabricksQueryService queryService) =>
        CreateService(queryService, new StubDatawarehouseService(), new ConfigurationBuilder().Build());

    private static AnalyticsReportService CreateService(
        StubDatabricksQueryService queryService,
        IDatawarehouseService datawarehouseService,
        IConfiguration configuration) =>
        new(
            datawarehouseService,
            queryService,
            configuration);

    private sealed class StubDatawarehouseService : IDatawarehouseService
    {
        public Task<WarehouseConfigDto> UpsertWarehouseConfigAsync(
            string tenantId,
            WarehouseConfigDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(request);

        public Task<PagedResultSetResponse<WarehouseConfigDto>> GetWarehouseConfig(string tenantId) =>
            Task.FromResult(new PagedResultSetResponse<WarehouseConfigDto>
            {
                Entities =
                [
                    new()
                    {
                        TenantId = tenantId,
                        SchemaFullName = "dev-catalog.mericantires",
                        ServicePrincipalName = "principal",
                        KeyVaultSecretAppId = "app-id",
                        KeyVaultSecretPassword = "password",
                        WorkSpaceFolder = "workspace"
                    }
                ],
                Count = 1
            });

        public Task<DatabricksNormalizedResult> Normalize(JsonElement root) =>
            Task.FromResult(new DatabricksNormalizedResult(null, null, false, null, []));
    }

    private sealed class EmptyDatawarehouseService : IDatawarehouseService
    {
        public Task<WarehouseConfigDto> UpsertWarehouseConfigAsync(
            string tenantId,
            WarehouseConfigDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(request);

        public Task<PagedResultSetResponse<WarehouseConfigDto>> GetWarehouseConfig(string tenantId) =>
            Task.FromResult(new PagedResultSetResponse<WarehouseConfigDto>
            {
                Entities = [],
                Count = 0
            });

        public Task<DatabricksNormalizedResult> Normalize(JsonElement root) =>
            Task.FromResult(new DatabricksNormalizedResult(null, null, false, null, []));
    }

    private sealed class StubDatabricksQueryService : IDatabricksQueryService
    {
        private readonly DatabricksNormalizedResult _result;

        public StubDatabricksQueryService()
            : this(new DatabricksNormalizedResult(null, null, false, null, []))
        {
        }

        public StubDatabricksQueryService(DatabricksNormalizedResult result)
        {
            _result = result;
        }

        public string LastQuery { get; private set; } = string.Empty;

        public Task<DatabricksNormalizedResult> RunQueryAsync(
            string tenantId,
            string query,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(_result);
        }

        public Task<DatabricksNormalizedResult> RunQueryAsync(
            string tenantId,
            WarehouseConfigDto warehouseConfig,
            string query,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(_result);
        }
    }
}
