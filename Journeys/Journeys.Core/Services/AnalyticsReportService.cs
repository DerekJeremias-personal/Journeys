using Journeys.Core.Interfaces.Services;
using Journeys.DTO.Models;
using Journeys.DTO.Models.Analytics;
using Microsoft.Extensions.Configuration;
using System.Globalization;

namespace Journeys.Core.Services;

public class AnalyticsReportService : IAnalyticsReportService
{
    public const string SalesByChannelAndStoreReportKey = "salesByChannelAndStore";
    public const string ChannelMixReportKey = "channelMix";
    public const string StoreLeaderboardReportKey = "storeLeaderboard";

    private const int DefaultLimit = 1000;
    private const int HardLimit = 10000;
    private const string Timezone = "UTC";
    private const string EcomChannel = "ecom";
    private const string PostmanTestStoreIdOne = "117003-99-18";
    private const string PostmanTestStoreIdTwo = "117003-99-20";

    private readonly IDatawarehouseService _datawarehouseService;
    private readonly IDatabricksQueryService _databricksQueryService;
    private readonly IConfiguration _configuration;

    public AnalyticsReportService(
        IDatawarehouseService datawarehouseService,
        IDatabricksQueryService databricksQueryService,
        IConfiguration configuration)
    {
        _datawarehouseService = datawarehouseService;
        _databricksQueryService = databricksQueryService;
        _configuration = configuration;
    }

    public async Task<AnalyticsReportResult> GetReportAsync(
        string tenantId,
        string reportKey,
        AnalyticsReportRequest? request,
        CancellationToken cancellationToken = default)
    {
        var warehouseConfig = await GetWarehouseConfigAsync(tenantId);
        var limit = ResolveLimit(request?.Limit);

        if (string.Equals(reportKey, SalesByChannelAndStoreReportKey, StringComparison.OrdinalIgnoreCase))
        {
            var query = BuildSalesByChannelAndStoreQuery(warehouseConfig, limit);
            var result = await _databricksQueryService.RunQueryAsync(tenantId, warehouseConfig, query, cancellationToken);

            return MapReportResult(
                result,
                limit,
                SalesByChannelAndStoreReportKey,
                SalesByChannelAndStoreColumns(),
                MapStoreChannelRow);
        }

        if (string.Equals(reportKey, ChannelMixReportKey, StringComparison.OrdinalIgnoreCase))
        {
            var query = BuildChannelMixQuery(warehouseConfig, limit);
            var result = await _databricksQueryService.RunQueryAsync(tenantId, warehouseConfig, query, cancellationToken);

            return MapReportResult(
                result,
                limit,
                ChannelMixReportKey,
                ChannelMixColumns(),
                MapChannelMixRow);
        }

        if (string.Equals(reportKey, StoreLeaderboardReportKey, StringComparison.OrdinalIgnoreCase))
        {
            var query = BuildStoreLeaderboardQuery(warehouseConfig, limit);
            var result = await _databricksQueryService.RunQueryAsync(tenantId, warehouseConfig, query, cancellationToken);

            return MapReportResult(
                result,
                limit,
                StoreLeaderboardReportKey,
                StoreLeaderboardColumns(),
                MapStoreLeaderboardRow);
        }

        throw new ArgumentException($"Unsupported analytics report '{reportKey}'.", nameof(reportKey));
    }

    private async Task<WarehouseConfigDto> GetWarehouseConfigAsync(string tenantId)
    {
        var config = await _datawarehouseService.GetWarehouseConfig(tenantId);
        if (config == null || config.Entities is not { Count: > 0 })
        {
            return GetLocalWarehouseConfig(tenantId)
                ?? throw new InvalidOperationException($"Databricks Config not found for tenant {tenantId}");
        }

        return config.Entities[0];
    }

