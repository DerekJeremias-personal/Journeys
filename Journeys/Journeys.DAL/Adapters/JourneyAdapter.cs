using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DAL.Adapters
{
    //public class JourneyAdapter : BaseAdapter<LoyaltyAccountJourney>, IJourneyAdapter
    public class JourneyAdapter : IJourneyAdapter
    {
        private const string JOURNEY_MODEL_ID = "";

        public JourneyAdapter(IDynamicDataAdapter dynAdapter) //: base(dynAdapter)
        {
        }

        //public async Task<LoyaltyAccountJourney> FetchUserJourneyAsync(string tenantId, string userJourneyId)
        //{
        //    return await base.FetchEntityAsync(tenantId, userJourneyId, JOURNEY_MODEL_ID).ConfigureAwait(false);
        //}

        //public async Task<List<LoyaltyAccountJourney>> GetUsersJourneysAsync(string tenantId, List<string> filters)
        //{
        //    return await base.GetEntitiesByFiltersAsync(tenantId, JOURNEY_MODEL_ID, filters).ConfigureAwait(false);
        //}

        //public async Task<LoyaltyAccountJourney> UpsertUserJourneyAsync(string tenantId, LoyaltyAccountJourney journey)
        //{
        //    return await base.UpsertEntityAsync(tenantId, JOURNEY_MODEL_ID, journey).ConfigureAwait(false);
        //}

        //public async Task DeleteUserJourneyAsync(string tenantId, string userJourneyId)
        //{
        //    await base.DeleteEntityAsync(tenantId, JOURNEY_MODEL_ID, userJourneyId);
        //}

        //public async Task DeleteUserJourneyAsync(string tenantId, LoyaltyAccountJourney journey)
        //{
        //    await base.DeleteEntityAsync(tenantId, JOURNEY_MODEL_ID, journey).ConfigureAwait(false);
        //}

        public async Task<LoyaltyAccountJourneyState?> LoadLoyaltyAccountJourneyState(string tenantId, LoyaltyAccount loyaltyAccount)
        {
            return new LoyaltyAccountJourneyState(loyaltyAccount.Id, loyaltyAccount.Journeys);
        }

        public async Task<LoyaltyAccountJourneyState> SaveLoyaltyAccountJourneyState(LoyaltyAccountJourneyState loyaltyAccountJourney, LoyaltyAccount loyaltyAccount)
        {
            if (loyaltyAccountJourney == null || loyaltyAccount == null) return null;

            loyaltyAccount.Journeys = loyaltyAccountJourney.Journeys?.Select(j => new LoyaltyAccountJourney(j.Key, j.Value))?.ToList();

            return loyaltyAccountJourney;
        }
    }
}
