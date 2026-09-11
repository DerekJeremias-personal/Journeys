using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.Notifications
{
    public interface INotificationAdapterConfig
    {
        string AdapterType { get; }
        bool Validate();
    }
}
