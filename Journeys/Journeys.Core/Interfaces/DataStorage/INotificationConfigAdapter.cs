using Journeys.Core.Models;

namespace Journeys.Core.Interfaces.DataStorage;

public interface INotificationConfigAdapter
{
    Task<NotificationConfig?> FetchNotificationConfigAsync(string tenantId, string status, string notificationConfigId);
    Task<PagedResultSet<NotificationConfig>?> FetchAllNotificationConfigByStatusAsync(string tenantId, string status);
    Task<NotificationConfig> UpsertNotificationConfigAsync(NotificationConfig notificationConfig);
    Task<bool> DeleteNotificationConfigAsync(string tenantId, string notificationConfigId, string status);
}