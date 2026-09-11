using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Requests
{
    public class EnqueueRequest
    {
        public string JobId { get; set; }
        public string BatchFileId { get; set; }
    }
}
