using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Requests
{
    public class BulkPointDepositRequest
    {
        public string LoyaltyAccountId { get; set; }
        public List<PointDepositlDetails> PointsDepositEntries { get; set; }

    }

    public class PointDepositlDetails
    {
        public string PointAccountTypeId { get; set; }
        public decimal Amount { get; set; }
        public string EventId { get; set; }
        public string EventType { get; set; }
        public string? Status { get; set; }
        public string? UserId { get; set; }

        public DateTimeOffset DepositDate { get; set; }
    }
}
