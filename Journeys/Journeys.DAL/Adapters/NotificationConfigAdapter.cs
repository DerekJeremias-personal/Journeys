using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;

namespace Journeys.DAL.Adapters;

public class NotificationConfigAdapter(IDynamicDataAdapter dataAdapter) : INotificationConfigAdapter
{
    private const string MODEL_ID = "ab8e92ab-c921-4ff0-b854-c9f81c9f8ae1";

    public async Task<NotificationConfig?> FetchNotificationConfigAsync(string tenantId, string status, string notificationConfigId)
    {
        return await dataAdapter.GetEntityAsync<NotificationConfig>(tenantId, notificationConfigId, MODEL_ID, status);
    }

    public async Task<PagedResultSet<NotificationConfig>?> FetchAllNotificationConfigByStatusAsync(string tenantId, string status)
    {
        return await dataAdapter.GetEntitiesByPKAsync<NotificationConfig>(tenantId, status, MODEL_ID, 100);
    }

    public async Task<NotificationConfig> UpsertNotificationConfigAsync(NotificationConfig notificationConfig)
    {
        notificationConfig.ModelId = MODEL_ID;
        notificationConfig.Id ??= Guid.NewGuid().ToString();

        return await dataAdapter.SetEntityAsync(notificationConfig.TenantId, notificationConfig, MODEL_ID, typeof(NotificationConfig));
    }

    public async Task<bool> DeleteNotificationConfigAsync(string tenantId, string notificationConfigId, string status)
    {
        var pkDictionary = new Dictionary<string, string> { { "tenantId", tenantId }, { "status", status } };
        return await dataAdapter.RemoveEntityAsync(tenantId, notificationConfigId, MODEL_ID, pkDictionary);
    }
}