using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Models
{
    public class PagedResultSet<T>
    {
        public string? ContinuationToken { get; set; }
        public int? Count { get; set; }
        public List<T>? Entities { get; set; }

        public PagedResultSet<T> AddRange(PagedResultSet<T> resultSet)
        {
            this.Entities.AddRange(resultSet.Entities);
            return this;
        }
    }
}
