using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.Core.Models
{
    public class PointsDetails : TenantedModelBase
    {
        [JsonConstructor]
        public PointsDetails(string loyaltyAccountId, string eventId, string eventType, string? name, string? description, decimal? quantity,
                            string tenantId, string id) : base(tenantId, id)
        {
            LoyaltyAccountId = loyaltyAccountId;
            EventId = eventId;
            EventType = eventType;
            Name = name;
            Description = description;
            Quantity = quantity;
        }

        public string LoyaltyAccountId { get; set; }
        public string EventId { get; set; }
        public string EventType { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public decimal? Quantity { get; set; }


    }
}
