using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Requests
{
    public class GetManyPointsDetailsRequest
    {
        public string LoyaltyAccountId { get; set; }
        public List<EventPair> Events { get; set; }
    }

    public class EventPair
    {
        public string EventType { get; set; }
        public string EventId { get; set; }
    }
}
