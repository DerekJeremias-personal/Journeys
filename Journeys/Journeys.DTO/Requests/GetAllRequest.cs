using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Requests
{
    public class GetAllRequest
    {
        public int PageSize { get; set; }

        public string? ContinuationToken {  get; set; }


    }
}
