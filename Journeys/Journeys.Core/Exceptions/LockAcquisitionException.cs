using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Exceptions
{
    public class LockAcquisitionException : Exception
    {
        public LockAcquisitionException(string message, string accountId, string concurrentLockLeaseKey, string storedLockLeaseKey) : base(message)
        {
            LoyaltyAccountId = accountId;
            concurrentLockLeaseKey = concurrentLockLeaseKey;
            StoredLockLeaseKey = storedLockLeaseKey;
        }
        public string LoyaltyAccountId { get; set; }
        public string CurrentLockLeaseKey { get; set; }
        public string StoredLockLeaseKey { get; set; }
    }
}
