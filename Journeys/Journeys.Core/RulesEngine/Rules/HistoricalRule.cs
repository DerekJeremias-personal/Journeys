using Journeys.Core.Models;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Providers.Enums;
using Journeys.Core.RulesEngine.Providers.Historical;
using Journeys.Core.RulesEngine.Providers.Historical.State;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Journeys.Core.RulesEngine.Rules
{
    public class HistoricalRule : RuleBase
    {
        public override string Kind => RuleKindDiscriminators.HistoricalRule;

        public AggregateType AggregateType { get; set; }
        public IValueProvider? AggregationValueProvider { get; set; }
        public RuleBase? IsApplicableConstraint { get; set; }
        //public DatePropertyRule? TemporalConstraint { get; set; }
        public IHistoricalValueProvider HistoricalValueProvider { get; set; }
        public IValueProvider? RightProvider { get; set; }

        public override async Task<bool> Evaluate(RulesEngineState state, CancellationToken token)
        {
            var ruleId = Id ?? HistoricalValueProvider?.Id;
            if (string.IsNullOrEmpty(state.CurrentCampaignId) || string.IsNullOrEmpty(ruleId))
                return false;

            var stateKey = GetHistoricalStateKey(state.CurrentCampaignId, ruleId);
            state.CurrentHistoricalStateKey = stateKey;

            if (IsApplicableConstraint != null && !await IsApplicableConstraint.Evaluate(state, token))
                return false;

            await HistoricalValueProvider.LoadStateAsync(state, token);

            if (!await HistoricalValueProvider.ShouldCalculateAsync(state, token))
            {
                var current = GetCurrentAggregateValue(state, stateKey);
                return RightProvider == null || await Compare(current, state, token);
            }

            if (AggregationValueProvider == null)
                return false;

            var aggval = await AggregationValueProvider.GetValue<SimpleState>(state, token);

            SyncStateToAccount(state, stateKey);
            if (!state.ModifiedHistoricalStateKeys.Contains(stateKey))
                state.ModifiedHistoricalStateKeys.Add(stateKey);

            var aggregateValue = GetCurrentAggregateValue(state, stateKey);
            return RightProvider == null || await Compare(aggregateValue, state, token);
        }

        private decimal GetCurrentAggregateValue(RulesEngineState state, string stateKey)
        {
            if (!state.LoadedState.TryGetValue(stateKey, out var baseState) || baseState is not SimpleState simple)
                return 0;
            return AggregateType == AggregateType.Count ? simple.Count : simple.Value;
        }

        private async Task<bool> Compare(decimal left, RulesEngineState state, CancellationToken token)
        {
            if (RightProvider == null) return true;
            var right = await RightProvider.GetValue<decimal>(state, token);
            return left >= right;
        }

        private void SyncStateToAccount(RulesEngineState state, string stateKey)
        {
            if (state.LoyaltyAccount?.RuleState == null) return;
            if (!state.LoadedState.TryGetValue(stateKey, out var baseState) || baseState is not SimpleState simple) return;

            state.LoyaltyAccount.RuleState[stateKey] = new HistoricalRuleState
            {
                Count = simple.Count,
                Value = simple.Value,
                FirstOccurrence = simple.FirstOccurrence
            };
        }
    }
}
