using Journeys.API.Models;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using MassTransit;
using System.Net;

namespace Journeys.API.Consumers
{
    public class BlobCreatedJobConsumer : IJobConsumer<BlobCreated>
    {
        private readonly IIngestService _ingestService;
        private readonly ILogger<BlobCreatedJobConsumer> _logger;
        private const int MaxRetryAttempts = 3;
        private const int BaseDelayMs = 1000;

        public BlobCreatedJobConsumer(IIngestService ingestService, ILogger<BlobCreatedJobConsumer> logger)
        {
            _ingestService = ingestService ?? throw new ArgumentNullException(nameof(ingestService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.LogInformation("BlobCreatedJobConsumer constructor called - consumer is being registered");
        }

        public async Task Run(JobContext<BlobCreated> context)
        {
            var blobUrl = context.Job.Url;
            var attempt = 0;

            _logger.LogInformation("BlobCreatedJobConsumer received message: {Url}", blobUrl);

            while (attempt < MaxRetryAttempts)
            {
                try
                {
                    attempt++;
                    _logger.LogDebug("Attempt {Attempt} for blob: {Url}", attempt, blobUrl);

                    var blobUri = new Uri(blobUrl);

                    // Check if blob has already been processed (idempotency)
                    if (await IsBlobAlreadyProcessed(blobUri))
                    {
                        _logger.LogInformation("Blob {Url} has already been processed, skipping", blobUrl);
                        return;
                    }

                    var (_, batchJob) = await _ingestService.ImportBlobFromUriAsync(blobUri);
                    if (batchJob == null)
                    {
                        _logger.LogWarning("No batch job created for blob: {Url}", blobUrl);
                        return;
                    }

                    _logger.LogInformation("Running job for batch: {BatchJobId}", batchJob.Id);

                    await _ingestService.RunJobAsync(batchJob);

                    _logger.LogInformation("Completed job for batch: {BatchJobId} on attempt {Attempt}", batchJob.Id, attempt);
                    return; // Success - exit retry loop
                }
                catch (Exception ex) when (IsRetryableError(ex) && attempt < MaxRetryAttempts)
                {
                    var delay = CalculateRetryDelay(attempt);
                    _logger.LogWarning(ex, "Retryable error on attempt {Attempt} for blob {Url}. Retrying in {Delay}ms",
                        attempt, blobUrl, delay);

                    await Task.Delay(delay);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process blob {Url} after {Attempt} attempts", blobUrl, attempt);
                    throw; // Let MassTransit handle final retry logic
                }
            }
        }

        private async Task<bool> IsBlobAlreadyProcessed(Uri blobUri)
        {
            try
            {
                // This is a placeholder for idempotency check
                // You could implement this by checking if a batch job already exists for this blob
                // or by checking blob metadata for a processing flag
                return false; // For now, always process
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check if blob {Url} was already processed, proceeding with processing", blobUri);
                return false;
            }
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

            //Failed to claim batch job
            if (ex.Message.Contains("Failed to claim batch job"))
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
}