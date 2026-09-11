using Journeys.DAL.Adapters;
using Journeys.DAL.Interfaces;
using Journeys.Core.Interfaces.DataStorage;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DAL.Factories
{
    //public class EventAdapterFactory : IEventAdapterFactory
    //{
    //    private readonly IServiceProvider _serviceProvider;

    //    public EventAdapterFactory(IServiceProvider serviceProvider)
    //    {
    //        _serviceProvider = serviceProvider;
    //    }

    //    public IEventAdapter Create(Type entityType)
    //    {
    //        // Create a new scope to resolve the scoped service
    //        using (var scope = _serviceProvider.CreateScope())
    //        {
    //            var adapterType = typeof(EventAdapter<>).MakeGenericType(entityType);
    //            return (IEventAdapter)scope.ServiceProvider.GetRequiredService(adapterType);
    //        }
    //    }
    //}
}
