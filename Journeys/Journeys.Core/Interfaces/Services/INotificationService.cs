using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Journeys.Core.Models;
using Journeys.DTO.Models;

namespace Journeys.Core.Interfaces.Services
{
    public interface INotificationService
    {
        Task<NotificationConfig> UpsertNotificationConfigAsync(string tenantId, NotificationConfigDto config);
        Task<NotificationConfig?> GetNotificationConfigAsync(string tenantId, string configId);
        Task<IEnumerable<NotificationConfig>> GetNotificationConfigsAsync(string tenantId, string status);
        Task<bool> DeleteNotificationConfigAsync(string tenantId, string configId);

        Task<bool> SendNotificationsAsync(string tenantId, ProcessedEventDto processedEventDto);
        Task<bool> SendNotificationsAsync(string tenantId, PointLedgerDto ledgerDto);
        Task<bool> SendNotificationAsync(string tenantId, string configId, object payload);
        Task<bool> SendNotificationAsync(string tenantId, NotificationConfig config, object payload);
    }
}
