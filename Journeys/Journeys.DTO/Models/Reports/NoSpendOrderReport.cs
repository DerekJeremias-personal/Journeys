using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Models.Reports
{

    public class NonOrderReportResult
    {
        public string CurrentDateTime { get; set; }
        public string SnapshotDateEndOfDay { get; set; }
        public List<NonSpendOrderReport> Events { get; set; } = new();
    }

    public class NonSpendOrderReport
    {
        public string CustomerId { get; set; }
        public string CustomerName { get; set; }
        public bool? IsActive { get; set; }

        public string EventType { get; set; }
        public decimal? PointsDeposited { get; set; }
        public decimal? PointsWithdrawn { get; set; }

        public DateTimeOffset? EarnDate { get; set; }
        public DateTimeOffset? BurnDate { get; set; }
        public string AdminUserId { get; set; }
    }


}
