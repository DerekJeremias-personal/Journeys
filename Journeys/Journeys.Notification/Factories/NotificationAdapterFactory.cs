using System;
using System.Collections.Generic;
using Journeys.Core.Interfaces.Notifications;
using Journeys.Notification.Adapters;
using Journeys.Notification.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Journeys.Notification.Factories;

public class NotificationAdapterFactory : INotificationAdapterFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationAdapterFactory> _logger;
    private readonly Dictionary<string, Type> _adapterTypes;

    public NotificationAdapterFactory(
        IServiceProvider serviceProvider,
        ILogger<NotificationAdapterFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _adapterTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            { "rest_api", typeof(RestApiAdapter) }
            // Add other adapter types here as they are implemented
        };
    }

    public INotificationAdapter CreateAdapter(string adapterType)
    {
        if (!IsAdapterTypeSupported(adapterType))
        {
            _logger.LogError("Unsupported adapter type: {AdapterType}", adapterType);
            throw new ArgumentException($"Unsupported adapter type: {adapterType}", nameof(adapterType));
        }

        try
        {
            return (INotificationAdapter)ActivatorUtilities.CreateInstance(
                _serviceProvider, 
                _adapterTypes[adapterType]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create adapter of type {AdapterType}", adapterType);
            throw;
        }
    }

    public bool IsAdapterTypeSupported(string adapterType)
    {
        return _adapterTypes.ContainsKey(adapterType);
    }
} 