using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Infra.Backend.Models
{
    public class GetByPKRequest : PagedRequestBase
    {
        public string PK { get; set; }
        public string? PK2 { get; set; }
    }
}
