using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Requests
{
    public class JourneyPreviewRequest
    {
        public string? LoyaltyAccountId { get; set; }
        public string? LoyaltyAccountXReference { get; set; }

        public DateTimeOffset StartDateUTC { get; set; }
        public DateTimeOffset EndDateUTC { get; set; }

    }
}
