using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Models
{
    public class PointAccountTypeDto : DtoModelBase
    {
        public string? ExtAccountId { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
        public string? PointSourceId { get; set; }
        public string LedgerType { get; set; }

        public decimal? PointsLifespanDays { get; set; }
        public DateTimeOffset? PointsLifespanEndDate { get; set; }

        public string? ExpiresToPointAccountTypeId { get; set; }

        public bool? IsSpendable { get; set; }

        public string? RoundingOptionString { get; set; }
        public decimal RoundingDecimalPlaces { get; set; } = 0;
    }
}
