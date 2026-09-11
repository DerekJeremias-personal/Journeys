using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.DataStorage
{
    public interface IJourneyAdapter
    {
        Task<LoyaltyAccountJourneyState?> LoadLoyaltyAccountJourneyState(string tenantId, LoyaltyAccount account);
        Task<LoyaltyAccountJourneyState> SaveLoyaltyAccountJourneyState(LoyaltyAccountJourneyState loyaltyAccountJourney, LoyaltyAccount account);
    }
}
