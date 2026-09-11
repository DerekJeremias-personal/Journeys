using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.Core.Models
{
    public class Tag : TenantedModelBase
    {
        [JsonConstructor]
        public Tag(string type, string entityId, string name, string value,
            DateTimeOffset? effectiveStartDate, DateTimeOffset? effectiveEndDate,
            TimeSpan? effectiveStartTime, TimeSpan? effectiveEndTime, decimal? ttlSec,
            string tenantId, string id) : base(tenantId, id)
        {
            Type = type;
            EntityId = entityId;
            Name = name;
            Value = value;
            EffectiveStartDate = effectiveStartDate;
            EffectiveEndDate = effectiveEndDate;
            EffectiveStartTime = effectiveStartTime;
            EffectiveEndTime = effectiveEndTime;
            TtlSec = ttlSec;
        }

        public string Type { get; set; }
        public string EntityId { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public DateTimeOffset? EffectiveStartDate { get; set; }
        public DateTimeOffset? EffectiveEndDate { get; set; }
        public TimeSpan? EffectiveStartTime { get; set; }
        public TimeSpan? EffectiveEndTime { get; set; }
        public decimal? TtlSec { get; set; }


    }
}
