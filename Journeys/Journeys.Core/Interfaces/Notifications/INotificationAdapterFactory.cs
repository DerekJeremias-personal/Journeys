using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.Notifications
{
    public interface INotificationAdapterFactory
    {
        INotificationAdapter CreateAdapter(string adapterType);
        bool IsAdapterTypeSupported(string adapterType);
    }
}
