using Journeys.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.DataStorage
{
    public interface IEventAdapter
    {
        Task<object> UpsertEventAsync(string tenantId, string modelId, object eventData);

        JsonSerializerOptions GetJsonSerializerOptions();
    }

}
