using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Models
{
    public class PointsDetailsDto : DtoModelBase
    {
        public string LoyaltyAccountId { get; set; }
        public string EventId { get; set; }
        public string EventType { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public decimal? Quantity { get; set; }

    }
}
