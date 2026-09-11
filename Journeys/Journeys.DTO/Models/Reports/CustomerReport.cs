using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Models.Reports
{
    public class CustomerReportResult
    {
        public string CurrentDateTime { get; set; }
        public string SnapshotDateEndOfDay { get; set; }
        public List<CustomerReport> Customers { get; set; } = new();
    }

    public class CustomerReport
    {
        public string CustomerId { get; set; }
        public string CustomerName { get; set; }
        public bool IsActive { get; set; }

        public decimal PointsRedeemedYtd { get; set; }
        public string Tier { get; set; }

        public decimal YtdSalesDollars { get; set; }
        public int YtdSalesCount { get; set; }

        public decimal DealerArchiveCurrentBalance { get; set; }
        public decimal DealerArchiveLifetimeTotal { get; set; }

        public decimal DealerSpendableCurrentBalance { get; set; }
        public decimal DealerSpendableLifetimeTotal { get; set; }

        public decimal DealerExpiredCurrentBalance { get; set; }
        public decimal DealerExpiredLifetimeTotal { get; set; }

        public decimal DealerTierQualifyingCurrentBalance { get; set; }
        public decimal DealerTierQualifyingLifetimeTotal { get; set; }

        public string LastModifiedDate { get; set; }
    }
}
