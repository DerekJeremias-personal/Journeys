using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Responses
{
    public abstract class ResponseBase
    {
        public string TenantId { get; set; }

        public object ResponseObject { get; set; }

        public Dictionary<string, string> Errors { get; set; }

    }
}
