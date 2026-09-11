using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.DataStorage
{
    public interface IEventAdapterFactory
    {
        IEventAdapter Create(Type entityType);
    }
}
