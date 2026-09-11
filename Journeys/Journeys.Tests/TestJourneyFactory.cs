using Journeys.Core.RulesEngine.Comparitors;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.RulesEngine.Rules.Composite;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.RulesEngine.Journey.Enums;
using Journeys.Core.RulesEngine.Outcomes;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.RulesEngine.Comparitors.Enums;
using Journeys.Core.RulesEngine.Providers.Enums;
using Journeys.Core.RulesEngine.Providers.Historical;
using Journeys.Core.Caching;
using System;

namespace Journeys.Tests
{
    public static class TestJourneyFactory
    {
        public static Campaign GetSimplePointEarningCampaign()
        {
            var j = new JourneyNode
                (
                    name: "SimplePointEarningCampaign_Node1",
                    //children: new List<JourneyNode>(),
                    rules: new List<RuleSet>(),
                    id: "SimplePointEarningCampaign_Node1",
                    rootNodeId: null
                );

            j.NavigationCriteria ??= new Dictionary<NavigationType, INavigationCriteria>();
            j.NavigationCriteria.Add(NavigationType.Entry, new SimpleNavigationCriteria()
                {
                    NavConstraint = new SimpleRule<bool>
                    (
                        leftProvider: new ConstantValueProvider(true),
                        rightProvider: new ConstantValueProvider(true),
                        evaluator: new BoolEvaluation()
                    ),
                    NavigationType = NavigationType.Entry,
                    Outcomes = new List<OutcomeBase>()
                }
            );

            //j.Navigations ??= new List<JourneyNavigation> ();
            //j.Navigations.Add(
            //    new JourneyNavigation(NavigationType.Entry, new List<INavigationCriteria>
            //        {
            //            new SimpleNavigationCriteria()
            //            {
            //                NavConstraint = new SimpleRule<bool>
            //                (
            //                    leftProvider: new ConstantValueProvider(true),
            //                    rightProvider: new ConstantValueProvider(true),
            //                    evaluator: new BoolEvaluation()
            //                ),
            //                NavigationType = NavigationType.Entry,
            //                Outcomes = new List<OutcomeBase>()
            //            }
            //        })
            //    );

            j.Rules.Add(new RuleSet
            {
                RuleTree = new AndRule()
                {
                    Children = new List<RuleBase>
                    {
                        new SimpleRule<bool>
                        (
                            leftProvider: new ConstantValueProvider(true),
                            rightProvider: new ConstantValueProvider(true),
                            evaluator: new BoolEvaluation()
                        )
                    }
                },
                Outcomes = new List<OutcomeBase>()
            });

            var c = new Campaign(
                extCampaignId: "TestJourneyFactory.GetSimplePointEarningCampaign",
                status: "active",
                name: "Simple Point Earning Campaign",
                events: new List<string> { "someevent" },
                startDate: DateTimeOffset.UtcNow.AddDays(-10),
                endDate: DateTimeOffset.UtcNow.AddDays(10),
                segments: new List<Segment>(),
                journey: j,
                tenantId: "mericantires",
                id: "TestJourneyFactory.GetSimplePointEarningCampaign"
            );

            return c;
        }

        public static Campaign GetTieredPointEarningCampaign()
        {
            var j = new JourneyNode
                (
                    name: "TieredPointEarningCampaign_Node1",
                    //children: new List<JourneyNode>(),
                    rules: new List<RuleSet>(),
                    id: "TieredPointEarningCampaign_Node1",
                    rootNodeId: null
                );

            j.NavigationCriteria ??= new Dictionary<NavigationType, INavigationCriteria>();
            j.NavigationCriteria.Add(NavigationType.Entry, new SimpleNavigationCriteria()
            {
                NavConstraint = new SimpleRule<bool>
                    (
                        leftProvider: new ConstantValueProvider(true),
                        rightProvider: new ConstantValueProvider(true),
                        evaluator: new BoolEvaluation()
                    ),
                NavigationType = NavigationType.Entry,
                Outcomes = new List<OutcomeBase>()
            });

            var rowProvider = new PathValueProvider("event.items");
            var rowPathProvider = new PathValueProvider("event.value");


            j.Rules.Add(new RuleSet
            {
                RuleTree = new AndRule()
                {
                    Children = new List<RuleBase>
                    {
                        new SimpleRule<bool>
                        (
                            leftProvider: new ConstantValueProvider(true),
                            rightProvider: new ConstantValueProvider(true),
                            evaluator: new BoolEvaluation()
                        )
                    }
                },
                Outcomes = new List<OutcomeBase>
                {
                    new DepositPointsOutcome
                    {
                        PointSourceAccountId = "",
                        AffectedPointAccountTypes = new List<PointAccountType> { SetSpendablePointAccount("mericantires", 30) },
                        EarnDateProvider = new PathValueProvider("event.invoiceDate"),
                        EventIdProvider = new PathValueProvider("evnet.uniqueKey"),
                        DollarAmountProvider = new AggregateValueProvider(AggregateType.Sum, rowProvider, rowPathProvider),
                        PointsPerDollar = 0.01m
                    }
                }
            });


            var c = new Campaign(
                extCampaignId: "TestJourneyFactory.GetSimplePointEarningCampaign",
                status: "active",
                name: "Simple Point Earning Campaign",
                events: new List<string> { "someevent" },
                startDate: DateTimeOffset.UtcNow.AddDays(-10),
                endDate: DateTimeOffset.UtcNow.AddDays(10),
                segments: new List<Segment>(),
                journey: j,
                tenantId: "mericantires",
                id: "TestJourneyFactory.GetSimplePointEarningCampaign"
            );

            return c;
        }

