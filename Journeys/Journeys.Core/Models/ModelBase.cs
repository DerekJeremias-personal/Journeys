using Journeys.Core.JsonConverters;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.Core.Models
{
    public abstract class ModelBase
    {
        [JsonIgnore]
        public bool CalculateOnly => false;

        [JsonConstructor]
        public ModelBase(string? id = null, DateTimeOffset? createdate = null, DateTimeOffset? lastupdated = null)
        {
            Id = id;
            CreateDate = createdate;
            LastUpdated = lastupdated;
        }

        public string? Id 
        { 
            get; 
            set; 
        }

        [JsonIgnore]
        public string ModelId { get; set; }

        //[JsonConverter(typeof(CustomDateTimeConverter))]
        public DateTimeOffset? CreateDate { get; set; } = DateTime.UtcNow;

        //[JsonConverter(typeof(CustomDateTimeConverter))]
        public DateTimeOffset? LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
