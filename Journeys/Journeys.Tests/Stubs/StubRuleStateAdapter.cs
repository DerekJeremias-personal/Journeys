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
    //public class StubRuleStateAdapter : ILoyaltyAccountRuleStateAdapter
    //{
    //    Dictionary<string, Dictionary<string, Dictionary<string, TaskCompletionSource<LoyaltyAccountRuleState?>>>> _rollupEventTypeStatesToLoad = new Dictionary<string, Dictionary<string, Dictionary<string, TaskCompletionSource<LoyaltyAccountRuleState?>>>>();
    //    Dictionary<string, Dictionary<string, Dictionary<string, TaskCompletionSource<LoyaltyAccountRuleState>>>> _rollupEventTypeStatesToSave = new Dictionary<string, Dictionary<string, Dictionary<string, TaskCompletionSource<LoyaltyAccountRuleState>>>>();
    //    Dictionary<string, Dictionary<string, Dictionary<string, LoyaltyAccountRuleState>>> _inMemoryStates = new Dictionary<string, Dictionary<string, Dictionary<string, LoyaltyAccountRuleState>>>();

    //    Dictionary<string, LoyaltyAccountRuleState> dbMirror = new Dictionary<string, LoyaltyAccountRuleState>();

    //    private static string GenerateKey(string tenantId, string userId, string modelId)
    //    {
    //        return $"{tenantId}|{userId}|{modelId}";
    //    }

    //    public async Task<T?> EnqueueStateToLoadAsync<T>(string tenantId, string rollupEntityId, string providerId, string eventType) where T : HistoricalStateBase
    //    {
    //        if (!_rollupEventTypeStatesToLoad.ContainsKey(tenantId))
    //        {
    //            _rollupEventTypeStatesToLoad[tenantId] = new Dictionary<string, Dictionary<string, TaskCompletionSource<LoyaltyAccountRuleState?>>>();
    //        }

    //        if (!_rollupEventTypeStatesToLoad[tenantId].ContainsKey(rollupEntityId))
    //        {
    //            _rollupEventTypeStatesToLoad[tenantId][rollupEntityId] = new Dictionary<string, TaskCompletionSource<LoyaltyAccountRuleState?>>();
    //        }

    //        if (!_rollupEventTypeStatesToLoad[tenantId][rollupEntityId].ContainsKey(eventType))
    //        {
    //            _rollupEventTypeStatesToLoad[tenantId][rollupEntityId][eventType] = new TaskCompletionSource<LoyaltyAccountRuleState?>();
    //        }

    //        var loadedState = _rollupEventTypeStatesToLoad[tenantId][rollupEntityId][eventType];
    //        var stateDoc = await loadedState.Task;
    //        if(stateDoc?.State.ContainsKey(providerId) ?? false)
    //        {
    //            return stateDoc.State[providerId] as T;
    //        }
    //        else
    //        {
    //            return default(T?);
    //        }
    //    }

    //    public async Task<T> EnqueueStateToSaveAsync<T>(string tenantId, string rollupEntityId, string eventType, T state) where T : HistoricalStateBase
    //    {
    //        //Add the value to the enqueued states dictionary.
    //        if (!_rollupEventTypeStatesToSave.ContainsKey(tenantId))
    //            _rollupEventTypeStatesToSave[tenantId] = new Dictionary<string, Dictionary<string, TaskCompletionSource<LoyaltyAccountRuleState>>>();

    //        if (!_rollupEventTypeStatesToSave[tenantId].ContainsKey(rollupEntityId))
    //            _rollupEventTypeStatesToSave[tenantId][rollupEntityId] = new Dictionary<string, TaskCompletionSource<LoyaltyAccountRuleState>>();

    //        if (!_rollupEventTypeStatesToSave[tenantId][rollupEntityId].ContainsKey(eventType))
    //        {
    //            var tcs = new TaskCompletionSource<LoyaltyAccountRuleState>();
    //            _rollupEventTypeStatesToSave[tenantId][rollupEntityId][eventType] = tcs;
    //        }

    //        //Keep an in-memory LoyaltyAccountRuleState document.
    //        if(!_inMemoryStates.ContainsKey(tenantId))
    //            _inMemoryStates[tenantId] = new Dictionary<string, Dictionary<string, LoyaltyAccountRuleState>>();

    //        if (!_inMemoryStates[tenantId].ContainsKey(rollupEntityId))
    //            _inMemoryStates[tenantId][rollupEntityId] = new Dictionary<string, LoyaltyAccountRuleState>();

    //        if (!_inMemoryStates[tenantId][rollupEntityId].ContainsKey(eventType))
    //            _inMemoryStates[tenantId][rollupEntityId][eventType] = new LoyaltyAccountRuleState(tenantId, rollupEntityId, eventType, new List<HistoricalStateBase>());

    //        _inMemoryStates[tenantId][rollupEntityId][eventType].State[state.ProviderId] = state;

    //        var loadedState = _rollupEventTypeStatesToSave[tenantId][rollupEntityId][eventType];
    //        var stateDoc = await loadedState.Task;
    //        stateDoc.State[state.ProviderId] = state;
    //        return state;
    //    }

    //    public async Task<List<LoyaltyAccountRuleState>> LoadEnqueuedStatesAsync(string tenantId, string rollupEntityId)
    //    {
    //        if (!_rollupEventTypeStatesToLoad.ContainsKey(tenantId))
    //        {
    //            _rollupEventTypeStatesToLoad[tenantId] = new Dictionary<string, Dictionary<string, TaskCompletionSource<LoyaltyAccountRuleState?>>>();
    //            return new List<LoyaltyAccountRuleState>();
    //        }

    //        var enqueued = _rollupEventTypeStatesToLoad[tenantId][rollupEntityId];

    //        var states = new List<LoyaltyAccountRuleState>();
    //        foreach (var e in enqueued)
    //        {
    //            var k = GenerateKey(tenantId, rollupEntityId, e.Key);
    //            if (dbMirror.ContainsKey(k))
    //            {
    //                states.Add(dbMirror[k]);
    //                e.Value.SetResult(dbMirror[k]);
    //            }
    //            else
    //            {
    //                e.Value.SetResult(default);
    //            }
    //        }
            
    //        //Release the object from memory.
    //        _rollupEventTypeStatesToLoad[tenantId].Remove(rollupEntityId);

    //        return states;
    //    }

    //    public async Task<List<LoyaltyAccountRuleState>> SaveEnqueuedStatesAsync(string tenantId, string rollupEntityId, List<LoyaltyAccountRuleState> loadedStateObjects)
    //    {
    //        var entities = _rollupEventTypeStatesToSave[tenantId][rollupEntityId];
    //        var result = new List<LoyaltyAccountRuleState>();
    //        foreach(var entity in entities)
    //        {
    //            var k = GenerateKey(tenantId, rollupEntityId, entity.Key);
    //            var raw = dbMirror[k];
    //            var updated = _inMemoryStates[tenantId][rollupEntityId][entity.Key];
                
    //            dbMirror[k] = updated;
    //            result.Add(updated);
    //        }

    //        return result;
    //    }
    //}
}
