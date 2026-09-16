using Journeys.DTO.Exceptions;
using Journeys.DTO.Models.RulesEngine;
using Journeys.Infra.Backend;
using System.Net;
using System.Text.Json;

namespace Journeys.API.Middleware;

/// <summary>
/// Global exception handling middleware that maintains backwards compatibility
/// while properly handling backend validation and system errors
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GlobalExceptionMiddleware caught exception: {ExceptionType} - {Message}", ex.GetType().Name, ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Check if response has already started - if so, we can't modify it
        if (context.Response.HasStarted)
        {
            _logger.LogWarning("Cannot handle exception - response has already started");
            return;
        }

        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

        try
        {
            switch (exception)
            {
                case APIErrorsException apiEx:
                    await HandleAPIErrorsException(context, apiEx, path);
                    break;
                    
                case BackendValidationException validationEx:
                    await HandleBackendValidationException(context, validationEx, path);
                    break;
                    
                case BackendSystemException systemEx:
                    await HandleBackendSystemException(context, systemEx, path);
                    break;
                    
                case BackendEntityNotFoundException notFoundEx:
                    await HandleBackendEntityNotFoundException(context, notFoundEx, path);
                    break;
                    
                default:
                    // Let existing controller handling work for non-backend exceptions
                    // Re-throw the original exception to maintain stack trace
                    throw exception;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in exception handling middleware");
            // If we can't handle the exception properly, re-throw the original
            throw exception;
        }
    }

    /// <summary>
    /// Handle APIErrorsException - maintains existing controller behavior
    /// </summary>
    private async Task HandleAPIErrorsException(HttpContext context, APIErrorsException ex, string path)
    {
        if (context.Response.HasStarted) return;

        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        context.Response.ContentType = "application/json";

        object response;

        // EventsController specific format for /process endpoint
        if (path.Contains("/api/events/") && path.EndsWith("/process"))
        {
            var tenantId = ExtractTenantIdFromPath(path, 2); // /api/events/{tenantId}/{modelName}/process
            var originalPayload = await GetOriginalPayloadFromRequest(context);
            
            response = new EventPayloadResponseDto
            {
                TenantId = tenantId,
                Event = originalPayload,
                Errors = ex.Errors ?? new Dictionary<string, string>()
                // All other properties will be null/default - matching existing behavior
            };
        }
        else
        {
            // For all other endpoints (including DropboxConfig), return Dictionary<string, string>
            response = ex.Errors ?? new Dictionary<string, string>();
        }

        var json = JsonSerializer.Serialize(response);
        if (!string.IsNullOrEmpty(json))
        {
            await context.Response.WriteAsync(json);
        }
        
        _logger.LogWarning(ex, "API validation error occurred for {Path}", path);
    }

    /// <summary>
    /// Handle BackendValidationException - convert to APIErrorsException format
    /// </summary>
    private async Task HandleBackendValidationException(HttpContext context, BackendValidationException ex, string path)
    {
        // Convert BackendValidationException to APIErrorsException format for consistency
        var apiException = new APIErrorsException(ex.ValidationErrors);
        await HandleAPIErrorsException(context, apiException, path);
        
        _logger.LogWarning(ex, "Backend validation error occurred for {Path}", path);
    }

    /// <summary>
    /// Handle BackendSystemException - maintains existing controller behavior
    /// </summary>
    private async Task HandleBackendSystemException(HttpContext context, BackendSystemException ex, string path)
    {
        if (context.Response.HasStarted) return;

        // Map error codes to HTTP status codes
        context.Response.StatusCode = ex.ErrorCode switch
        {
            "unauthorized" => (int)HttpStatusCode.Unauthorized,
            "forbidden" => (int)HttpStatusCode.Forbidden,
            "service_unavailable" => (int)HttpStatusCode.ServiceUnavailable,
            "timeout" => (int)HttpStatusCode.RequestTimeout,
            _ => (int)HttpStatusCode.InternalServerError
        };
        
        context.Response.ContentType = "application/json";

        object response;

        // EventsController specific format for generic exceptions
        if (path.Contains("/api/events/") && path.EndsWith("/process"))
        {
            response = new Dictionary<string, string?>
            {
                { "Exception.Message", ex.Message ?? "Unknown error" },
                { "Exception.InnerException", ex.InnerException?.Message },
                { "Exception.StackTrace", ex.StackTrace }
            };
        }
        // AccountController specific format
        else if (path.Contains("/api/account/"))
        {
            response = $"Internal server error: {ex.Message ?? "Unknown error"}";
        }
        else
        {
            // For other endpoints, return structured error
            response = new { error = ex.Message ?? "Unknown error", code = ex.ErrorCode ?? "unknown" };
        }

        var json = JsonSerializer.Serialize(response);
        if (!string.IsNullOrEmpty(json))
        {
            await context.Response.WriteAsync(json);
        }
        
        _logger.LogError(ex, "Backend system error occurred for {Path}", path);
    }

    /// <summary>
    /// Handle BackendEntityNotFoundException - maintains existing controller behavior
    /// </summary>
    private async Task HandleBackendEntityNotFoundException(HttpContext context, BackendEntityNotFoundException ex, string path)
    {
        if (context.Response.HasStarted) return;

        context.Response.StatusCode = (int)HttpStatusCode.NotFound;

        // AccountController returns standard 404 (empty body)
        if (path.Contains("/api/account/"))
        {
            // Don't write anything - ASP.NET will handle 404 response
            return;
        }
        else
        {
            // For other endpoints, provide structured response
            context.Response.ContentType = "application/json";
            var response = new { 
                message = ex.Message ?? "Entity not found", 
                entityId = ex.EntityId, 
                modelId = ex.ModelId 
            };
            var json = JsonSerializer.Serialize(response);
            if (!string.IsNullOrEmpty(json))
            {
                await context.Response.WriteAsync(json);
            }
        }
        
        _logger.LogWarning(ex, "Backend entity not found for {Path}", path);
    }

    /// <summary>
    /// Extract tenant ID from path segments
    /// </summary>
    private string ExtractTenantIdFromPath(string path, int segmentIndex)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > segmentIndex ? segments[segmentIndex] : "unknown";
    }

    /// <summary>
    /// Get original payload from request - simplified for backwards compatibility
    /// </summary>
    private async Task<JsonElement> GetOriginalPayloadFromRequest(HttpContext context)
    {
        // This is tricky since the request body has already been read
        // For backwards compatibility, return empty object - matches existing behavior
        // In the future, we could enable request buffering to capture the original payload
        return JsonDocument.Parse("{}").RootElement;
    }
}
