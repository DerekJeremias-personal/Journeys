using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using MassTransit;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Journeys.Core.Services.Ingest;

public class ProcessChunkJobConsumer : IJobConsumer<ProcessChunkJobMessage>
{
    private readonly IIngestService _ingestService;
    private readonly ILogger<ProcessChunkJobConsumer> _logger;
    private const int MaxRetryAttempts = 3;
    private const int BaseDelayMs = 1000;

    public ProcessChunkJobConsumer(IIngestService ingestService, ILogger<ProcessChunkJobConsumer> logger)
    {
        _ingestService = ingestService;
        _logger = logger;
    }

    public async Task Run(JobContext<ProcessChunkJobMessage> context)
    {
        var message = context.Job;
        var attempt = 0;

        _logger.LogInformation("Processing chunk job {BatchJobId} from batch file {BatchFileId} for tenant {TenantId}",
            message.BatchJobId, message.BatchFileId, message.TenantId);

        //while (attempt < MaxRetryAttempts)
        //{
        //    try
        //    {
        //        attempt++;
        //        _logger.LogDebug("Attempt {Attempt} for chunk job {BatchJobId}", attempt, message.BatchJobId);

        //        // Get the chunk job from database and process it - using BatchFileId for efficient claiming
        //        await _ingestService.RunJobAsync(message.TenantId, message.BatchFileId, message.BatchJobId);

        //        _logger.LogInformation("Successfully processed chunk job {BatchJobId} on attempt {Attempt}",
        //            message.BatchJobId, attempt);
        //        return; // Success - exit retry loop
        //    }
        //    catch (Exception ex) when (IsRetryableError(ex) && attempt < MaxRetryAttempts)
        //    {
        //        var delay = CalculateRetryDelay(attempt);
        //        _logger.LogWarning(ex, "Retryable error on attempt {Attempt} for chunk job {BatchJobId}. Retrying in {Delay}ms",
        //            attempt, message.BatchJobId, delay);

        //        await Task.Delay(delay);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Failed to process chunk job {BatchJobId} for tenant {TenantId} after {Attempt} attempts",
        //            message.BatchJobId, message.TenantId, attempt);
        //        throw; // Let MassTransit handle final retry logic
        //    }
        //}
    }

    private static bool IsRetryableError(Exception ex)
    {
        // Check for HTTP 412 "Condition Not Met" errors
        if (ex.Message.Contains("ConditionNotMet") ||
            ex.Message.Contains("The condition specified using HTTP conditional header(s) is not met"))
        {
            return true;
        }

        // Check for other transient errors
        if (ex is HttpRequestException httpEx)
        {
            return httpEx.Message.Contains("timeout") ||
                   httpEx.Message.Contains("connection") ||
                   httpEx.Message.Contains("temporary");
        }

        // Check for Azure Storage specific errors
        if (ex.Message.Contains("RequestTimeout") ||
            ex.Message.Contains("ServiceUnavailable") ||
            ex.Message.Contains("InternalServerError"))
        {
            return true;
        }

        return false;
    }

    private static int CalculateRetryDelay(int attempt)
    {
        // Exponential backoff: 1s, 2s, 4s
        return BaseDelayMs * (int)Math.Pow(2, attempt - 1);
    }
}
