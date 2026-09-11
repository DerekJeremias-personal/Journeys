using Journeys.Core.Interfaces.Services;
using Journeys.DTO.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Journeys.Core.Services
{
    public class WarehouseAuthService : IWarehouseAuthService
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;

        public WarehouseAuthService(IConfiguration config, IHttpClientFactory factory)
        {
            _config = config;
            _httpClient = factory.CreateClient();
        }

        public async Task<string> GetToken(WarehouseConfigDto config)
        {
            var basicAuth = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{config.KeyVaultSecretAppId}:{config.KeyVaultSecretPassword}")
            );

            return await RequestToken(basicAuth);
        }

        //Fetches token info for a specific dashboard and viewer
        public async Task<JsonElement> GetDashboardTokenInfoAsync(string dashboardId, string viewerId, string externalValue, string oidcToken)
        {
            string tokenInfoUrl = $"{_config["INSTANCE_URL"]}/api/2.0/lakeview/dashboards/{dashboardId}/published/tokeninfo" +
                                  $"?external_viewer_id={viewerId}&external_value={externalValue}";

            var request = new HttpRequestMessage(HttpMethod.Get, tokenInfoUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", oidcToken);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<JsonElement>(jsonString);
        }

        public async Task<string> RequestDashboardTokenAsync(Dictionary<string, string> formData, string basicAuthHeader)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_config["INSTANCE_URL"]}/oidc/v1/token");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuthHeader);
            request.Content = new FormUrlEncodedContent(formData);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync();
            var json = JsonSerializer.Deserialize<JsonElement>(jsonString);
            return json.GetProperty("access_token").GetString();
        }

        private async Task<string> RequestToken(string basicAuthHeader)
        {
            var instanceUrl = _config["INSTANCE_URL"];

            if (string.IsNullOrWhiteSpace(instanceUrl))
                throw new Exception("INSTANCE_URL is not configured");

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{instanceUrl}/oidc/v1/token"
            );

            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuthHeader);

            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" },
                { "scope", "all-apis" }
            });

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Token request failed: {content}");

            using var doc = JsonDocument.Parse(content);

            if (!doc.RootElement.TryGetProperty("access_token", out var tokenElement))
                throw new Exception("access_token not found");

            return tokenElement.GetString()!;
        }


    }
}
