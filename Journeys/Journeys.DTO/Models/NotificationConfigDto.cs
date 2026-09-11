using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Journeys.DTO.Models
{
    public static class ConfigStatus
    {
        public const string ACTIVE = "active";
        public const string INACTIVE = "inactive";
        public const string DELETED = "deleted";
    }

    public static class AdapterTypes
    {
        public const string REST_API = "rest_api";
        public const string TWILIO = "twilio";
        public const string EMAIL = "email";
        public const string DATA_PIPELINE = "data_pipeline";
    }

    public class NotificationConfigDto : DtoModelBase
    {
        public string Name { get; set; }

        public string AdapterType { get; set; }

        public string Status { get; set; } = ConfigStatus.ACTIVE;

        public JsonElement ConfigDetails { get; set; }

        /// <summary>
        /// The event model ID that this notification config should listen to.
        /// If null, the notification will be sent for all events.
        /// </summary>
        public string? EventModelId { get; set; }
    }
}
