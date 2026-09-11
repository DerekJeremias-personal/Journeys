using Azure.Messaging.EventGrid.SystemEvents;
using Azure.Messaging.EventGrid;
using Journeys.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Journeys.API.Controllers
{
    [AllowAnonymous]
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class BlobCreatedController(IIngestService ingestService, ILogger<BlobCreatedController> logger) : Controller
    {
        [HttpPost]
        public async Task<IActionResult> Event()
        {
            try
            {
                var binaryBody = await BinaryData.FromStreamAsync(Request.Body);
                if (binaryBody == null)
                {
                    logger.LogWarning("BlobCreated event: No event data provided");
                    return BadRequest("No event data provided");
                }

                var eventGridEvents = EventGridEvent.ParseMany(binaryBody);
                if (eventGridEvents == null)
                {
                    logger.LogWarning("BlobCreated event: No events resolved from request");
                    return BadRequest("No events resolved from request");
                }

                foreach (var eventGridEvent in eventGridEvents)
                {
                    if (!eventGridEvent.TryGetSystemEventData(out var eventData))
                    {
                        logger.LogInformation("BlobCreated event: Failed to get system event - {Event}", eventGridEvent.ToString());
                        continue;
                    }

                    switch (eventData)
                    {
                        case SubscriptionValidationEventData subscriptionValidationEventData:
                            {
                                var responseData = new
                                {
                                    ValidationResponse = subscriptionValidationEventData.ValidationCode
                                };
                                logger.LogInformation("BlobCreated event: Subscription validation received");
                                return Ok(responseData);
                            }
                        case StorageBlobCreatedEventData storageBlobCreatedEventData:
                            {
                                var blobUrl = storageBlobCreatedEventData.Url;
                                logger.LogInformation("BlobCreated event received: {Url}", blobUrl);

                                try
                                {
                                    var blobUri = new Uri(blobUrl);
                                    var jobsCreated = await ingestService.CreateBatchJobsFromBlobUriAsync(blobUri);

                                    if (jobsCreated > 0)
                                    {
                                        logger.LogInformation("BlobCreated event: Successfully created {JobCount} batch job(s) for blob {Url}",
                                            jobsCreated, blobUrl);
                                    }
                                    else
                                    {
                                        logger.LogWarning("BlobCreated event: No batch jobs created for blob {Url} (may not have dropbox config)", blobUrl);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    logger.LogError(ex, "BlobCreated event: Error processing blob {Url}", blobUrl);
                                    // Don't throw - log and continue to next event
                                    // Azure EventGrid will retry if we return an error status
                                }

                                break;
                            }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "BlobCreated event: Error processing EventGrid request");
                // Return 500 to trigger EventGrid retry for transient errors
                return StatusCode(500, "Error processing event");
            }

            return Ok();
        }
    }
}
