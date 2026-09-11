using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Models.Reports
{
    public class TotalSpendDto
    {
        public string LoyaltyAccountId { get; set; }
        public decimal TotalValue { get; set; }
        public int OrderCount { get; set; }
    }
}
