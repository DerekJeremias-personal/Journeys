using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Journeys.Core.Models
{
    public class ExternalReference : TenantedModelBase
    {
        [JsonConstructor]
        public ExternalReference(string type, string status, string mapFromId, string mapToId,
            string tenantId, string? id, string? etag = null) : base(tenantId, id)
        {
            Type = type;
            Status = status;
            MapFromId = mapFromId;
            MapToId = mapToId;
            ETag = etag;
        }

        public string Type { get; set; }

        public string Status { get; set; }

        public string MapFromId { get; set; }

        public string MapToId { get; set; }

    }
}
