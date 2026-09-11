using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Infra.Backend.Models
{
    public class PagedRequestBase
    {
        public string? ModelId { get; set; }
        public string? ModelType { get; set; }
        public string? ModelName { get; set; }

        public int PageSize { get; set; }
        public string? ContinuationToken { get; set; }
    }
}
