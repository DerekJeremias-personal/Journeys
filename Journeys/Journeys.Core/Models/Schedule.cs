using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.Core.Models
{
    public class Schedule : ModelBase
    {
        [JsonConstructor]
        public Schedule(string name, string status, string type, DateTimeOffset startDate, DateTimeOffset? endDate,
            decimal? frequency, string? frequencyUnit, DateTimeOffset? lastRunDate, decimal? lastRunDuration,
            DateTimeOffset? nextRunDate, string id) : base(id)
        {
            Name = name;
            Status = status;
            Type = type;
            StartDate = startDate;
            EndDate = endDate;
            Frequency = frequency;
            FrequencyUnit = frequencyUnit;
            LastRunDate = lastRunDate;
            LastRunDuration = lastRunDuration;
            NextRunDate = nextRunDate;
        }

        public string Name { get; set; }
        public string Status { get; set; }
        public string Type { get; set; }
        public DateTimeOffset StartDate { get; set; }
        public DateTimeOffset? EndDate { get; set; }
        public decimal? Frequency { get; set; }
        public string? FrequencyUnit { get; set; }
        public DateTimeOffset? LastRunDate { get; set; }
        public decimal? LastRunDuration { get; set; }
        public DateTimeOffset? NextRunDate { get; set; }


    }
}
