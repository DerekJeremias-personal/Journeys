using Journeys.DTO.Requests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Infra.Backend.Models
{
    public class QueryObjectsRequest : PagedRequestBase
    {
        public string Query { get; set; }

        public Dictionary<string, object>? Parameters { get; set; }
        public string? SortBy { get; set; }
        public SortOrder SortOrder { get; set; }
        public bool? IncludeChildModels { get; set; }
    }
}
