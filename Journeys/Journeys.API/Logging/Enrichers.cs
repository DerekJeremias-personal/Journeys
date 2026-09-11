using System.Diagnostics;
using Journeys.Core.Services;
using Microsoft.Extensions.Primitives;
using Serilog.Core;
using Serilog.Events;

public class Enrichers : ILogEventEnricher
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private static readonly HttpClient _httpClient = new HttpClient();
    private readonly AzureServerIdService _azureServerIdService;


    private static readonly string[] SensitiveHeaders =
    {
        "Authorization",
        "Cookie",
        "Set-Cookie",
        "X-Api-Key",
        "X-Amz-Security-Token",
        "Journeys-API-KEY"
    };

    public Enrichers(IHttpContextAccessor httpContextAccessor, AzureServerIdService azureServerIdService)
    {
        _httpContextAccessor = httpContextAccessor;
        _azureServerIdService = azureServerIdService;
    }


    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        string requestId;
        try
        {
            var requestHeader = _httpContextAccessor.HttpContext?.Request?.Headers["X-Request-ID"].FirstOrDefault();
            if (requestHeader == null)
            {
                requestId = Guid.NewGuid().ToString();
                if (_httpContextAccessor?.HttpContext?.Request?.Headers != null)
                {
                    _httpContextAccessor.HttpContext.Request.Headers.Add("X-Request-ID", requestId);
                }
            }
            else
            {
                requestId = requestHeader;
            }
        }
        catch
        {
            requestId = "Undefined-DefaultError";
        }

        if (logEvent.Level == LogEventLevel.Error || logEvent.Level == LogEventLevel.Fatal || _httpContextAccessor.HttpContext?.Response?.StatusCode == null) // applies different enricher sets for logs / trace logs
        {
            var context = _httpContextAccessor.HttpContext;
            var timestamp = DateTime.UtcNow.ToString("o");
            var exception = logEvent.Exception;
            var exceptionType = exception?.GetType().Name ?? "";
            var statusCode = logEvent.Properties.ContainsKey("StatusCode") ? logEvent.Properties["StatusCode"].ToString() : context?.Response?.StatusCode.ToString() ?? "";
            var errorMessage = exception?.Message ?? "";
            var stackTrace = exception?.StackTrace ?? "";
            var rawException = exception?.ToString() ?? "";
            var level = logEvent.Level.ToString();
            var codebase = exception?.TargetSite?.DeclaringType?.FullName ?? "";

                //string requestId = _httpContextAccessor.HttpContext?.Request?.Headers["X-Request-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString();
            var logSource = logEvent.Properties.ContainsKey("SourceContext") ? logEvent.Properties["SourceContext"].ToString() : "";

            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Codebase", codebase));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("RequestId", requestId));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Level", level));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("StatusCode", statusCode));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ExceptionType", exceptionType));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("StackTrace", stackTrace));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Message", errorMessage));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("LogSource", logSource));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("RawException", rawException));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TimeOfOccurrence", timestamp));
        }
        else
        {
            var stackTrace = new StackTrace();
            var stackFrame = stackTrace.FrameCount > 2 ? stackTrace.GetFrame(2) : null;
            var method = stackFrame?.GetMethod();

            var codeBase = logEvent.Properties.ContainsKey("ConsumerType") ? logEvent.Properties["ConsumerType"].ToString() 
                : logEvent.Properties.ContainsKey("Uri") ? logEvent.Properties["Uri"].ToString() 
                : logEvent.Properties.ContainsKey("Path") ? logEvent.Properties["Path"].ToString() : "";

            var timestamp = DateTime.UtcNow.ToString("o");

            var path = _httpContextAccessor.HttpContext?.Request?.Path.ToString() ?? "";

            var user = _httpContextAccessor.HttpContext?.User?.Identity;
            var httpContext = _httpContextAccessor.HttpContext;
            var identityName = httpContext?.User?.Identity?.Name;
            var apiKey = httpContext?.Request?.Headers["X-API-Key"].FirstOrDefault();

            string authenticatedUser = !string.IsNullOrEmpty(identityName)
                ? identityName
                : !string.IsNullOrEmpty(apiKey)
                    ? $"S2S - {apiKey}"
                    : "";

            var ipAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "";

            //string azureServerId = _azureServerIdService.GetServerId();
            var statusCode = _httpContextAccessor.HttpContext?.Response?.StatusCode.ToString() ?? "";

            var request = logEvent.Properties.ContainsKey("Request") ? logEvent.Properties["Request"].ToString() : "";
            var response = logEvent.Properties.ContainsKey("Response") ? logEvent.Properties["Response"].ToString() : "";

            var callerMetaData = new Dictionary<string, object>
            {
                { "ip", ipAddress },
                { "culture", _httpContextAccessor.HttpContext?.Request?.Headers["Accept-Language"].ToString() ?? "" },
                { "deviceInfo", _httpContextAccessor.HttpContext?.Request?.Headers["User-Agent"].ToString() ?? "" }
            };

            // Sanitize headers before logging
            var headers = _httpContextAccessor.HttpContext?.Request?.Headers;
            var sanitizedHeaders = new Dictionary<string, string> { };

            if (headers != null)
            {
                sanitizedHeaders = headers?
                .Where(h => !SensitiveHeaders.Contains(h.Key)) // Remove sensitive headers
                .ToDictionary(h => h.Key, h => StringValues.IsNullOrEmpty(h.Value) ? "" : h.Value.ToString());
            }
            ;

            string sanitizedHeadersString = sanitizedHeaders.Any()
            ? string.Join(", ", sanitizedHeaders.Select(kvp => $"{kvp.Key}: {kvp.Value}")) : "";


            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Codebase", codeBase));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("RequestId", requestId));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("StatusCode", statusCode));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Request", request));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Response", response));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Tenant", "Hayward"));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Path", path));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Headers", sanitizedHeadersString));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("AuthenticatedUser", authenticatedUser));
            //logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ServerId", azureServerId));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CallerMetaData", callerMetaData));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TimeOfOccurrence", timestamp));
        }

    }
}