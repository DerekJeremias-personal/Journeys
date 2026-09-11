using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Journeys.Notification.Models;
using Journeys.Notification.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Journeys.Core.Interfaces.Notifications;

namespace Journeys.Notification.Adapters
{
    public class RestApiAdapter : INotificationAdapter
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<RestApiAdapter> _logger;

        public string AdapterType => "rest_api";
        public Type ConfigType => typeof(RestApiConfig);

        public RestApiAdapter(IHttpClientFactory httpClientFactory, ILogger<RestApiAdapter> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<bool> SendAsync(object config, object payload)
        {
            if (config is not RestApiConfig restConfig)
            {
                _logger.LogError("Invalid config type for RestApiAdapter");
                return false;
            }

            var retryCount = 0;
            var delay = restConfig.Retry.InitialDelayMilliseconds;

            while (true)
            {
                try
                {
                    using var client = _httpClientFactory.CreateClient();
                    var request = CreateHttpRequestMessage(restConfig, payload);
                    
                    var response = await client.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    
                    return true;
                }
                catch (Exception ex) when (ShouldRetry(ex) && retryCount < restConfig.Retry.NumberOfRetries)
                {
                    retryCount++;
                    _logger.LogWarning(ex, 
                        "Attempt {RetryCount} of {MaxRetries} failed for {Url}. Retrying in {Delay}ms", 
                        retryCount, 
                        restConfig.Retry.NumberOfRetries,
                        restConfig.BaseUrl + restConfig.Endpoint,
                        delay);

                    await Task.Delay(delay);

                    if (restConfig.Retry.UseExponentialBackoff)
                    {
                        delay = Math.Min(delay * 2, restConfig.Retry.MaxDelayMilliseconds);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "Failed to send REST API request to {Url} after {RetryCount} attempts", 
                        restConfig.BaseUrl + restConfig.Endpoint,
                        retryCount);
                    return false;
                }
            }
        }

        private HttpRequestMessage CreateHttpRequestMessage(RestApiConfig config, object payload)
        {
            var url = BuildUrl(config);
            var request = new HttpRequestMessage(new HttpMethod(config.HttpMethod), url);

            // Add headers
            if (config.Headers != null)
            {
                foreach (var header in config.Headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }
            }

            // Add authentication
            AddAuthentication(request, config.Authentication);

            // Add body for POST, PUT, PATCH
            if (payload != null && IsMethodWithBody(config.HttpMethod))
            {
                var json = JsonSerializer.Serialize(payload);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            return request;
        }

        private string BuildUrl(RestApiConfig config)
        {
            var url = $"{config.BaseUrl.TrimEnd('/')}/{config.Endpoint.TrimStart('/')}";
            
            if (config.QueryParameters != null && config.QueryParameters.Count > 0)
            {
                var queryString = string.Join("&", config.QueryParameters.Select(kvp => 
                    $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
                url += $"?{queryString}";
            }

            return url;
        }

        private void AddAuthentication(HttpRequestMessage request, AuthConfig auth)
        {
            if (auth == null) return;

            switch (auth.Type?.ToLower())
            {
                case "bearer":
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
                    break;

                case "apikey":
                    request.Headers.Add(auth.ApiKeyHeader ?? "X-API-Key", auth.ApiKey);
                    break;

                case "oauth":
                    // Note: OAuth implementation would typically involve getting a token first
                    // This is a simplified version
                    if (!string.IsNullOrEmpty(auth.Token))
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
                    }
                    break;
            }
        }

        private bool IsMethodWithBody(string method)
        {
            return method.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
                   method.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
                   method.Equals("PATCH", StringComparison.OrdinalIgnoreCase);
        }

        private bool ShouldRetry(Exception ex)
        {
            if (ex is HttpRequestException httpEx)
            {
                // Don't retry on 400-level errors (client errors)
                if (httpEx.StatusCode.HasValue && 
                    httpEx.StatusCode.Value >= System.Net.HttpStatusCode.BadRequest && 
                    httpEx.StatusCode.Value < System.Net.HttpStatusCode.InternalServerError)
                {
                    return false;
                }
                
                // Retry on 500-level errors (server errors)
                return httpEx.StatusCode.HasValue && 
                       httpEx.StatusCode.Value >= System.Net.HttpStatusCode.InternalServerError;
            }

            // Retry on network errors and timeouts
            return ex is TaskCanceledException;
        }
    }
} 