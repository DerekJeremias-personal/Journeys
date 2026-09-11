using Journeys.Core.Interfaces.Services;
using Journeys.Core.Utility;
using Journeys.DTO.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using static Journeys.Core.Services.DatawarehouseService;

namespace Journeys.Core.Services;

public class DatabricksQueryService : IDatabricksQueryService
{
    private const int MaxStatementPollAttempts = 12;
    private static readonly TimeSpan StatementPollDelay = TimeSpan.FromSeconds(2);

    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;
    private readonly IDatawarehouseService _datawarehouseService;
    private readonly IWarehouseAuthService _warehouseAuthService;
    private readonly ILogger<DatabricksQueryService> _logger;

    public DatabricksQueryService(
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        IDatawarehouseService datawarehouseService,
        IWarehouseAuthService warehouseAuthService,
        ILogger<DatabricksQueryService> logger)
    {
        _config = config;
        _httpClient = httpClientFactory.CreateClient();
        _datawarehouseService = datawarehouseService;
        _warehouseAuthService = warehouseAuthService;
        _logger = logger;
    }

    public async Task<DatabricksNormalizedResult> RunQueryAsync(
        string tenantId,
        string query,
        CancellationToken cancellationToken = default)
    {
        var databricksConfig = await _datawarehouseService.GetWarehouseConfig(tenantId);
        if (databricksConfig == null || databricksConfig.Entities is not { Count: > 0 })
        {
            throw new InvalidOperationException($"Databricks Config not found for tenant {tenantId}");
        }

        return await RunQueryAsync(tenantId, databricksConfig.Entities[0], query, cancellationToken);
    }

    public async Task<DatabricksNormalizedResult> RunQueryAsync(
        string tenantId,
        WarehouseConfigDto warehouseConfig,
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("SQL query is required", nameof(query));
        }

        if (!SqlValidator.Validate(query, out var validationError))
        {
            throw new ArgumentException(validationError, nameof(query));
        }

        var databricksInstanceUrl = _config["INSTANCE_URL"];
        var warehouseId = _config["DATABRICKS_WAREHOUSE_ID"];

        if (string.IsNullOrWhiteSpace(databricksInstanceUrl))
        {
            _logger.LogError("Databricks INSTANCE_URL is missing in configuration");
            throw new InvalidOperationException("Databricks INSTANCE_URL is not configured");
        }

        if (string.IsNullOrWhiteSpace(warehouseId))
        {
            _logger.LogError("Databricks WAREHOUSE_ID is missing in configuration");
            throw new InvalidOperationException("Databricks warehouse ID is not configured");
        }

        var token = await _warehouseAuthService.GetToken(warehouseConfig);
        var url = $"{databricksInstanceUrl.TrimEnd('/')}/api/2.0/sql/statements";

        var payload = new
        {
            statement = query,
            warehouse_id = warehouseId,
            wait_timeout = "30s"
        };

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var content = await SendStatementRequestAsync(request, tenantId, cancellationToken);
        return await NormalizeCompletedStatementAsync(tenantId, token, content, cancellationToken);
    }

    private async Task<DatabricksNormalizedResult> NormalizeCompletedStatementAsync(
        string tenantId,
        string token,
        string content,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= MaxStatementPollAttempts; attempt++)
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var state = GetStatementState(root);

            if (string.Equals(state, "FAILED", StringComparison.OrdinalIgnoreCase))
            {
                var errorMessage = GetStatementError(root);

                _logger.LogError("Databricks query failed for tenant {TenantId}: {Error}", tenantId, errorMessage);
                throw new InvalidOperationException(errorMessage ?? "Databricks query failed");
            }

            if (string.Equals(state, "CANCELED", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "CLOSED", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Databricks statement ended with state {state}");
            }

            if (string.Equals(state, "SUCCEEDED", StringComparison.OrdinalIgnoreCase) || HasInlineResult(root))
            {
                return await _datawarehouseService.Normalize(root.Clone());
            }

            var statementId = GetStatementId(root);
            if (string.IsNullOrWhiteSpace(statementId))
            {
                throw new InvalidOperationException($"Databricks statement did not return a final result. State: {state ?? "unknown"}");
            }

            if (attempt == MaxStatementPollAttempts)
            {
                throw new TimeoutException($"Databricks statement {statementId} did not finish before polling timed out.");
            }

            await Task.Delay(StatementPollDelay, cancellationToken);
            content = await GetStatementAsync(statementId, tenantId, token, cancellationToken);
        }

        throw new TimeoutException("Databricks statement did not finish before polling timed out.");
    }

    private async Task<string> GetStatementAsync(
        string statementId,
        string tenantId,
        string token,
        CancellationToken cancellationToken)
    {
        var databricksInstanceUrl = _config["INSTANCE_URL"];
        if (string.IsNullOrWhiteSpace(databricksInstanceUrl))
        {
            _logger.LogError("Databricks INSTANCE_URL is missing in configuration");
            throw new InvalidOperationException("Databricks INSTANCE_URL is not configured");
        }

        var url = $"{databricksInstanceUrl.TrimEnd('/')}/api/2.0/sql/statements/{statementId}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await SendStatementRequestAsync(request, tenantId, cancellationToken);
    }

    private async Task<string> SendStatementRequestAsync(
        HttpRequestMessage request,
        string tenantId,
        CancellationToken cancellationToken)
    {
        var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Databricks query failed for tenant {TenantId}: {Content}", tenantId, content);
            throw new InvalidOperationException($"Databricks query failed with status {(int)response.StatusCode}");
        }

        return content;
    }

    private static string? GetStatementId(JsonElement root)
    {
        return root.TryGetProperty("statement_id", out var statementId)
            ? statementId.GetString()
            : null;
    }

    private static string? GetStatementState(JsonElement root)
    {
        return root.TryGetProperty("status", out var status)
            && status.TryGetProperty("state", out var state)
                ? state.GetString()
                : null;
    }

    private static string? GetStatementError(JsonElement root)
    {
        return root.TryGetProperty("status", out var status)
            && status.TryGetProperty("error", out var error)
            && error.TryGetProperty("message", out var message)
                ? message.GetString()
                : null;
    }

    private static bool HasInlineResult(JsonElement root)
    {
        return root.TryGetProperty("manifest", out _)
            && root.TryGetProperty("result", out _);
    }
}
