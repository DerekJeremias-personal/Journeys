using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine.Providers.Historical.State;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Tests.Stubs
{
    //public class StubLoyaltyAccountRuleStateAdapter : ILoyaltyAccountRuleStateAdapter
    //{
    //    public Task<T?> EnqueueStateToLoadAsync<T>(string tenantId, string rollupEntityId, string providerId, string eventType) where T : HistoricalStateBase
    //    {
    //        return Task.FromResult<T?>(default);
    //    }

    //    public Task<T> EnqueueStateToSaveAsync<T>(string tenantId, string rollupEntityId, string eventType, T state) where T : HistoricalStateBase
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public Task<List<LoyaltyAccountRuleState>> LoadEnqueuedStatesAsync(string tenantId, string rollupEntityId)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public Task<List<LoyaltyAccountRuleState>> SaveEnqueuedStatesAsync(string tenantId, string rollupEntityId, List<LoyaltyAccountRuleState> loadedStateObjects)
    //    {
    //        throw new NotImplementedException();
    //    }
    //}
}
