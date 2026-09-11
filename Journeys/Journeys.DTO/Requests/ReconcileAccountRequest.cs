using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Requests
{
    public class ReconcileAccountRequest
    {
        public string? AccountId { get; set; }

        public string? ExternalId { get; set; }

        public bool? Reprocess {  get; set; }

    }
}
