using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.Core.Models
{
    public abstract class TenantedModelBase : ModelBase
    {
        [JsonConstructor]
        public TenantedModelBase(string tenantId, string? id, DateTimeOffset? createdate = null,
                    DateTimeOffset? lastupdated = null) : base(id, createdate, lastupdated)
        {
            TenantId = tenantId;
        }

        public string TenantId { get; set; }

        [JsonPropertyName("etag")]
        public string ETag { get; set; }
    }
}
