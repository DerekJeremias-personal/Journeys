using Journeys.API.Models;
using Journeys.Core.Interfaces.Services;
using Journeys.DTO.Models;
using MassTransit;

namespace Journeys.API.Consumers
{
    public class EventProcessedJobConsumer : IJobConsumer<ProcessedEventDto>
    {
        private readonly INotificationService _notificationService;
        private readonly ILogger<EventProcessedJobConsumer> _logger;

        public EventProcessedJobConsumer(INotificationService notificationService, ILogger<EventProcessedJobConsumer> logger)
        {
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Run(JobContext<ProcessedEventDto> context)
        {
            try
            {
                _logger.LogInformation($"EventProcessedJobConsumer received message: {context.Job.ProcessedEvent.EventNaturalKey}");

                var eventProcessedJobResult = await _notificationService.SendNotificationsAsync(context.Job.TenantId, context.Job);
                if (eventProcessedJobResult)
                    _logger.LogInformation($"EventProcessedJobConsumer Completed notification of: {context.Job.ProcessedEvent.EventNaturalKey}");
                else
                    _logger.LogInformation($"EventProcessedJobConsumer Failed to send notification of: {context.Job.ProcessedEvent.EventNaturalKey}");
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"Journeys.API.Consumers.EventProcessedJobConsumer.Run Error: {ex.ToString()}");
                _logger.LogError(ex);
                throw;
            }
        }
    }
}
