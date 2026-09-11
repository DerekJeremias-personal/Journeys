using Journeys.Core.Interfaces.Services;
using Journeys.Core.Utility;
using Journeys.DTO.Models;
using Journeys.DTO.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Journeys.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DatabricksController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;
        private readonly IDatawarehouseService _warehouserConfigService;
        private readonly IWarehouseAuthService _warehouseAuthService;
        private readonly ILogger<DatabricksController> _logger;

        public DatabricksController(IDatawarehouseService warehouserConfigService, IWarehouseAuthService warehouseAuthService, ILogger<DatabricksController> logger, IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _warehouserConfigService = warehouserConfigService;
            _warehouseAuthService = warehouseAuthService;
            _logger = logger;
            _config = config;
            _httpClient = httpClientFactory.CreateClient();
        }

        [HttpGet("token")]
        public async Task<IActionResult> GetScopedToken([FromQuery] string dashboardId,[FromQuery] string viewerId, [FromQuery] string value)
        {
            if (string.IsNullOrEmpty(viewerId) || string.IsNullOrEmpty(value))
                return BadRequest(new { error = "viewerId and value are required" });

            try
            {
                string servicePrincipalId = _config["SERVICE_PRINCIPAL_ID"];
                string servicePrincipalSecret = _config["SERVICE_PRINCIPAL_SECRET"];
                // Basic auth header
                var basicAuthHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{servicePrincipalId}:{servicePrincipalSecret}"));

                // Step 1: Get OIDC token with all-apis scope
                string oidcToken = await _warehouseAuthService.RequestDashboardTokenAsync(
                    new Dictionary<string, string>
                    {
                        { "grant_type", "client_credentials" },
                        { "scope", "all-apis" }
                    }, basicAuthHeader
                    );

                // Step 2: Get dashboard token info
                var dashboardTokenInfo = await _warehouseAuthService.GetDashboardTokenInfoAsync(dashboardId, viewerId, value, oidcToken);

                // Step 3: Generate scoped dashboard token using the same RequestTokenAsync method
                string authorizationDetails = dashboardTokenInfo.GetProperty("authorization_details").ToString();
                var scopedTokenRequestData = new Dictionary<string, string>();
                foreach (var property in dashboardTokenInfo.EnumerateObject())
                {
                    if (property.Name != "authorization_details")
                        scopedTokenRequestData.Add(property.Name, property.Value.GetRawText().Trim('"'));
                }
                scopedTokenRequestData.Add("grant_type", "client_credentials");
                scopedTokenRequestData.Add("authorization_details", authorizationDetails);

                string scopedDashboardToken = await _warehouseAuthService.RequestDashboardTokenAsync(scopedTokenRequestData, basicAuthHeader);

                return Ok(new { token = scopedDashboardToken });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("{tenantId}/config/save")]
        public async Task<ActionResult<WarehouseConfigDto>> UpsertDatabricksConfigAsync(string tenantId, [FromBody] WarehouseConfigDto modelDefinitionDto, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                return BadRequest(new { error = "Tenant ID is required." });

            if (modelDefinitionDto == null)
                return BadRequest(new { error = "Request body is required." });

            try
            {
                var ret = await _warehouserConfigService.UpsertWarehouseConfigAsync(tenantId, modelDefinitionDto, cancellationToken);
                return Ok(ret);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving DatabricksConfig model for tenant {TenantId}", tenantId);
                return StatusCode(500, new
                {
                    message = "An unexpected error occurred while saving the WarehouseConfig model.",
                    error = ex.Message
                });
            }
        }

        [HttpGet("{tenantId}/config/get")]
        public async Task<IActionResult> GetDatabricksConfig(string tenantId)
        {
            try
            {
                var response = await _warehouserConfigService.GetWarehouseConfig(tenantId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving DatabricksConfig for tenant {TenantId}", tenantId);
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("{tenantId}/query")]
        public async Task<IActionResult> RunQuery(string tenantId, [FromBody] DatabricksViewRawQueryRequest request, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Query))
                    return BadRequest(new { error = "SQL query is required" });

                // Validate SQL query
                if (!SqlValidator.Validate(request.Query, out var validationError))
                    return BadRequest(new { error = validationError });

                // fetch databricks config for tenant
                var databricksConfig = await _warehouserConfigService.GetWarehouseConfig(tenantId);
                if (databricksConfig == null || databricksConfig.Entities is not { Count: > 0 })
                {
                    return BadRequest(new { error = $"Databricks Config not found for tenant {tenantId}" });
                }


                // get token using client credentials flow
                var token = await _warehouseAuthService.GetToken(databricksConfig.Entities[0]);

                string? databricksInstanceUrl = _config["INSTANCE_URL"];
                var warehouseId = _config["DATABRICKS_WAREHOUSE_ID"];

                if (string.IsNullOrWhiteSpace(databricksInstanceUrl))
                {
                    _logger.LogError("Databricks INSTANCE_URL is missing in configuration");
                    return StatusCode(500, new { error = "Databricks INSTANCE_URL is not configured" });
                }

                if (string.IsNullOrWhiteSpace(warehouseId))
                {
                    _logger.LogError("Databricks WAREHOUSE_ID is missing in configuration");
                    return StatusCode(500, new { error = "Databricks warehouse ID is not configured" });
                }

                // call databricks statement api to run query against view
                var url = $"{databricksInstanceUrl}/api/2.0/sql/statements";

                var payload = new
                {
                    statement = request.Query,
                    warehouse_id = warehouseId,
                    wait_timeout = "30s"
                };

                var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                httpRequest.Content = new StringContent(
                    JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                // Execute databricks statement api
                var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Query failed: {Content}", content);
                    return StatusCode((int)response.StatusCode, content);
                }

                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                // Check Databricks query status
                if (root.TryGetProperty("status", out var statusElement))
                {
                    var state = statusElement.GetProperty("state").GetString();

                    if (state == "FAILED")
                    {
                        var errorMessage = statusElement
                            .GetProperty("error")
                            .GetProperty("message")
                            .GetString();

                        _logger.LogError("Databricks query failed: {Error}", errorMessage);

                        return BadRequest(new
                        {
                            error = errorMessage,
                            type = "SQL_ERROR"
                        });
                    }
                }

                var normalized = await _warehouserConfigService.Normalize(doc.RootElement);
                return Ok(normalized);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running query for tenant {TenantId}", tenantId);
                return StatusCode(500, new { message = ex.Message });
            }
        }
      
        [HttpGet("{tenantId}/metadata/views")]
        public async Task<IActionResult> GetViewsAsync(string tenantId)
        {
            // fetch databricks config for tenant
            var databricksConfig = await _warehouserConfigService.GetWarehouseConfig(tenantId);
            if (databricksConfig == null || databricksConfig.Entities is not { Count: > 0 })
            {
                return BadRequest(new { error = $"Databricks Config not found for tenant {tenantId}" });
            }

            var schema = databricksConfig.Entities[0].SchemaFullName ?? "";
         
            // get token using client credentials flow
            var token = await _warehouseAuthService.GetToken(databricksConfig.Entities[0]);

            string? databricksInstanceUrl = _config["INSTANCE_URL"];
            var warehouseId = _config["DATABRICKS_WAREHOUSE_ID"];

            if (string.IsNullOrWhiteSpace(databricksInstanceUrl))
            {
                _logger.LogError("Databricks INSTANCE_URL is missing in configuration");
                return StatusCode(500, new { error = "Databricks INSTANCE_URL is not configured" });
            }

            if (string.IsNullOrWhiteSpace(warehouseId))
            {
                _logger.LogError("Databricks WAREHOUSE_ID is missing in configuration");
                return StatusCode(500, new { error = "Databricks warehouse ID is not configured" });
            }

            // call databricks statement api to run query against view
            var url = $"{databricksInstanceUrl}/api/2.0/sql/statements";
            var parts = schema.Split('.');
            var catalogName = parts[0];
            var schemaName = parts[1];

            var payload = new
            {
                statement = $"SELECT table_name FROM {catalogName}.information_schema.tables WHERE table_schema = '{tenantId}'",
                warehouse_id = warehouseId,
                wait_timeout = "30s"
            };


            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);

            var views = new List<string>();

            var data = doc.RootElement
                .GetProperty("result")
                .GetProperty("data_array");

            foreach (var row in data.EnumerateArray())
            {
                views.Add(row[0].GetString());
            }

            return Ok( new { data = views});
        }

        [HttpGet("{tenantId}/metadata/{viewName}/columns")]
        public async Task<IActionResult> GetColumnsAsync(string tenantId, string viewName)
        {
            var databricksConfig = await _warehouserConfigService.GetWarehouseConfig(tenantId);
            if (databricksConfig == null || databricksConfig.Entities is not { Count: > 0 })
            {
                return BadRequest(new { error = $"Databricks Config not found for tenant {tenantId}" });
            }

            var entity = databricksConfig.Entities[0];
            var schema = entity.SchemaFullName;

            // Basic validation (important)
            if (!Regex.IsMatch(viewName, "^[a-zA-Z0-9_]+$"))
                return BadRequest("Invalid view name");

            var token = await _warehouseAuthService.GetToken(entity);

            var url = $"{_config["INSTANCE_URL"]}/api/2.0/sql/statements";

            var parts = schema.Split('.');
            var catalogName = parts[0];
            var schemaName = parts[1];

            var query = $@"
                SELECT column_name
                FROM {catalogName}.information_schema.columns
                WHERE table_schema = '{tenantId}'
                  AND table_name = '{viewName}'
                ORDER BY ordinal_position
            ";

            var payload = new
            {
                statement = query,
                warehouse_id = _config["DATABRICKS_WAREHOUSE_ID"]?.ToLower(), // ensure lowercase
                wait_timeout = "30s"
            };

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, json);

            using var doc = JsonDocument.Parse(json);

            var columns = new List<string>();

            if (doc.RootElement.TryGetProperty("result", out var result) &&
                result.TryGetProperty("data_array", out var data))
            {
                foreach (var row in data.EnumerateArray())
                {
                    columns.Add(row[0].GetString());
                }
            }

            return Ok(new
            {
                data = columns
            });
        }
    }
}
