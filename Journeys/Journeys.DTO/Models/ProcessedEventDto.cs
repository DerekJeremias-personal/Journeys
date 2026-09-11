using Journeys.DTO.Models.RulesEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Models
{
    public class ProcessedEventDto
    {
        public string TenantId { get; set; }

        public EventPayloadResponseDto ProcessedEvent { get; set; }




    }
}
