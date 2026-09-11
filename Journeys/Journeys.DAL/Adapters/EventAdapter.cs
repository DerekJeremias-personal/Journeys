using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.Entities;
using Journeys.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Journeys.DAL.Adapters
{
    //public class EventAdapter<T> : ReferenceableBaseAdapter<TenantedModelBase>, IEventAdapter where T : TenantedModelBase
    //{
    //    private const string EVENT_TYPE = "EventType";

    //    public EventAdapter(IDynamicDataAdapter dynAdapter, IExternalReferenceAdapter extReferenceAdapter) : base(dynAdapter, extReferenceAdapter)
    //    {
    //        var typeMap = new Dictionary<string, Type>
    //            {
    //                { "Journeys.Models.Order", typeof(Order) },
    //                // Add other derived types here
    //            };

    //        base.SetPolymorphicTypes(typeMap, EVENT_TYPE);

    //        extReferenceAdapter.SetPolymorphicTypes(typeMap, EVENT_TYPE);
    //    }

    //    public async Task<T> GetEventAsync(string tenantId, string modelId, string eventId)
    //    {
    //        return (T) await base.FetchEntityAsync(tenantId, eventId, modelId).ConfigureAwait(false);
    //    }

    //    public async Task<T> GetEventByExtIdAsync(string tenantId, string modelId, string extId)
    //    {
    //        return (T) await base.GetEntityByExtId(tenantId, modelId, extId).ConfigureAwait(false);
    //    }

    //    public async Task<object> UpsertEventAsync(string tenantId, string modelId, object eventData)
    //    {
    //        var evt = (T)eventData;
    //        if (evt is IIdempotent<T> idempotent)
    //        {
    //            //Get stored event
    //            var storedEvent = !string.IsNullOrEmpty(((TenantedModelBase)eventData).Id) ?
    //                await GetEventAsync(tenantId, modelId, ((TenantedModelBase)eventData).Id) : null;

    //            if (storedEvent == null && evt is IReferenceable referencable) 
    //            {
    //                storedEvent = await GetEventByExtIdAsync(tenantId, modelId, referencable.ExternalId);
    //            }

    //            if (storedEvent != null && !idempotent.IsNewer(storedEvent, (T)eventData))
    //            {
    //                return storedEvent;
    //            }
    //        }
    //        return await base.UpsertEntityAsync(tenantId, modelId, (T)eventData, typeof(T)).ConfigureAwait(false);
    //    }

    //    public JsonSerializerOptions GetJsonSerializerOptions()
    //    {
    //        return base.GetJsonSerializerOptions();
    //    }
    //}

}
