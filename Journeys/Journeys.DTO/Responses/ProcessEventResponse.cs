using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Responses
{
    public class ProcessEventResponse : ResponseBase
    {
        public string EventId { get; set; }
        public string EventType { get; set; }

        public string EventModelId { get; set; }

        public bool Successful { get; set; } //false by default

    }
}
