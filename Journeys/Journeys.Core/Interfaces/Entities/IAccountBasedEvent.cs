using Journeys.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.Entities
{
    public interface IAccountBasedEvent
    {
        string LoyaltyAccountId { get; }
        bool CalculateOnly { get; }
        void SetLoyaltyAccount(LoyaltyAccount account);
    }

    public interface ITemporalEvent
    {
        DateTimeOffset TimeOfOccurrence { get; set; }
    }
}
