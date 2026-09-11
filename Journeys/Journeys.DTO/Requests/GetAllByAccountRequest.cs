using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Requests
{
    public class GetAllByAccountRequest : GetAllRequest
    {
        public string LoyaltyAccountId { get; set; }
    }
}
