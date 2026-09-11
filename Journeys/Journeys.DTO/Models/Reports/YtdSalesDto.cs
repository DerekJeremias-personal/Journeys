using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Models.Reports
{
    public class YtdSalesDto
    {
        public string LoyaltyAccountId { get; set; }
        public decimal YtdSalesDollars { get; set; }
        public int YtdSalesCount { get; set; }
    }
}
