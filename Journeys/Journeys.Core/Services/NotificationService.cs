using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.Notifications;
using Journeys.Core.Models;
using Journeys.DTO.Models;
using Microsoft.Extensions.Logging;

namespace Journeys.Core.Services
{
    public class NotificationService : INotificationService
    {
        private readonly INotificationConfigAdapter _configAdapter;
        private readonly INotificationAdapterFactory _adapterFactory;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            INotificationConfigAdapter configAdapter,
            INotificationAdapterFactory adapterFactory,
            ILogger<NotificationService> logger)
        {
            _configAdapter = configAdapter;
            _adapterFactory = adapterFactory;
            _logger = logger;
        }

        public async Task<NotificationConfig> UpsertNotificationConfigAsync(string tenantId, NotificationConfigDto config)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentNullException(nameof(tenantId), "The TenantId must be provided.");

            try
            {
                var notificationConfig = new NotificationConfig(
                    tenantId,
                    config.Id,
                    config.Name,
                    config.AdapterType,
                    config.Status,
                    config.ConfigDetails,
                    config.EventModelId);

                return await _configAdapter.UpsertNotificationConfigAsync(notificationConfig);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting notification config for tenant {TenantId}", tenantId);
                throw;
            }
        }

        public async Task<NotificationConfig?> GetNotificationConfigAsync(string tenantId, string configId)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentNullException(nameof(tenantId), "The TenantId must be provided.");

            if (string.IsNullOrWhiteSpace(configId))
                throw new ArgumentNullException(nameof(tenantId), "The ConfigId must be provided.");

            try
            {
                return await _configAdapter.FetchNotificationConfigAsync(tenantId, ConfigStatus.ACTIVE, configId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching notification config {ConfigId} for tenant {TenantId}", configId, tenantId);
                throw;
            }
        }

        public async Task<IEnumerable<NotificationConfig>> GetNotificationConfigsAsync(string tenantId, string status = null)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentNullException(nameof(tenantId), "The TenantId must be provided.");

            try
            {
                var configs = await _configAdapter.FetchAllNotificationConfigByStatusAsync(tenantId, status ?? ConfigStatus.ACTIVE);
                return configs?.Entities != null ? configs.Entities : Array.Empty<NotificationConfig>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching notification configs for tenant {TenantId} with status {Status}", tenantId, status);
                throw;
            }
        }

        public async Task<bool> DeleteNotificationConfigAsync(string tenantId, string configId)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentNullException(nameof(tenantId), "The TenantId must be provided.");

            if (string.IsNullOrWhiteSpace(configId))
                throw new ArgumentNullException(nameof(tenantId), "The ConfigId must be provided.");

            try
            {
                return await _configAdapter.DeleteNotificationConfigAsync(tenantId, configId, ConfigStatus.DELETED);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting notification config {ConfigId} for tenant {TenantId}", configId, tenantId);
                throw;
            }
        }

        public async Task<bool> SendNotificationsAsync(string tenantId, ProcessedEventDto processedEventDto)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentNullException(nameof(tenantId), "The TenantId must be provided.");

            if (processedEventDto == null)
                throw new ArgumentNullException(nameof(processedEventDto));

            try
            {
                var bOk = false;
                var allNotifs = await _configAdapter.FetchAllNotificationConfigByStatusAsync(tenantId, ConfigStatus.ACTIVE);
                var thisNotifs = allNotifs?.Entities?
                    .Where(x => string.IsNullOrEmpty(x.EventModelId) || 
                               x.EventModelId.Equals(processedEventDto.ProcessedEvent.ProcessedEventModelId, StringComparison.OrdinalIgnoreCase))?
                    .ToList() ?? new List<NotificationConfig>();

                foreach (var cfg in thisNotifs)
                {
                    bOk = await SendNotificationAsync(tenantId, cfg, processedEventDto.ProcessedEvent);
                }

                return bOk;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending notification for tenant {tenantId} with event: {processedEventDto.ProcessedEvent.EventNaturalKey}");
                //throw;
            }
            return false;
        }

        public async Task<bool> SendNotificationsAsync(string tenantId, PointLedgerDto ledgerDto)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentNullException(nameof(tenantId), "The TenantId must be provided.");

            if (ledgerDto == null)
                throw new ArgumentNullException(nameof(ledgerDto));

            try
            {
                var allNotifs = await _configAdapter.FetchAllNotificationConfigByStatusAsync(tenantId, ConfigStatus.ACTIVE);
                var thisNotifs = allNotifs?.Entities?
                    .Where(x => string.IsNullOrEmpty(x.EventModelId) ||
                               x.EventModelId.Equals(ledgerDto.ModelId, StringComparison.OrdinalIgnoreCase))?
                    .ToList() ?? new List<NotificationConfig>();

                foreach (var cfg in thisNotifs)
                {
                    var bOk = await SendNotificationAsync(tenantId, cfg, ledgerDto);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending notification for tenant {tenantId} with event: {ledgerDto.AccountId}");
                //throw;
            }
            return false;
        }

        public async Task<bool> SendNotificationAsync(string tenantId, string configId, object payload)
        {
            var config = await _configAdapter.FetchNotificationConfigAsync(tenantId, ConfigStatus.ACTIVE, configId);
            if (config == null)
            {
                _logger.LogError($"Notification config {configId} not found for tenant {tenantId}");
                return false;
            }

            return await SendNotificationAsync(tenantId, config, payload);
        }

        public async Task<bool> SendNotificationAsync(string tenantId, NotificationConfig config, object payload)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentNullException(nameof(tenantId), "The TenantId must be provided.");

            if (config == null)
                throw new ArgumentNullException(nameof(tenantId), "The Config must be provided.");

            try
            {
                if (!_adapterFactory.IsAdapterTypeSupported(config.AdapterType))
                {
                    _logger.LogError($"Unsupported adapter type {config.AdapterType} for config {config.Id}");
                    return false;
                }

                var adapter = _adapterFactory.CreateAdapter(config.AdapterType);

                var serializerOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                // Deserialize the config details directly from JsonElement to the appropriate type
                var configDetails = JsonSerializer.Deserialize(config.ConfigDetails, adapter.ConfigType, serializerOptions);
                if (configDetails == null)
                {
                    _logger.LogError($"Failed to deserialize config details for config {config.Id}");
                    return false;
                }

                // Validate the configuration
                if (configDetails is INotificationAdapterConfig adapterConfig && !adapterConfig.Validate())
                {
                    _logger.LogError($"Invalid configuration for config {config.Id}");
                    return false;
                }

                return await adapter.SendAsync(configDetails, payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending notification using config {config.Id} for tenant {tenantId}");
                return false;
            }
        }
    }
}
