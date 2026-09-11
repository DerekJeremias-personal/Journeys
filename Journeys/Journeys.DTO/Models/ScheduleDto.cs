using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Models
{
    public class ScheduleDto : DtoModelBase
    {
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