        /// <summary>
        /// Campaign similar to Bogo Ecom: one RuleSet with a simple (always-true) rule and one RuleSet with a HistoricalRule
        /// (count orders in last 90 days >= 1). Used for historical rule unit tests.
        /// </summary>
        public static Campaign GetBogoEcomWithHistoricalRuleCampaign()
        {
            const string campaignId = "8fd39eb1-2130-4b5b-aeea-5dfe2fb67d3d";
            const string journeyRootId = "BogoEcom";

            var historicalProvider = new SimpleCalculationProvider
            {
                Id = "historical-orders-90d",
                AggregateType = AggregateType.Count,
                InstanceValueProvider = new ConstantValueProvider(1m),
                TemporalConstraint = new TemporalConstraintRule(
                    new PathValueProvider("event.transactionDate"),
                    new TemporalEvaluation(TemporalEvalType.After),
                    TimeSpan.FromDays(90))
            };

            var historicalRule = new HistoricalRule
            {
                Id = "historical-orders-90d",
                AggregateType = AggregateType.Count,
                AggregationValueProvider = historicalProvider,
                HistoricalValueProvider = historicalProvider,
                RightProvider = new ConstantValueProvider(1m)
            };

            var j = new JourneyNode(
                name: "Bogo Ecom",
                rules: new List<RuleSet>(),
                id: journeyRootId,
                rootNodeId: journeyRootId);

            j.NavigationCriteria ??= new Dictionary<NavigationType, INavigationCriteria>();
            j.NavigationCriteria.Add(NavigationType.Entry, new SimpleNavigationCriteria
            {
                NavConstraint = new SimpleRule<bool>(
                    new ConstantValueProvider(true),
                    new ConstantValueProvider(true),
                    new BoolEvaluation()),
                NavigationType = NavigationType.Entry,
                Outcomes = new List<OutcomeBase>()
            });

            // RuleSet 1: simple always-true (like a taxonomy rule placeholder)
            j.Rules.Add(new RuleSet
            {
                Name = "Bogo Ecom",
                RuleTree = new SimpleRule<bool>(
                    new ConstantValueProvider(true),
                    new ConstantValueProvider(true),
                    new BoolEvaluation()),
                Outcomes = new List<OutcomeBase>()
            });

            // RuleSet 2: historical rule (orders in last 90 days >= 1)
            j.Rules.Add(new RuleSet
            {
                Name = "Historical orders 90d",
                RuleTree = historicalRule,
                Outcomes = new List<OutcomeBase>()
            });

            return new Campaign(
                extCampaignId: "Bogo Ecom",
                status: "live",
                name: "Bogo",
                events: new List<string> { "a6edbbc5-bf43-4c57-b2f1-e015b9efaf03" },
                startDate: new DateTimeOffset(2025, 1, 1, 22, 30, 0, TimeSpan.Zero),
                endDate: null,
                segments: null,
                journey: j,
                tenantId: "mericantires",
                id: campaignId);
        }

        private static PointAccountType SetSpendablePointAccount(string tenantId, decimal expDays)
        {
            var ptacct = new PointAccountType("test", "Active", "TestPointAccountType", null,
                                        PointLedgerTypeStrings.SPENDABLE, expDays, null, PointLedgerTypeStrings.EXPIRED, //90, null, 
                                        true, "AwayFromZero", 0, tenantId, "pointacct1");
            PointAccountTypeCache.Instance.CachePointAccountType(tenantId, ptacct);
            return ptacct;
        }
    }
}