    private WarehouseConfigDto? GetLocalWarehouseConfig(string tenantId)
    {
        if (!IsDevelopmentEnvironment() || !_configuration.GetValue<bool>("AnalyticsReports:LocalWarehouseConfig:Enabled"))
        {
            return null;
        }

        var tenantSection = _configuration.GetSection($"AnalyticsReports:LocalWarehouseConfig:{tenantId}");
        var schemaFullName = tenantSection["SchemaFullName"];
        if (string.IsNullOrWhiteSpace(schemaFullName))
        {
            schemaFullName = _configuration["AnalyticsReports:LocalWarehouseConfig:SchemaFullName"];
        }

        if (string.IsNullOrWhiteSpace(schemaFullName))
        {
            return null;
        }

        return new WarehouseConfigDto
        {
            TenantId = tenantId,
            SchemaFullName = schemaFullName,
            ServicePrincipalName = tenantSection["ServicePrincipalName"]
                ?? _configuration["AnalyticsReports:LocalWarehouseConfig:ServicePrincipalName"]
                ?? string.Empty,
            KeyVaultSecretAppId = tenantSection["KeyVaultSecretAppId"]
                ?? _configuration["AnalyticsReports:LocalWarehouseConfig:KeyVaultSecretAppId"]
                ?? _configuration["SERVICE_PRINCIPAL_ID"]
                ?? string.Empty,
            KeyVaultSecretPassword = tenantSection["KeyVaultSecretPassword"]
                ?? _configuration["AnalyticsReports:LocalWarehouseConfig:KeyVaultSecretPassword"]
                ?? _configuration["SERVICE_PRINCIPAL_SECRET"]
                ?? string.Empty,
            WorkSpaceFolder = tenantSection["WorkSpaceFolder"]
                ?? _configuration["AnalyticsReports:LocalWarehouseConfig:WorkSpaceFolder"]
                ?? string.Empty
        };
    }

