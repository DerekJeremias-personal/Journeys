using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Responses
{
    public class FileResponse
    {
        public Stream FileStream { get; set; }
        public string ContentType { get; set; }
    }
}
