using Journeys.Core.Interfaces.Services;
using Journeys.DTO.Models;
using MassTransit;

namespace Journeys.API.Consumers
{
    public class PointsChangedJobConsumer : IJobConsumer<PointLedgerDto>
    {
        private readonly INotificationService _notificationService;
        private readonly ILogger<PointsChangedJobConsumer> _logger;

        public PointsChangedJobConsumer(INotificationService notificationService, ILogger<PointsChangedJobConsumer> logger)
        {
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Run(JobContext<PointLedgerDto> context)
        {
            try
            {
                _logger.LogInformation($"PointsChangedJobConsumer received message: {context.Job.AccountId}");

                var eventProcessedJobResult = await _notificationService.SendNotificationsAsync(context.Job.TenantId, context.Job);
                if (eventProcessedJobResult)
                    _logger.LogInformation($"PointsChangedJobConsumer Completed notification of: {context.Job.AccountId}");
                else
                    _logger.LogInformation($"PointsChangedJobConsumer Failed to send notification of: {context.Job.AccountId}");
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"Journeys.API.Consumers.PointsChangedJobConsumer.Run Error: {ex.ToString()}");
                _logger.LogError(ex);
                throw;
            }
        }
    }
}
