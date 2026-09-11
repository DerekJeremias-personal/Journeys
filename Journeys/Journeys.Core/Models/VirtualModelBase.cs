using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.Core.Models
{
    public class VirtualModelBase : TenantedModelBase
    {
        [JsonConstructor]
        public VirtualModelBase(string eventType, string tenantId, string id) : base(tenantId, id)
        {
            EventType = eventType;
        }

        public string EventType;
    }
}
