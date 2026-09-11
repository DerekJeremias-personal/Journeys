using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Models
{
    public class TagDto : DtoModelBase
    {
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
