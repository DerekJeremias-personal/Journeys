using System.Collections.Generic;
using Journeys.Core.Interfaces.Notifications;
using Journeys.Notification.Interfaces;

namespace Journeys.Notification.Models
{
    public class RestApiConfig : INotificationAdapterConfig
    {
        public string AdapterType => "rest_api";
        
        public string BaseUrl { get; set; }
        public string Endpoint { get; set; }
        public string HttpMethod { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public Dictionary<string, string> QueryParameters { get; set; }
        public AuthConfig Authentication { get; set; }
        public RetryConfig Retry { get; set; } = new RetryConfig();

        public bool Validate()
        {
            if (string.IsNullOrWhiteSpace(BaseUrl))
                return false;

            if (string.IsNullOrWhiteSpace(Endpoint))
                return false;

            if (string.IsNullOrWhiteSpace(HttpMethod))
                return false;

            // Validate HTTP method
            var validMethods = new[] { "GET", "POST", "PUT", "DELETE", "PATCH" };
            if (!validMethods.Contains(HttpMethod.ToUpper()))
                return false;

            // If authentication is provided, validate it
            if (Authentication != null)
            {
                switch (Authentication.Type?.ToLower())
                {
                    case "bearer":
                        if (string.IsNullOrWhiteSpace(Authentication.Token))
                            return false;
                        break;
                    case "apikey":
                        if (string.IsNullOrWhiteSpace(Authentication.ApiKey))
                            return false;
                        break;
                    case "oauth":
                        if (Authentication.OAuth == null ||
                            string.IsNullOrWhiteSpace(Authentication.OAuth.TokenUrl) ||
                            string.IsNullOrWhiteSpace(Authentication.OAuth.ClientId) ||
                            string.IsNullOrWhiteSpace(Authentication.OAuth.ClientSecret))
                            return false;
                        break;
                }
            }

            // Validate retry configuration
            if (Retry != null)
            {
                if (Retry.NumberOfRetries < 0)
                    return false;
                if (Retry.InitialDelayMilliseconds < 0)
                    return false;
                if (Retry.MaxDelayMilliseconds < Retry.InitialDelayMilliseconds)
                    return false;
            }

            return true;
        }
    }

    public class AuthConfig
    {
        public string Type { get; set; } // "Bearer", "ApiKey", "OAuth"
        public string Token { get; set; }
        public string ApiKey { get; set; }
        public string ApiKeyHeader { get; set; }
        public OAuthConfig OAuth { get; set; }
    }

    public class OAuthConfig
    {
        public string TokenUrl { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string Scope { get; set; }
    }

    public class RetryConfig
    {
        public int NumberOfRetries { get; set; } = 3;
        public bool UseExponentialBackoff { get; set; } = true;
        public int InitialDelayMilliseconds { get; set; } = 1000;
        public int MaxDelayMilliseconds { get; set; } = 30000;
    }
} 