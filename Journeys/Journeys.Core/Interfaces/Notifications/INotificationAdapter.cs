using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.Notifications
{
    public interface INotificationAdapter
    {
        string AdapterType { get; }
        Type ConfigType { get; }
        Task<bool> SendAsync(object config, object payload);
    }
}
