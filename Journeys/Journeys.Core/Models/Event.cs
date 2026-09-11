using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.Core.Models
{
    public class Event : TenantedModelBase
    {
        [JsonConstructor]
        public Event(object data,
            string tenantId, string id) : base(tenantId, id)
        {
            Data = data;
        }

        public object Data { get; set; }
    }
}
