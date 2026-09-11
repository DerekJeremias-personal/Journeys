using System.Diagnostics;

public class RequestLogging
{
    private const string CampaignAgentSseStreamPathSegment = "/campaign-agent/messages/stream";

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLogging> _logger;

    public RequestLogging(RequestDelegate next, ILogger<RequestLogging> logger)
    {
        _next = next;
        _logger = logger;
    }

    private static bool IsCampaignAgentSseStreamPath(PathString path) =>
        path.Value?.Contains(CampaignAgentSseStreamPathSegment, StringComparison.OrdinalIgnoreCase) == true;

    public async Task Invoke(HttpContext context)
    {
        if (IsCampaignAgentSseStreamPath(context.Request.Path))
        {
            var started = Stopwatch.StartNew();
            try
            {
                await _next(context);
            }
            finally
            {
                started.Stop();
                _logger.LogInformation(
                    "SSE stream passthrough: {Method} {Path} {StatusCode} {DurationMs}ms",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    started.ElapsedMilliseconds);
            }

            return;
        }

        context.Request.EnableBuffering();

        string requestBody;
        using (var reader = new StreamReader(context.Request.Body, leaveOpen: true))
        {
            requestBody = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }

        var originalBodyStream = context.Response.Body;
        using var responseBodyStream = new MemoryStream();
        context.Response.Body = responseBodyStream;

        try
        {
            await _next(context);

            responseBodyStream.Seek(0, SeekOrigin.Begin);
            string responseBody = await new StreamReader(responseBodyStream).ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(responseBody))
            {
                responseBody = $"{context.Response.StatusCode}";
            }

            responseBodyStream.Seek(0, SeekOrigin.Begin);
            await responseBodyStream.CopyToAsync(originalBodyStream);
            context.Response.Body = originalBodyStream;

            _logger.LogInformation("Request-Response Log, Status Code: {@StatusCode} Request: {@Request} Response: {@Response}", context.Response.StatusCode, requestBody, responseBody);
        }
        catch (Exception ex)
        {
            responseBodyStream.Seek(0, SeekOrigin.Begin);
            string responseBody = await new StreamReader(responseBodyStream).ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(responseBody))
            {
                responseBody = $"{context.Response} {context.Response.StatusCode}";
            }

            responseBodyStream.Seek(0, SeekOrigin.Begin);
            await responseBodyStream.CopyToAsync(originalBodyStream);
            context.Response.Body = originalBodyStream;
            _logger.LogError(ex, context.Response.ToString());
            _logger.LogInformation("Request-Response Log {@Request} {@Response}", requestBody, responseBody);
        }
    }
}

