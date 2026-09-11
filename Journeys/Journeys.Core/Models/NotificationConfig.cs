using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.Text.Json;
using Journeys.DTO.Models;

namespace Journeys.Core.Models
{
    public class NotificationConfig : TenantedModelBase
    {


        [JsonConstructor]
        public NotificationConfig(
            string tenantId,
            string? id,
            string name,
            string adapterType,
            string status,
            JsonElement configDetails,
            string? eventModelId = null) : base(tenantId, id)
        {
            Name = name;
            AdapterType = adapterType;
            Status = status;
            ConfigDetails = configDetails;
            EventModelId = eventModelId;
        }

        public string Name { get; set; }

        public string AdapterType { get; set; }

        public string Status { get; set; } = ConfigStatus.ACTIVE;

        public JsonElement ConfigDetails { get; set; }

        /// <summary>
        /// The event model ID that this notification config should listen to.
        /// If null, the notification will be sent for all events.
        /// </summary>
        public string? EventModelId { get; set; }

        public DateTimeOffset CreateDate { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
    }
}