    private bool IsDevelopmentEnvironment()
    {
        var environment = _configuration["ASPNETCORE_ENVIRONMENT"]
            ?? _configuration["DOTNET_ENVIRONMENT"];

        return string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase);
    }

    private static int ResolveLimit(int? requestedLimit)
    {
        if (!requestedLimit.HasValue || requestedLimit.Value <= 0)
        {
            return DefaultLimit;
        }

        return Math.Min(requestedLimit.Value, HardLimit);
    }

    private static string BuildSalesByChannelAndStoreQuery(WarehouseConfigDto warehouseConfig, int limit)
    {
        var sourceTable = ResolveOrdersSourceTable(warehouseConfig);

        return $"""
            WITH {BuildOrdersCte(sourceTable)},
            summary AS (
                SELECT
                    count(*) AS totalOrders,
                    count(DISTINCT profile_id) AS distinctProfiles,
                    coalesce(sum(revenue), 0) AS totalRevenue,
                    coalesce(avg(revenue), 0) AS totalAverageOrderValue,
                    max(processed_at) AS dataAsOf,
                    max(CASE WHEN lower(channel) = '{EcomChannel}' THEN 1 ELSE 0 END) AS hasEcomChannel
                FROM deduped_orders
            ),
            store_channel AS (
                SELECT
                    store_id AS storeId,
                    channel AS channel,
                    count(*) AS orderCount,
                    count(DISTINCT profile_id) AS profileCount,
                    coalesce(sum(revenue), 0) AS revenue,
                    coalesce(avg(revenue), 0) AS averageOrderValue
                FROM deduped_orders
                GROUP BY store_id, channel
            )
            SELECT
                store_channel.storeId,
                store_channel.channel,
                store_channel.orderCount,
                store_channel.profileCount,
                store_channel.revenue,
                store_channel.averageOrderValue,
                summary.totalOrders,
                summary.distinctProfiles,
                summary.totalRevenue,
                summary.totalAverageOrderValue,
                summary.dataAsOf,
                summary.hasEcomChannel,
                count(*) OVER () AS matrixRowCount
            FROM store_channel
            CROSS JOIN summary
            ORDER BY revenue DESC
            LIMIT {limit}
            """;
    }

    private static string BuildChannelMixQuery(WarehouseConfigDto warehouseConfig, int limit)
    {
        var sourceTable = ResolveOrdersSourceTable(warehouseConfig);

        return $"""
            WITH {BuildOrdersCte(sourceTable)},
            summary AS (
                SELECT
                    count(*) AS totalOrders,
                    count(DISTINCT profile_id) AS distinctProfiles,
                    coalesce(sum(revenue), 0) AS totalRevenue,
                    coalesce(avg(revenue), 0) AS totalAverageOrderValue,
                    max(processed_at) AS dataAsOf,
                    max(CASE WHEN lower(channel) = '{EcomChannel}' THEN 1 ELSE 0 END) AS hasEcomChannel
                FROM deduped_orders
            ),
            channel_mix AS (
                SELECT
                    channel AS channel,
                    count(*) AS orderCount,
                    count(DISTINCT profile_id) AS profileCount,
                    coalesce(sum(revenue), 0) AS revenue,
                    coalesce(avg(revenue), 0) AS averageOrderValue,
                    max(processed_at) AS dataAsOf
                FROM deduped_orders
                GROUP BY channel
            )
            SELECT
                channel_mix.channel,
                channel_mix.orderCount,
                channel_mix.profileCount,
                channel_mix.revenue,
                channel_mix.averageOrderValue,
                CASE
                    WHEN summary.totalRevenue > 0 THEN (channel_mix.revenue / summary.totalRevenue) * 100
                    ELSE 0
                END AS revenueShare,
                channel_mix.dataAsOf,
                summary.totalOrders,
                summary.distinctProfiles,
                summary.totalRevenue,
                summary.totalAverageOrderValue,
                summary.hasEcomChannel,
                count(*) OVER () AS matrixRowCount
            FROM channel_mix
            CROSS JOIN summary
            ORDER BY revenue DESC
            LIMIT {limit}
            """;
    }

    private static string BuildStoreLeaderboardQuery(WarehouseConfigDto warehouseConfig, int limit)
    {
        var sourceTable = ResolveOrdersSourceTable(warehouseConfig);

        return $"""
            WITH {BuildOrdersCte(sourceTable)},
            summary AS (
                SELECT
                    count(*) AS totalOrders,
                    count(DISTINCT profile_id) AS distinctProfiles,
                    coalesce(sum(revenue), 0) AS totalRevenue,
                    coalesce(avg(revenue), 0) AS totalAverageOrderValue,
                    max(processed_at) AS dataAsOf,
                    max(CASE WHEN lower(channel) = '{EcomChannel}' THEN 1 ELSE 0 END) AS hasEcomChannel
                FROM deduped_orders
            ),
            store_leaderboard AS (
                SELECT
                    store_id AS storeId,
                    count(*) AS orderCount,
                    count(DISTINCT profile_id) AS profileCount,
                    coalesce(sum(revenue), 0) AS revenue,
                    coalesce(avg(revenue), 0) AS averageOrderValue,
                    max(processed_at) AS dataAsOf
                FROM deduped_orders
                GROUP BY store_id
            )
            SELECT
                row_number() OVER (ORDER BY store_leaderboard.revenue DESC) AS rank,
                store_leaderboard.storeId,
                store_leaderboard.orderCount,
                store_leaderboard.profileCount,
                store_leaderboard.revenue,
                store_leaderboard.averageOrderValue,
                store_leaderboard.dataAsOf,
                summary.totalOrders,
                summary.distinctProfiles,
                summary.totalRevenue,
                summary.totalAverageOrderValue,
                summary.hasEcomChannel,
                count(*) OVER () AS matrixRowCount
            FROM store_leaderboard
            CROSS JOIN summary
            ORDER BY store_leaderboard.revenue DESC
            LIMIT {limit}
            """;
    }

    private static string ResolveOrdersSourceTable(WarehouseConfigDto warehouseConfig)
    {
        var (catalogName, schemaName) = ResolveSchema(warehouseConfig);
        var tableName = $"{schemaName}_orders_delta_table";

        return $"{QuoteIdentifier(catalogName)}.{QuoteIdentifier(schemaName)}.{QuoteIdentifier(tableName)}";
    }

    private static string BuildOrdersCte(string sourceTable) =>
        $"""
        ranked_orders AS (
            SELECT
                event_orderid,
                event_profileid AS profile_id,
                coalesce(event_channel, 'Unknown') AS channel,
                coalesce(event_storeid, 'Unknown') AS store_id,
                try_cast(event_ordertotal AS DECIMAL(18, 2)) AS revenue,
                processed_timestamp AS processed_at,
                row_number() OVER (
                    PARTITION BY event_orderid
                    ORDER BY processed_timestamp DESC
                ) AS order_rank
            FROM {sourceTable}
            WHERE event_orderid IS NOT NULL
              AND trim(event_orderid) <> ''
              AND NOT (
                  lower(coalesce(event_channel, '')) = '{EcomChannel}'
                  AND event_storeid IN ('{PostmanTestStoreIdOne}', '{PostmanTestStoreIdTwo}')
              )
        ),
        deduped_orders AS (
            SELECT
                event_orderid,
                profile_id,
                channel,
                store_id,
                revenue,
                processed_at
            FROM ranked_orders
            WHERE order_rank = 1
        )
        """;

    private static (string CatalogName, string SchemaName) ResolveSchema(WarehouseConfigDto warehouseConfig)
    {
        if (string.IsNullOrWhiteSpace(warehouseConfig.SchemaFullName))
        {
            throw new InvalidOperationException("Databricks schema is not configured.");
        }

        var parts = warehouseConfig.SchemaFullName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            throw new InvalidOperationException("Databricks schema must include catalog and schema.");
        }

        return (parts[0], parts[1]);
    }

    private static string QuoteIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains('`'))
        {
            throw new InvalidOperationException("Databricks identifier is invalid.");
        }

        return $"`{value}`";
    }

    private static AnalyticsReportResult MapReportResult(
        DatawarehouseService.DatabricksNormalizedResult result,
        int limit,
        string reportKey,
        List<AnalyticsReportColumn> columns,
        Func<IReadOnlyDictionary<string, object?>, Dictionary<string, object?>> mapRow)
    {
        var summary = MapSummary(result.Entities.FirstOrDefault());
        var rows = result.Entities.Select(mapRow).ToList();
        var warnings = new List<string>();
        var matrixRowCount = summary.TryGetValue("matrixRowCount", out var matrixRowCountValue)
            ? Convert.ToInt32(matrixRowCountValue, CultureInfo.InvariantCulture)
            : rows.Count;
        var isTruncated = result.Truncated || matrixRowCount > rows.Count;

        if (isTruncated)
        {
            warnings.Add("Report rows were limited. Use narrower filters if the detail table looks incomplete.");
        }

        if (summary.TryGetValue("hasEcomChannel", out var hasEcomChannel) && IsTrue(hasEcomChannel))
        {
            warnings.Add("The ecom channel is included and still needs demo-data validation.");
        }

        var dataAsOf = rows
            .Select(row => row.TryGetValue("dataAsOf", out var value) ? Convert.ToString(value, CultureInfo.InvariantCulture) : null)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .OrderByDescending(value => value, StringComparer.Ordinal)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(dataAsOf))
        {
            dataAsOf = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            warnings.Add("Source freshness was unavailable; dataAsOf uses query completion time.");
        }

        return new AnalyticsReportResult
        {
            ReportKey = reportKey,
            Columns = columns,
            Rows = rows,
            Summary = summary,
            RowCount = matrixRowCount,
            Truncated = isTruncated,
            DataAsOf = dataAsOf,
            Timezone = Timezone,
            Warnings = warnings
        };
    }

    private static Dictionary<string, object?> MapSummary(IReadOnlyDictionary<string, object?>? row)
    {
        if (row == null)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["totalOrders"] = GetLong(row, "totalOrders"),
            ["distinctProfiles"] = GetLong(row, "distinctProfiles"),
            ["totalRevenue"] = GetDecimal(row, "totalRevenue"),
            ["totalAverageOrderValue"] = GetDecimal(row, "totalAverageOrderValue"),
            ["dataAsOf"] = GetText(row, "dataAsOf"),
            ["hasEcomChannel"] = GetLong(row, "hasEcomChannel") ?? 0,
            ["matrixRowCount"] = GetLong(row, "matrixRowCount") ?? 0
        };
    }

    private static Dictionary<string, object?> MapStoreChannelRow(IReadOnlyDictionary<string, object?> row)
    {
        var orderCount = GetLong(row, "orderCount");
        var revenue = GetDecimal(row, "revenue");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["storeId"] = GetText(row, "storeId") ?? "Unknown",
            ["channel"] = GetText(row, "channel") ?? "Unknown",
            ["orderCount"] = orderCount,
            ["profileCount"] = GetLong(row, "profileCount"),
            ["revenue"] = revenue,
            ["averageOrderValue"] = GetDecimal(row, "averageOrderValue") ?? CalculateAverageOrderValue(revenue, orderCount),
            ["dataAsOf"] = GetText(row, "dataAsOf")
        };
    }

    private static Dictionary<string, object?> MapChannelMixRow(IReadOnlyDictionary<string, object?> row)
    {
        var orderCount = GetLong(row, "orderCount");
        var revenue = GetDecimal(row, "revenue");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["channel"] = GetText(row, "channel") ?? "Unknown",
            ["orderCount"] = orderCount,
            ["profileCount"] = GetLong(row, "profileCount"),
            ["revenue"] = revenue,
            ["averageOrderValue"] = GetDecimal(row, "averageOrderValue") ?? CalculateAverageOrderValue(revenue, orderCount),
            ["revenueShare"] = GetDecimal(row, "revenueShare"),
            ["dataAsOf"] = GetText(row, "dataAsOf")
        };
    }

    private static Dictionary<string, object?> MapStoreLeaderboardRow(IReadOnlyDictionary<string, object?> row)
    {
        var orderCount = GetLong(row, "orderCount");
        var revenue = GetDecimal(row, "revenue");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["rank"] = GetLong(row, "rank"),
            ["storeId"] = GetText(row, "storeId") ?? "Unknown",
            ["orderCount"] = orderCount,
            ["profileCount"] = GetLong(row, "profileCount"),
            ["revenue"] = revenue,
            ["averageOrderValue"] = GetDecimal(row, "averageOrderValue") ?? CalculateAverageOrderValue(revenue, orderCount),
            ["dataAsOf"] = GetText(row, "dataAsOf")
        };
    }

    private static List<AnalyticsReportColumn> SalesByChannelAndStoreColumns() =>
    [
        new() { Key = "storeId", Label = "Store", Type = "string", Role = "dimension" },
        new() { Key = "channel", Label = "Channel", Type = "string", Role = "dimension" },
        new() { Key = "orderCount", Label = "Orders", Type = "number", Role = "metric", Format = "integer" },
        new() { Key = "profileCount", Label = "Profiles", Type = "number", Role = "metric", Format = "integer" },
        new() { Key = "revenue", Label = "Revenue", Type = "number", Role = "metric", Format = "currency" },
        new() { Key = "averageOrderValue", Label = "Average order value", Type = "number", Role = "metric", Format = "currency" },
        new() { Key = "dataAsOf", Label = "Data as of", Type = "dateTime", Role = "time" }
    ];

    private static List<AnalyticsReportColumn> ChannelMixColumns() =>
    [
        new() { Key = "channel", Label = "Channel", Type = "string", Role = "dimension" },
        new() { Key = "orderCount", Label = "Orders", Type = "number", Role = "metric", Format = "integer" },
        new() { Key = "profileCount", Label = "Profiles", Type = "number", Role = "metric", Format = "integer" },
        new() { Key = "revenue", Label = "Revenue", Type = "number", Role = "metric", Format = "currency" },
        new() { Key = "averageOrderValue", Label = "Average order value", Type = "number", Role = "metric", Format = "currency" },
        new() { Key = "revenueShare", Label = "Revenue share", Type = "number", Role = "metric", Format = "percent" },
        new() { Key = "dataAsOf", Label = "Data as of", Type = "dateTime", Role = "time" }
    ];

    private static List<AnalyticsReportColumn> StoreLeaderboardColumns() =>
    [
        new() { Key = "rank", Label = "Rank", Type = "number", Role = "dimension", Format = "integer" },
        new() { Key = "storeId", Label = "Store", Type = "string", Role = "dimension" },
        new() { Key = "orderCount", Label = "Orders", Type = "number", Role = "metric", Format = "integer" },
        new() { Key = "profileCount", Label = "Profiles", Type = "number", Role = "metric", Format = "integer" },
        new() { Key = "revenue", Label = "Revenue", Type = "number", Role = "metric", Format = "currency" },
        new() { Key = "averageOrderValue", Label = "Average order value", Type = "number", Role = "metric", Format = "currency" },
        new() { Key = "dataAsOf", Label = "Data as of", Type = "dateTime", Role = "time" }
    ];

    private static string? GetText(IReadOnlyDictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static long? GetLong(IReadOnlyDictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        return value switch
        {
            long longValue => longValue,
            int intValue => intValue,
            decimal decimalValue => (long)decimalValue,
            double doubleValue => (long)doubleValue,
            string text when long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }

    private static decimal? GetDecimal(IReadOnlyDictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        return value switch
        {
            decimal decimalValue => decimalValue,
            double doubleValue => Convert.ToDecimal(doubleValue, CultureInfo.InvariantCulture),
            float floatValue => Convert.ToDecimal(floatValue, CultureInfo.InvariantCulture),
            long longValue => longValue,
            int intValue => intValue,
            string text when decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }

    private static bool IsTrue(object? value)
    {
        return value switch
        {
            bool boolValue => boolValue,
            long longValue => longValue != 0,
            int intValue => intValue != 0,
            decimal decimalValue => decimalValue != 0,
            string text when long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed != 0,
            string text when bool.TryParse(text, out var parsed) => parsed,
            _ => false
        };
    }

    private static decimal? CalculateAverageOrderValue(decimal? revenue, long? orderCount)
    {
        if (!revenue.HasValue || !orderCount.HasValue || orderCount.Value == 0)
        {
            return null;
        }

        return decimal.Round(revenue.Value / orderCount.Value, 2);
    }
}
