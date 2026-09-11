using Journeys.Core.Models;
using Journeys.Core.RulesEngine.Comparitors;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Providers.Enums;
using Journeys.Core.RulesEngine.Providers.Historical;
using Journeys.Core.RulesEngine.Providers.Historical.State;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.RulesEngine.Comparitors.Enums;
using Journeys.Tests.Stubs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Journeys.Tests.RulesEngine.Rules
{
    public class HistoricalRuleTests
    {
        private static LoyaltyAccount CreateMinimalAccount()
        {
            return new LoyaltyAccount(
                extAccountId: "test@bishoplabs.com",
                type: "account",
                status: "Active",
                lockLeaseKey: null,
                lockLeaseExpiration: null,
                tags: null,
                journeys: null,
                knownExternalIds: null,
                tenantId: TestDataFactory.TENANT_ID,
                id: "test@bishoplabs.com");
        }

        private static RulesEngineState CreateState(
            string campaignId,
            LoyaltyAccount account = null,
            bool withEventInWindow = true)
        {
            var acct = account ?? CreateMinimalAccount();
            acct.RuleState ??= new Dictionary<string, HistoricalRuleState>();
            var campaigns = new List<Campaign> { TestJourneyFactory.GetBogoEcomWithHistoricalRuleCampaign() };
            var state = new RulesEngineState( campaigns, false, acct);
            state.TenantId = TestDataFactory.TENANT_ID;
            state.LoyaltyAccountId = acct.Id;
            state.CurrentCampaignId = campaignId;
            state.EventModelId = "Order";
            state.EventId = "202411191824";
            state.EventType = "Order";
            state.ImportDynamicModels(TestDataFactory.GenerateGlobals(), acct, withEventInWindow ? TestDataFactory.GenerateOrder1() : TestDataFactory.GenerateOrder2());
            state.LoyaltyAccount = acct;
            state.LoyaltyAccountService = null;
            return state;
        }

        private static HistoricalRule CreateCountRule(string ruleId, decimal threshold = 1m)
        {
            var provider = new SimpleCalculationProvider
            {
                Id = ruleId,
                AggregateType = AggregateType.Count,
                InstanceValueProvider = new ConstantValueProvider(1m),
                TemporalConstraint = new TemporalConstraintRule(
                    new PathValueProvider("event.transactionDate"),
                    new TemporalEvaluation(TemporalEvalType.After),
                    TimeSpan.FromDays(90))
            };
            return new HistoricalRule
            {
                Id = ruleId,
                AggregateType = AggregateType.Count,
                AggregationValueProvider = provider,
                HistoricalValueProvider = provider,
                RightProvider = new ConstantValueProvider(threshold)
            };
        }

        [Fact]
        public async Task Evaluate_returns_false_when_CurrentCampaignId_null()
        {
            var state = CreateState(campaignId: null!);
            state.CurrentCampaignId = null;
            var rule = CreateCountRule("r1");

            var result = await rule.Evaluate(state, default);

            Assert.False(result);
        }

        [Fact]
        public async Task Evaluate_returns_false_when_rule_Id_and_HistoricalValueProvider_Id_null()
        {
            var state = CreateState("campaign-1");
            var provider = new SimpleCalculationProvider
            {
                Id = null,
                AggregateType = AggregateType.Count,
                InstanceValueProvider = new ConstantValueProvider(1m),
                TemporalConstraint = new TemporalConstraintRule(
                    new PathValueProvider("event.transactionDate"),
                    new TemporalEvaluation(TemporalEvalType.After),
                    TimeSpan.FromDays(90))
            };
            var rule = new HistoricalRule
            {
                Id = null,
                HistoricalValueProvider = provider,
                AggregationValueProvider = provider,
                RightProvider = new ConstantValueProvider(1m)
            };

            var result = await rule.Evaluate(state, default);

            Assert.False(result);
        }

        [Fact]
        public async Task Evaluate_returns_false_when_IsApplicableConstraint_fails()
        {
            var state = CreateState("8fd39eb1-2130-4b5b-aeea-5dfe2fb67d3d");
            var rule = CreateCountRule("r1");
            rule.IsApplicableConstraint = new SimpleRule<bool>(
                new ConstantValueProvider(true),
                new ConstantValueProvider(false),
                new BoolEvaluation());

            var result = await rule.Evaluate(state, default);

            Assert.False(result);
        }

        [Fact]
        public async Task Evaluate_includes_event_meets_threshold_returns_true()
        {
            var campaignId = "8fd39eb1-2130-4b5b-aeea-5dfe2fb67d3d";
            var state = CreateState(campaignId);
            var rule = CreateCountRule("historical-orders-90d", threshold: 1m);
            // HydrateState would have run and seeded LoadedState for state key; for unit test we seed it so LoadStateAsync gets a base
            var stateKey = RuleBase.GetHistoricalStateKey(campaignId, rule.Id);
            state.LoadedState[stateKey] = new SimpleState(rule.Id, 0, 0m, null, new List<StateTTL>());

            var result = await rule.Evaluate(state, default);

            Assert.True(result);
        }

        [Fact]
        public async Task Evaluate_does_not_meet_threshold_returns_false()
        {
            var campaignId = "8fd39eb1-2130-4b5b-aeea-5dfe2fb67d3d";
            var state = CreateState(campaignId);
            var rule = CreateCountRule("historical-orders-90d", threshold: 10m);
            var stateKey = RuleBase.GetHistoricalStateKey(campaignId, rule.Id);
            state.LoadedState[stateKey] = new SimpleState(rule.Id, 0, 0m, null, new List<StateTTL>());

            var result = await rule.Evaluate(state, default);

            Assert.False(result);
        }

        [Fact]
        public async Task Evaluate_syncs_state_to_LoyaltyAccount_RuleState()
        {
            var campaignId = "8fd39eb1-2130-4b5b-aeea-5dfe2fb67d3d";
            var state = CreateState(campaignId);
            var rule = CreateCountRule("historical-orders-90d", threshold: 1m);
            var stateKey = RuleBase.GetHistoricalStateKey(campaignId, rule.Id);
            state.LoadedState[stateKey] = new SimpleState(rule.Id, 0, 0m, null, new List<StateTTL>());

            await rule.Evaluate(state, default);

            Assert.True(state.LoyaltyAccount.RuleState.ContainsKey(stateKey));
            Assert.Equal(1, state.LoyaltyAccount.RuleState[stateKey].Count);
        }

        [Fact]
        public async Task Evaluate_adds_to_ModifiedHistoricalStateKeys()
        {
            var campaignId = "8fd39eb1-2130-4b5b-aeea-5dfe2fb67d3d";
            var state = CreateState(campaignId);
            var rule = CreateCountRule("historical-orders-90d", threshold: 1m);
            var stateKey = RuleBase.GetHistoricalStateKey(campaignId, rule.Id);
            state.LoadedState[stateKey] = new SimpleState(rule.Id, 0, 0m, null, new List<StateTTL>());

            await rule.Evaluate(state, default);

            Assert.Contains(stateKey, state.ModifiedHistoricalStateKeys);
        }

        [Fact]
        public async Task Evaluate_sets_CurrentHistoricalStateKey()
        {
            var campaignId = "8fd39eb1-2130-4b5b-aeea-5dfe2fb67d3d";
            var state = CreateState(campaignId);
            var rule = CreateCountRule("historical-orders-90d", threshold: 1m);
            var stateKey = RuleBase.GetHistoricalStateKey(campaignId, rule.Id);
            state.LoadedState[stateKey] = new SimpleState(rule.Id, 0, 0m, null, new List<StateTTL>());

            await rule.Evaluate(state, default);

            Assert.Equal(stateKey, state.CurrentHistoricalStateKey);
        }
    }
}
