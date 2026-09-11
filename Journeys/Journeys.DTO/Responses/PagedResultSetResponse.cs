using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Responses
{
    public class PagedResultSetResponse<T>
    {
        public string? ContinuationToken { get; set; }
        public int? Count { get; set; }
        public List<T>? Entities { get; set; }
    }
}
