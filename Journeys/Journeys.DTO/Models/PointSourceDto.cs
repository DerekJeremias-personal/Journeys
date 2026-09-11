using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Models
{
    public class PointSourceDto : DtoModelBase
    {
        public string ExtSourceId { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
        public double CurrentBalance { get; set; }
        public double OriginalBalance { get; set; }
        public double MaxAward { get; set; }
        public ScheduleDto ExpirationSchedule { get; set; }
    }
}
