using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.Notifications;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Configuration
{
    public static class ConfigureNotifications
    {
        public static IServiceCollection AddNotificationsServices(this IServiceCollection services, IConfiguration config)
        {
            if (!config.GetValue<bool?>("DisableDataLake") ?? true)
            {
                services.AddScoped<INotificationService, NotificationService>();

            }

            return services;
        }
    }
}
