using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Tests.Stubs
{
    internal class StubUserJourneyAdapter : IJourneyAdapter
    {
        private Dictionary<string, Dictionary<string, LoyaltyAccountJourneyState>> _journeys = new Dictionary<string, Dictionary<string, LoyaltyAccountJourneyState>>();
        public async Task<LoyaltyAccountJourneyState?> LoadLoyaltyAccountJourneyState(string tenantId, LoyaltyAccount account)
        {
            if(!_journeys.ContainsKey(tenantId))
                _journeys.Add(tenantId, new Dictionary<string, LoyaltyAccountJourneyState>());

            if(_journeys[tenantId].ContainsKey(account.Id))
                return await Task.FromResult(_journeys[tenantId][account.Id]);

            return null;
        }

        public async Task<LoyaltyAccountJourneyState> SaveLoyaltyAccountJourneyState(LoyaltyAccountJourneyState accountJourney, LoyaltyAccount account)
        {
            if (!_journeys.ContainsKey(account.TenantId))
                _journeys.Add(account.TenantId, new Dictionary<string, LoyaltyAccountJourneyState>());

            _journeys[account.TenantId][account.Id] = accountJourney;
            return await Task.FromResult(accountJourney);
        }
    }
}
