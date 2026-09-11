using Journeys.Core.Models;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Providers.Enums;
using Journeys.Core.RulesEngine.Providers.Historical.State;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.Utility;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.RulesEngine.Providers.Historical
{
    public class SimpleCalculationProvider : ProviderBase, IHistoricalValueProvider
    {
        #region Calculation Helpers...
        private static string GetEventId(RulesEngineState state)
        {
            //var id = state.Event?.GetValue("id") as string ?? state.Event?.GetValue("eventId") as string ?? state.Event?.GetValue("EventId") as string ?? state.Event?.GetValue("Id") as string ?? state.EventId;
            var strId = EventKeyUtility.ToEventKey(state.EventType, state.EventId);
            
            if (string.IsNullOrEmpty(strId))
            {
                throw new NullReferenceException("id is required in the Event payload for TimeToLive and engine state enrichment.");
            }
            return strId;
        }
        private static async Task<SimpleState?> ProcessAverage(RulesEngineState node, SimpleCalculationProvider self, SimpleState state, CancellationToken token)
        {
            if (self == null || self.InstanceValueProvider == null)
                throw new NullReferenceException("Both a SimpleCalculationProvider instance and the InstanceValueProvider are required for the AVG operation.");

            if (self.TemporalConstraint == null)
                throw new NullReferenceException("A TemoralConstraint is required for TimeToLive decay.");

            var value = await self.InstanceValueProvider.GetValue<decimal>(node, token);
            var stateKey = node.CurrentHistoricalStateKey;
            var existingTtl = stateKey != null && node.ExistingEventTtlByStateKey.TryGetValue(stateKey, out var existing) ? existing : null;
            if (existingTtl != null)
            {
                var previousValue = existingTtl.ContributionValue ?? 0;
                state.Value += (value - previousValue);
            }
            else
            {
                state.Count++;
                state.Value += value;
            }
            var ttl = await self.TemporalConstraint.CalculateDecayDate(node, token);
            (state.ContributorTTLsList ??= new List<StateTTL>()).Add(new StateTTL(GetEventId(node), ttl, value));
            return state;
        }

        private static async Task<SimpleState?> ProcessMin(RulesEngineState node, SimpleCalculationProvider self, SimpleState state, CancellationToken token)
        {
            if (self == null || self.InstanceValueProvider == null)
                throw new NullReferenceException("Both a SimpleCalculationProvider instance and the InstanceValueProvider are required for the MIN operation.");

            if (self.TemporalConstraint == null)
                throw new NullReferenceException("A TemoralConstraint is required for TimeToLive decay.");

            var value = await self.InstanceValueProvider.GetValue<decimal>(node, token);
            state.Count++;
            if (state.Value > value)
            {
                state.Value = value;
            }
            var ttl = await self.TemporalConstraint.CalculateDecayDate(node, token);
            (state.ContributorTTLsList ??= new List<StateTTL>()).Add(new StateTTL(GetEventId(node), ttl, value));
            return state;
        }

        private static async Task<SimpleState?> ProcessMax(RulesEngineState node, SimpleCalculationProvider self, SimpleState state, CancellationToken token)
        {
            if (self == null || self.InstanceValueProvider == null)
                throw new NullReferenceException("Both a SimpleCalculationProvider instance and the InstanceValueProvider are required for the MAX operation.");

            if (self.TemporalConstraint == null)
                throw new NullReferenceException("A TemoralConstraint is required for TimeToLive decay.");

            var value = await self.InstanceValueProvider.GetValue<decimal>(node, token);
            state.Count++;
            if (state.Value < value)
            {
                state.Value = value;
            }
            var ttlMax = await self.TemporalConstraint.CalculateDecayDate(node, token);
            (state.ContributorTTLsList ??= new List<StateTTL>()).Add(new StateTTL(GetEventId(node), ttlMax, value));
            return state;
        }

        private static async Task<SimpleState?> ProcessSum(RulesEngineState node, SimpleCalculationProvider self, SimpleState state, CancellationToken token)
        {
            if (self == null || self.InstanceValueProvider == null)
                throw new NullReferenceException("Both a SimpleCalculationProvider instance and the InstanceValueProvider are required for the SUM operation.");

            if (self.TemporalConstraint == null)
                throw new NullReferenceException("A TemoralConstraint is required for TimeToLive decay.");

            var value = await self.InstanceValueProvider.GetValue<decimal>(node, token);
            var stateKey = node.CurrentHistoricalStateKey;
            var existingTtl = stateKey != null && node.ExistingEventTtlByStateKey.TryGetValue(stateKey, out var existing) ? existing : null;
            if (existingTtl != null)
            {
                var previousValue = existingTtl.ContributionValue ?? 0;
                state.Value += (value - previousValue);
            }
            else
            {
                state.Count++;
                state.Value += value;
            }
            var ttlSum = await self.TemporalConstraint.CalculateDecayDate(node, token);
            (state.ContributorTTLsList ??= new List<StateTTL>()).Add(new StateTTL(GetEventId(node), ttlSum, value));
            return state;
        }

        private static async Task<SimpleState?> ProcessCount(RulesEngineState node, SimpleCalculationProvider self, SimpleState state, CancellationToken token)
        {
            if (self.TemporalConstraint == null)
                throw new NullReferenceException("A TemoralConstraint is required for TimeToLive decay.");

            var stateKey = node.CurrentHistoricalStateKey;
            var isUpdate = stateKey != null && node.ExistingEventTtlByStateKey.ContainsKey(stateKey);
            if (!isUpdate)
                state.Count++;
            var ttlCount = await self.TemporalConstraint.CalculateDecayDate(node, token);
            (state.ContributorTTLsList ??= new List<StateTTL>()).Add(new StateTTL(GetEventId(node), ttlCount));
            return state;
        }


        private static Dictionary<AggregateType, Func<RulesEngineState, SimpleCalculationProvider, SimpleState, CancellationToken, Task<SimpleState?>>>
            _aggregateFunctions = new Dictionary<AggregateType, Func<RulesEngineState, SimpleCalculationProvider, SimpleState, CancellationToken, Task<SimpleState?>>>
            {
                    { AggregateType.Average, ProcessAverage },
                    { AggregateType.Min, ProcessMin },
                    { AggregateType.Max, ProcessMax },
                    { AggregateType.Sum, ProcessSum },
                    { AggregateType.Count, ProcessCount }
            };
        #endregion

        public override string Kind => ProviderKindDiscriminators.SimpleCalculationProvider;
        public TemporalConstraintRule? TemporalConstraint { get; set; }

        public string Id { get; set; } = Guid.NewGuid().ToString();
        public IValueProvider? InstanceValueProvider { get; set; }
        public AggregateType AggregateType { get; set; }

        public RuleBase? CalculationGatingConstraint { get; set; }

        public SimpleCalculationProvider() { }

        public async Task<T?> GetValue<T>(RulesEngineState state, CancellationToken token)
        {
            if (!typeof(T).IsAssignableFrom(typeof(SimpleState)))
            {
                throw new NotSupportedException("SimpleCalculation only supports generation of HistoricalStateBase derrived objects.");
            }

            if (!await ShouldCalculateAsync(state, token))
            {
                return default;
            }

            HistoricalStateBase? providerState;
            var key = state.CurrentHistoricalStateKey ?? Id;
            if (!state.LoadedState.TryGetValue(key, out providerState))
                throw new InvalidOperationException($"LoadStateAsync must be called prior to GetValue. Encountered in SimpleCalculation Id: {Id}");

            if (providerState == null)
                throw new InvalidOperationException($"Provider State loaded from cache successfully, however, was null. Unrecoverable fatal state. Encountered in SimpleCalculation Id: {Id}");

            if (InstanceValueProvider == null)
                throw new NullReferenceException("InstanceValueProvider must NOT be null prior to applying aggregate operations.");

            if (TemporalConstraint == null)
                throw new NullReferenceException($"TemporalConstraint must NOT be null prior to applying aggregate operations, encountered in SimpleCalculationProvider Id: {Id}.");


            if (_aggregateFunctions.ContainsKey(AggregateType))
            {
                var func = _aggregateFunctions[AggregateType];
                if (func != null)
                    providerState = await func(state, this, (SimpleState)providerState, token);
            }
            else
                throw new ArgumentException($"AggregateType {AggregateType} is not supported.");

            if (providerState == null)
                throw new Exception("providerState was null after aggregate operation. Unrecoverable fatal state.");

            if (providerState is SimpleState simpleState && !simpleState.FirstOccurrence.HasValue)
            {
                var timeOfOccurrence = await TemporalConstraint.TimeOfOccurrenceProvider.GetValue<DateTimeOffset?>(state, token);
                //Get the time of occurrence from the Event
                //If the event does not have a TimeOfOccurrence, default to NOW for the event.
                simpleState.FirstOccurrence = timeOfOccurrence.HasValue ? timeOfOccurrence.Value : DateTimeOffset.UtcNow;
                if (!simpleState.FirstOccurrence.HasValue)
                {
                    System.Diagnostics.Debug.WriteLine($"Provided Event entity did not have a timeOfOccurrence. Defaulting to system time for Event (Model Id: {state?.EventModelId}) timeOfOccurrence. Event: {state?.Event?.ToString()}");
                    //TODO: Log an error here.
                }
                providerState = simpleState;
            }

            state.LoadedState.AddOrUpdate(key, providerState, (k, v) => providerState);

            return (T?)Convert.ChangeType(providerState, typeof(T));
        }

        public async Task<HistoricalStateBase?> LoadStateAsync(RulesEngineState state, CancellationToken token)
        {
            //TODO: Protect against repeat loads and await the prior.
            if (await ShouldCalculateAsync(state, token))
            {
                var key = state.CurrentHistoricalStateKey ?? Id;

                // Use state already hydrated up front (e.g. by HydrateState in ProcessRulesAsync); only call adapter when not present.
                if (state.LoadedState.TryGetValue(key, out var existing))
                    return existing;

                //var loadedState = await state.RuleStateAdapter.EnqueueStateToLoadAsync<SimpleState>(tenantId: state.TenantId, rollupEntityId: state.LoyaltyAccountId, providerId: Id, eventType: state.EventModelId);

                ////Add the state to the cache.
                ////If it is not in state, new it up and add it to state.
                //var cache = state.LoadedState.GetOrAdd(key, loadedState ?? new SimpleState(Id, 0, 0M, null));
                //return cache;
                return state.LoadedState.GetOrAdd(key, _ => new SimpleState(Id, 0, 0M, null));
            }
            return null;
        }

        public async Task<bool> ShouldCalculateAsync(RulesEngineState state, CancellationToken token)
        {
            var tasks = new List<Task<bool>>();

            //Gating constraints are optional, if null no processing is required.
            if (CalculationGatingConstraint != null)
            {
                //While a slight hack for now, historical gating constraints may not
                //be loaded prior to being evaluated in ShouldCalcualte of other 
                //HistoricalBase rules. To alleviate this, we will load the state here.
                //We need to safe-guard against multiple loads being called and await the
                //first if in progress.
                if (CalculationGatingConstraint is HistoricalRule rule)
                {
                    await rule.HistoricalValueProvider.LoadStateAsync(state, token);
                }
                var gatingTask = CalculationGatingConstraint.Evaluate(state, token);
                tasks.Add(gatingTask);
            }

            //Temporal constraints are always required in a SimpleCalculationProvider
            //If null is encountered, throw an exception.
            if (TemporalConstraint == null)
            {
                throw new NullReferenceException("All IHistoricalValueProvider rules require a temporal constraint.");
            }


            var temporalTask = TemporalConstraint.Evaluate(state, token);
            tasks.Add(temporalTask);
            await Task.WhenAll(tasks);
            var result = tasks.All(x => x.Result);
            return result;
        }
    }
}
