using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Requests
{
    public class GetCampaignsByFilterRequest
    {
        public string Query { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public int PageSize { get; set; }

        public string? ContinuationToken { get; set; }

        public string? SortBy { get; set; }
        public SortOrder SortOrder { get; set; }

        public GetCampaignsByFilterRequest()
        {
            Query = String.Empty;
            Parameters = new Dictionary<string, object>();
            PageSize = 50;
            SortBy = String.Empty;
            SortOrder = SortOrder.ASC;
        }
    }
}
