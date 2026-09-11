using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Requests
{
    public class GetManyCampaignsRequest
    {
        public List<string> Ids { get; set; }
        public string Status { get; set; }
    }
}
