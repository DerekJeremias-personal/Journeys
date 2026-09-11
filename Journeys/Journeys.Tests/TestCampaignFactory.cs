using Journeys.Core.Caching;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine;
using Journeys.Core.RulesEngine.Comparitors;
using Journeys.Core.RulesEngine.Comparitors.Enums;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.RulesEngine.Journey.Enums;
using Journeys.Core.RulesEngine.Outcomes;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Providers.Enums;
using Journeys.Core.RulesEngine.Providers.Historical;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.RulesEngine.Rules.Composite;
using System;

namespace Journeys.Tests
{
    /// <summary>
    /// Builds test campaigns for unit tests: tier variants (TenantA/TenantB),
    /// taxonomy variants, historical variants, minimal, and date-filter campaigns.
    /// Uses generic tenant names (TenantA = dealer/invoice, TenantB = ecom/order).
    /// </summary>
    public static class TestCampaignFactory
    {
        public const string TenantA = "TenantA";
        public const string TenantB = "TenantB";

        public const string TqpPointAccountIdTenantA = "tqp-TenantA";
        public const string SpendablePointAccountIdTenantA = "spendable-TenantA";
        public const string TqpPointAccountIdTenantB = "tqp-TenantB";
        public const string SpendablePointAccountIdTenantB = "spendable-TenantB";

        public const string TierCampaignV1Id = "campaign-tier-v1";
        public const string TierCampaignV2Id = "campaign-tier-v2";
        public const string TaxonomyCampaignV1Id = "campaign-taxonomy-v1";
        public const string TaxonomyCampaignV2Id = "campaign-taxonomy-v2";
        public const string HistoricalCampaignV1Id = "campaign-historical-v1";
        public const string HistoricalCampaignV2Id = "campaign-historical-v2";
        public const string HistoricalCampaignV3Id = "campaign-historical-v3";
        /// <summary>Campaign: sum dollar amount for a specific SKU (taxonomy); rule true when sum >= threshold.</summary>
        public const string HistoricalBySkuCampaignId = "campaign-historical-by-sku";
        /// <summary>Campaign: sum dollar amount for items in an included category (taxonomy); rule true when sum >= threshold.</summary>
        public const string HistoricalByCategoryCampaignId = "campaign-historical-by-category";
        public const string MinimalCampaignId = "campaign-minimal";
        public const string NotStartedCampaignId = "campaign-not-started";
        public const string EndedCampaignId = "campaign-ended";

        private static void EnsurePointAccountsTenantA()
        {
            if (PointAccountTypeCache.Instance.GetPointAccountTypeAsync(TenantA, TqpPointAccountIdTenantA).GetAwaiter().GetResult() != null)
                return;
            var tqp = new PointAccountType(null, "Active", "TQP TenantA", null, "TQP", 365, null, null, false, "AwayFromZero", 0, TenantA, TqpPointAccountIdTenantA);
            var spend = new PointAccountType(null, "Active", "Spendable TenantA", null, PointLedgerTypeStrings.SPENDABLE, 30, null, null, true, "AwayFromZero", 0, TenantA, SpendablePointAccountIdTenantA);
            PointAccountTypeCache.Instance.CachePointAccountType(TenantA, tqp);
            PointAccountTypeCache.Instance.CachePointAccountType(TenantA, spend);
        }

        private static void EnsurePointAccountsTenantB()
        {
            if (PointAccountTypeCache.Instance.GetPointAccountTypeAsync(TenantB, TqpPointAccountIdTenantB).GetAwaiter().GetResult() != null)
                return;
            var tqp = new PointAccountType(null, "Active", "TQP TenantB", null, "TQP", 365, null, null, false, "AwayFromZero", 0, TenantB, TqpPointAccountIdTenantB);
            var spend = new PointAccountType(null, "Active", "Spendable TenantB", null, PointLedgerTypeStrings.SPENDABLE, 30, null, null, true, "AwayFromZero", 0, TenantB, SpendablePointAccountIdTenantB);
            PointAccountTypeCache.Instance.CachePointAccountType(TenantB, tqp);
            PointAccountTypeCache.Instance.CachePointAccountType(TenantB, spend);
        }

        /// <summary>Tier campaign variant 1: TenantA (dealer) — paths invoiceDate, uniquekey, items, value.</summary>
        public static Campaign GetTierCampaignV1()
        {
            EnsurePointAccountsTenantA();
            const string rootId = "TierRootV1";
            var root = new JourneyNode("Tier Root V1", rules: null, id: rootId, rootNodeId: rootId, navigation: null, children: null);
            root.NavigationCriteria = new System.Collections.Generic.Dictionary<NavigationType, INavigationCriteria>
            {
                [NavigationType.Entry] = new SimpleNavigationCriteria("Entry", NavigationType.Entry,
                    new NumericPropertyRule
                    {
                        LeftProvider = new PointBalanceProvider(TqpPointAccountIdTenantA),
                        RightProvider = new ConstantValueProvider(1_000_000m),
                        Evaluator = new NumericEvaluation(NumEvalType.LessThanOrEqual)
                    }, null)
            };

            var bronze = CreateTierNodeTenantA("Bronze", rootId, 0.166m, -100_000m, 44_999m);
            var silver = CreateTierNodeTenantA("Silver", rootId, 0.199m, 45_000m, 89_999m);
            var gold = CreateTierNodeTenantA("Gold", rootId, 0.239m, 90_000m, 1_000_000m);
            root.Children = new System.Collections.Generic.List<JourneyNode> { bronze, silver, gold };

            return new Campaign(
                extCampaignId: "TierV1",
                status: CampaignStatusStrings.Live,
                name: "Tier Campaign V1 (TenantA)",
                events: new System.Collections.Generic.List<string> { "InvoiceEvent" },
                startDate: DateTimeOffset.UtcNow.AddDays(-30),
                endDate: null,
                segments: null,
                journey: root,
                tenantId: TenantA,
                id: TierCampaignV1Id);
        }

        private static JourneyNode CreateTierNodeTenantA(string name, string rootId, decimal pointsPerDollarSpendable, decimal transitionLow, decimal transitionHigh)
        {
            var node = new JourneyNode(name, rules: new System.Collections.Generic.List<RuleSet>(), id: name, rootNodeId: rootId, navigation: null, children: null);
            node.Rules.Add(new RuleSet
            {
                Name = $"{name} Point Economy",
                RuleTree = new SimpleRule<bool>(new ConstantValueProvider(true), new ConstantValueProvider(true), new BoolEvaluation()),
                Outcomes = new System.Collections.Generic.List<OutcomeBase>
                {
                    new DepositPointsOutcome
                    {
                        AffectedPointAccountTypeIds = new System.Collections.Generic.List<string> { SpendablePointAccountIdTenantA },
                        EarnDateProvider = new PathValueProvider("event.invoiceDate"),
                        EventIdProvider = new PathValueProvider("event.uniquekey"),
                        DollarAmountProvider = new AggregateValueProvider(AggregateType.Sum, new PathValueProvider("event.items"), new PathValueProvider("event.value")),
                        PointsPerDollar = pointsPerDollarSpendable
                    },
                    new DepositPointsOutcome
                    {
                        AffectedPointAccountTypeIds = new System.Collections.Generic.List<string> { TqpPointAccountIdTenantA },
                        EarnDateProvider = new PathValueProvider("event.invoiceDate"),
                        EventIdProvider = new PathValueProvider("event.uniquekey"),
                        DollarAmountProvider = new AggregateValueProvider(AggregateType.Sum, new PathValueProvider("event.items"), new PathValueProvider("event.value")),
                        PointsPerDollar = 1.0m
                    }
                }
            });
            node.NavigationCriteria = new System.Collections.Generic.Dictionary<NavigationType, INavigationCriteria>();
            node.NavigationCriteria[NavigationType.Transition] = new SimpleNavigationCriteria("Transition", NavigationType.Transition,
                new AndRule
                {
                    Children = new System.Collections.Generic.List<RuleBase>
                    {
                        new NumericPropertyRule
                        {
                            LeftProvider = new PointBalanceProvider(TqpPointAccountIdTenantA),
                            RightProvider = new ConstantValueProvider(transitionLow),
                            Evaluator = new NumericEvaluation(NumEvalType.GreaterThanOrEqual)
                        },
                        new NumericPropertyRule
                        {
                            LeftProvider = new PointBalanceProvider(TqpPointAccountIdTenantA),
                            RightProvider = new ConstantValueProvider(transitionHigh),
                            Evaluator = new NumericEvaluation(NumEvalType.LessThanOrEqual)
                        }
                    }
                }, null);
            if (transitionLow >= 45_000)
                node.NavigationCriteria[NavigationType.Exit] = new SimpleNavigationCriteria("Exit", NavigationType.Exit,
                    new NumericPropertyRule
                    {
                        LeftProvider = new PointBalanceProvider(TqpPointAccountIdTenantA),
                        RightProvider = new ConstantValueProvider(transitionLow),
                        Evaluator = new NumericEvaluation(NumEvalType.LessThan)
                    }, null);
            return node;
        }

        /// <summary>Tier campaign variant 2: TenantB (ecom) — paths timestamp, orderId, items, price.</summary>
        public static Campaign GetTierCampaignV2()
        {
            EnsurePointAccountsTenantB();
            const string rootId = "TierRootV2";
            var root = new JourneyNode("Tier Root V2", rules: null, id: rootId, rootNodeId: rootId, navigation: null, children: null);
            root.NavigationCriteria = new System.Collections.Generic.Dictionary<NavigationType, INavigationCriteria>
            {
                [NavigationType.Entry] = new SimpleNavigationCriteria("Entry", NavigationType.Entry,
                    new NumericPropertyRule
                    {
                        LeftProvider = new PointBalanceProvider(TqpPointAccountIdTenantB),
                        RightProvider = new ConstantValueProvider(1_000_000m),
                        Evaluator = new NumericEvaluation(NumEvalType.LessThanOrEqual)
                    }, null)
            };

            var bronze = CreateTierNodeTenantB("Bronze", rootId, 0.166m, -100_000m, 44_999m);
            var silver = CreateTierNodeTenantB("Silver", rootId, 0.199m, 45_000m, 89_999m);
            var gold = CreateTierNodeTenantB("Gold", rootId, 0.239m, 90_000m, 1_000_000m);
            root.Children = new System.Collections.Generic.List<JourneyNode> { bronze, silver, gold };

            return new Campaign(
                extCampaignId: "TierV2",
                status: CampaignStatusStrings.Live,
                name: "Tier Campaign V2 (TenantB)",
                events: new System.Collections.Generic.List<string> { "OrderEvent" },
                startDate: DateTimeOffset.UtcNow.AddDays(-30),
                endDate: null,
                segments: null,
                journey: root,
                tenantId: TenantB,
                id: TierCampaignV2Id);
        }

        private static JourneyNode CreateTierNodeTenantB(string name, string rootId, decimal pointsPerDollarSpendable, decimal transitionLow, decimal transitionHigh)
        {
            var node = new JourneyNode(name, rules: new System.Collections.Generic.List<RuleSet>(), id: name, rootNodeId: rootId, navigation: null, children: null);
            node.Rules.Add(new RuleSet
            {
                Name = $"{name} Point Economy",
                RuleTree = new SimpleRule<bool>(new ConstantValueProvider(true), new ConstantValueProvider(true), new BoolEvaluation()),
                Outcomes = new System.Collections.Generic.List<OutcomeBase>
                {
                    new DepositPointsOutcome
                    {
                        AffectedPointAccountTypeIds = new System.Collections.Generic.List<string> { SpendablePointAccountIdTenantB },
                        EarnDateProvider = new PathValueProvider("event.timestamp"),
                        EventIdProvider = new PathValueProvider("event.orderId"),
                        DollarAmountProvider = new AggregateValueProvider(AggregateType.Sum, new PathValueProvider("event.items"), new PathValueProvider("event.price")),
                        PointsPerDollar = pointsPerDollarSpendable
                    },
                    new DepositPointsOutcome
                    {
                        AffectedPointAccountTypeIds = new System.Collections.Generic.List<string> { TqpPointAccountIdTenantB },
                        EarnDateProvider = new PathValueProvider("event.timestamp"),
                        EventIdProvider = new PathValueProvider("event.orderId"),
                        DollarAmountProvider = new AggregateValueProvider(AggregateType.Sum, new PathValueProvider("event.items"), new PathValueProvider("event.price")),
                        PointsPerDollar = 1.0m
                    }
                }
            });
            node.NavigationCriteria = new System.Collections.Generic.Dictionary<NavigationType, INavigationCriteria>();
            node.NavigationCriteria[NavigationType.Transition] = new SimpleNavigationCriteria("Transition", NavigationType.Transition,
                new AndRule
                {
                    Children = new System.Collections.Generic.List<RuleBase>
                    {
                        new NumericPropertyRule
                        {
                            LeftProvider = new PointBalanceProvider(TqpPointAccountIdTenantB),
                            RightProvider = new ConstantValueProvider(transitionLow),
                            Evaluator = new NumericEvaluation(NumEvalType.GreaterThanOrEqual)
                        },
                        new NumericPropertyRule
                        {
                            LeftProvider = new PointBalanceProvider(TqpPointAccountIdTenantB),
                            RightProvider = new ConstantValueProvider(transitionHigh),
                            Evaluator = new NumericEvaluation(NumEvalType.LessThanOrEqual)
                        }
                    }
                }, null);
            if (transitionLow >= 45_000)
                node.NavigationCriteria[NavigationType.Exit] = new SimpleNavigationCriteria("Exit", NavigationType.Exit,
                    new NumericPropertyRule
                    {
                        LeftProvider = new PointBalanceProvider(TqpPointAccountIdTenantB),
                        RightProvider = new ConstantValueProvider(transitionLow),
                        Evaluator = new NumericEvaluation(NumEvalType.LessThan)
                    }, null);
            return node;
        }

        /// <summary>Taxonomy campaign variant 1: inclusion only (IncludedTreeNodes); KeySymbolPath sku; TenantB.</summary>
        public static Campaign GetTaxonomyCampaignV1(string taxonomyId = "test-taxonomy-v1")
        {
            EnsurePointAccountsTenantB();
            const string rootId = "TaxonomyRootV1";
            var rule = new TaxonomicRule
            {
                LeftProvider = new PathValueProvider("event.items"),
                RightProvider = new ConstantValueProvider("alwaystrue"),
                Evaluator = new StringEvaluation(StringEvalType.Equal),
                IncludedTreeNodes = new System.Collections.Generic.List<string> { "Electronics.TV & Audio", "category/featured" },
                IncludedIds = new System.Collections.Generic.List<string>(),
                ExcludedTreeNodes = new System.Collections.Generic.List<string>(),
                ExcludedIds = new System.Collections.Generic.List<string>(),
                TaxonomyType = "TreeRoot",
                TaxonomyId = taxonomyId,
                KeySymbolPath = "sku"
            };
            var node = new JourneyNode("Taxonomy V1", rules: new System.Collections.Generic.List<RuleSet>(), id: rootId, rootNodeId: rootId, navigation: null, children: null);
            node.NavigationCriteria = new System.Collections.Generic.Dictionary<NavigationType, INavigationCriteria>
            {
                [NavigationType.Entry] = new SimpleNavigationCriteria("Entry", NavigationType.Entry,
                    new SimpleRule<bool>(new ConstantValueProvider(true), new ConstantValueProvider(true), new BoolEvaluation()), null)
            };
            node.Rules.Add(new RuleSet
            {
                Name = "Taxonomy Inclusion",
                RuleTree = rule,
                Outcomes = new System.Collections.Generic.List<OutcomeBase>
                {
                    new DepositPointsOutcome
                    {
                        AffectedPointAccountTypeIds = new System.Collections.Generic.List<string> { SpendablePointAccountIdTenantB },
                        EarnDateProvider = new PathValueProvider("event.timestamp"),
                        EventIdProvider = new PathValueProvider("event.orderId"),
                        DollarAmountProvider = new AggregateValueProvider(AggregateType.Sum, new PathValueProvider("event.items"), new PathValueProvider("event.price")),
                        PointsPerDollar = 1.0m
                    }
                }
            });

            return new Campaign(
                extCampaignId: "TaxonomyV1",
                status: CampaignStatusStrings.Live,
                name: "Taxonomy Campaign V1 (inclusion)",
                events: new System.Collections.Generic.List<string> { "OrderEvent" },
                startDate: DateTimeOffset.UtcNow.AddDays(-30),
                endDate: null,
                segments: null,
                journey: node,
                tenantId: TenantB,
                id: TaxonomyCampaignV1Id);
        }

        /// <summary>Taxonomy campaign variant 2: inclusion + exclusion (ExcludedTreeNodes); TenantB.</summary>
        public static Campaign GetTaxonomyCampaignV2(string taxonomyId = "test-taxonomy-v2")
        {
            EnsurePointAccountsTenantB();
            const string rootId = "TaxonomyRootV2";
            var rule = new TaxonomicRule
            {
                LeftProvider = new PathValueProvider("event.items"),
                RightProvider = new ConstantValueProvider("alwaystrue"),
                Evaluator = new StringEvaluation(StringEvalType.Equal),
                IncludedTreeNodes = new System.Collections.Generic.List<string> { "Electronics.TV & Audio" },
                IncludedIds = new System.Collections.Generic.List<string>(),
                ExcludedTreeNodes = new System.Collections.Generic.List<string> { "Clearance" },
                ExcludedIds = new System.Collections.Generic.List<string>(),
                TaxonomyType = "TreeRoot",
                TaxonomyId = taxonomyId,
                KeySymbolPath = "sku"
            };
            var node = new JourneyNode("Taxonomy V2", rules: new System.Collections.Generic.List<RuleSet>(), id: rootId, rootNodeId: rootId, navigation: null, children: null);
            node.NavigationCriteria = new System.Collections.Generic.Dictionary<NavigationType, INavigationCriteria>
            {
                [NavigationType.Entry] = new SimpleNavigationCriteria("Entry", NavigationType.Entry,
                    new SimpleRule<bool>(new ConstantValueProvider(true), new ConstantValueProvider(true), new BoolEvaluation()), null)
            };
            node.Rules.Add(new RuleSet
            {
                Name = "Taxonomy Inclusion Exclude",
                RuleTree = rule,
                Outcomes = new System.Collections.Generic.List<OutcomeBase>
                {
                    new DepositPointsOutcome
                    {
                        AffectedPointAccountTypeIds = new System.Collections.Generic.List<string> { SpendablePointAccountIdTenantB },
                        EarnDateProvider = new PathValueProvider("event.timestamp"),
                        EventIdProvider = new PathValueProvider("event.orderId"),
                        DollarAmountProvider = new AggregateValueProvider(AggregateType.Sum, new PathValueProvider("event.items"), new PathValueProvider("event.price")),
                        PointsPerDollar = 1.0m
                    }
                }
            });

            return new Campaign(
                extCampaignId: "TaxonomyV2",
                status: CampaignStatusStrings.Live,
                name: "Taxonomy Campaign V2 (inclusion + exclusion)",
                events: new System.Collections.Generic.List<string> { "OrderEvent" },
                startDate: DateTimeOffset.UtcNow.AddDays(-30),
                endDate: null,
                segments: null,
                journey: node,
                tenantId: TenantB,
                id: TaxonomyCampaignV2Id);
        }

        /// <summary>Historical campaign variant 1: count in last 90 days >= 1; TenantB.</summary>
        public static Campaign GetHistoricalCampaignV1()
        {
            EnsurePointAccountsTenantB();
            const string rootId = "HistoricalRootV1";
            var provider = new SimpleCalculationProvider
            {
                Id = "orders-90d",
                AggregateType = AggregateType.Count,
                InstanceValueProvider = new ConstantValueProvider(1m),
                TemporalConstraint = new TemporalConstraintRule(
                    new PathValueProvider("event.timestamp"),
                    new TemporalEvaluation(TemporalEvalType.After),
                    TimeSpan.FromDays(90))
            };
            var historicalRule = new HistoricalRule
            {
                Id = "orders-90d",
                AggregateType = AggregateType.Count,
                AggregationValueProvider = provider,
                HistoricalValueProvider = provider,
                RightProvider = new ConstantValueProvider(1m)
            };
            var node = new JourneyNode("Historical V1", rules: new System.Collections.Generic.List<RuleSet>(), id: rootId, rootNodeId: rootId, navigation: null, children: null);
            node.NavigationCriteria = new System.Collections.Generic.Dictionary<NavigationType, INavigationCriteria>
            {
                [NavigationType.Entry] = new SimpleNavigationCriteria("Entry", NavigationType.Entry,
                    new SimpleRule<bool>(new ConstantValueProvider(true), new ConstantValueProvider(true), new BoolEvaluation()), null)
            };
            node.Rules.Add(new RuleSet
            {
                Name = "Orders in 90d",
                RuleTree = historicalRule,
                Outcomes = new System.Collections.Generic.List<OutcomeBase>()
            });

            return new Campaign(
                extCampaignId: "HistoricalV1",
                status: CampaignStatusStrings.Live,
                name: "Historical Campaign V1 (count 90d)",
                events: new System.Collections.Generic.List<string> { "OrderEvent" },
                startDate: DateTimeOffset.UtcNow.AddDays(-30),
                endDate: null,
                segments: null,
                journey: node,
                tenantId: TenantB,
                id: HistoricalCampaignV1Id);
        }

        /// <summary>Historical campaign variant 2: sum in last 30 days >= threshold; TenantB.</summary>
        public static Campaign GetHistoricalCampaignV2(decimal spendThreshold = 500m)
        {
            EnsurePointAccountsTenantB();
            const string rootId = "HistoricalRootV2";
            var provider = new SimpleCalculationProvider
            {
                Id = "spend-30d",
                AggregateType = AggregateType.Sum,
                InstanceValueProvider = new AggregateValueProvider(AggregateType.Sum, new PathValueProvider("event.items"), new PathValueProvider("event.price")),
                TemporalConstraint = new TemporalConstraintRule(
                    new PathValueProvider("event.timestamp"),
                    new TemporalEvaluation(TemporalEvalType.After),
                    TimeSpan.FromDays(30))
            };
            var historicalRule = new HistoricalRule
            {
                Id = "spend-30d",
                AggregateType = AggregateType.Sum,
                AggregationValueProvider = provider,
                HistoricalValueProvider = provider,
                RightProvider = new ConstantValueProvider(spendThreshold)
            };
            var node = new JourneyNode("Historical V2", rules: new System.Collections.Generic.List<RuleSet>(), id: rootId, rootNodeId: rootId, navigation: null, children: null);
            node.NavigationCriteria = new System.Collections.Generic.Dictionary<NavigationType, INavigationCriteria>
            {
                [NavigationType.Entry] = new SimpleNavigationCriteria("Entry", NavigationType.Entry,
                    new SimpleRule<bool>(new ConstantValueProvider(true), new ConstantValueProvider(true), new BoolEvaluation()), null)
            };
            node.Rules.Add(new RuleSet
            {
                Name = "Spend in 30d",
                RuleTree = historicalRule,
                Outcomes = new System.Collections.Generic.List<OutcomeBase>()
            });

            return new Campaign(
                extCampaignId: "HistoricalV2",
                status: CampaignStatusStrings.Live,
                name: "Historical Campaign V2 (sum 30d)",
                events: new System.Collections.Generic.List<string> { "OrderEvent" },
                startDate: DateTimeOffset.UtcNow.AddDays(-30),
                endDate: null,
                segments: null,
                journey: node,
                tenantId: TenantB,
                id: HistoricalCampaignV2Id);
        }

        /// <summary>Historical campaign variant 3: count in last 7 days >= 2; TenantB.</summary>
        public static Campaign GetHistoricalCampaignV3()
        {
            EnsurePointAccountsTenantB();
            const string rootId = "HistoricalRootV3";
            var provider = new SimpleCalculationProvider
            {
                Id = "orders-7d",
                AggregateType = AggregateType.Count,
                InstanceValueProvider = new ConstantValueProvider(1m),
                TemporalConstraint = new TemporalConstraintRule(
                    new PathValueProvider("event.timestamp"),
                    new TemporalEvaluation(TemporalEvalType.After),
                    TimeSpan.FromDays(7))
            };
            var historicalRule = new HistoricalRule
            {
                Id = "orders-7d",
                AggregateType = AggregateType.Count,
                AggregationValueProvider = provider,
                HistoricalValueProvider = provider,
                RightProvider = new ConstantValueProvider(2m)
            };
            var node = new JourneyNode("Historical V3", rules: new System.Collections.Generic.List<RuleSet>(), id: rootId, rootNodeId: rootId, navigation: null, children: null);
            node.NavigationCriteria = new System.Collections.Generic.Dictionary<NavigationType, INavigationCriteria>
            {
                [NavigationType.Entry] = new SimpleNavigationCriteria("Entry", NavigationType.Entry,
                    new SimpleRule<bool>(new ConstantValueProvider(true), new ConstantValueProvider(true), new BoolEvaluation()), null)
            };
            node.Rules.Add(new RuleSet
            {
                Name = "Orders in 7d",
                RuleTree = historicalRule,
                Outcomes = new System.Collections.Generic.List<OutcomeBase>()
            });

            return new Campaign(
                extCampaignId: "HistoricalV3",
                status: CampaignStatusStrings.Live,
                name: "Historical Campaign V3 (count 7d)",
                events: new System.Collections.Generic.List<string> { "OrderEvent" },
                startDate: DateTimeOffset.UtcNow.AddDays(-30),
                endDate: null,
                segments: null,
                journey: node,
                tenantId: TenantB,
                id: HistoricalCampaignV3Id);
        }

        /// <summary>Historical + taxonomy: sum dollar amount for a specific SKU (via taxonomy); rule true when sum >= threshold. Use StubTaxonomyDataAdapter with same TaxonomyNodeId and RegisterSku(sku). AndRule ensures TaxonomicRule is flattened so HydrateState loads taxonomy.</summary>
        public static Campaign GetHistoricalBySkuCampaign(string taxonomyId = "test-sku-taxonomy", string taxonomyNodeId = "test-sku-node", decimal spendThreshold = 500m)
        {
            EnsurePointAccountsTenantB();
            const string rootId = "HistoricalBySkuRoot";
            var taxonomyRule = new TaxonomicRule
            {
                LeftProvider = new PathValueProvider("event.items"),
                RightProvider = new ConstantValueProvider("alwaystrue"),
                Evaluator = new StringEvaluation(StringEvalType.Equal),
                IncludedTreeNodes = new System.Collections.Generic.List<string>(),
                IncludedIds = new System.Collections.Generic.List<string> { taxonomyNodeId },
                ExcludedTreeNodes = new System.Collections.Generic.List<string>(),
                ExcludedIds = new System.Collections.Generic.List<string>(),
                TaxonomyType = "TreeRoot",
                TaxonomyId = taxonomyId,
                KeySymbolPath = "sku"
            };
            var provider = new SimpleCalculationProvider
            {
                Id = "spend-sku-30d",
                AggregateType = AggregateType.Sum,
                InstanceValueProvider = new AggregateValueProvider(AggregateType.Sum, new PathValueProvider("event.items"), new PathValueProvider("event.price")),
                TemporalConstraint = new TemporalConstraintRule(
                    new PathValueProvider("event.timestamp"),
                    new TemporalEvaluation(TemporalEvalType.After),
                    TimeSpan.FromDays(30))
            };
            var historicalRule = new HistoricalRule
            {
                Id = "spend-sku-30d",
                AggregateType = AggregateType.Sum,
                AggregationValueProvider = provider,
                HistoricalValueProvider = provider,
                RightProvider = new ConstantValueProvider(spendThreshold)
            };
            var andRule = new AndRule { Children = new System.Collections.Generic.List<RuleBase> { taxonomyRule, historicalRule } };
            var node = new JourneyNode("Historical By SKU", rules: new System.Collections.Generic.List<RuleSet>(), id: rootId, rootNodeId: rootId, navigation: null, children: null);
            node.NavigationCriteria = new System.Collections.Generic.Dictionary<NavigationType, INavigationCriteria>
            {
                [NavigationType.Entry] = new SimpleNavigationCriteria("Entry", NavigationType.Entry,
                    new SimpleRule<bool>(new ConstantValueProvider(true), new ConstantValueProvider(true), new BoolEvaluation()), null)
            };
            node.Rules.Add(new RuleSet
            {
                Name = "Spend for SKU in 30d",
                RuleTree = andRule,
                Outcomes = new System.Collections.Generic.List<OutcomeBase>()
            });

            return new Campaign(
                extCampaignId: "HistoricalBySku",
                status: CampaignStatusStrings.Live,
                name: "Historical by SKU (taxonomy + sum 30d)",
                events: new System.Collections.Generic.List<string> { "OrderEvent" },
                startDate: DateTimeOffset.UtcNow.AddDays(-30),
                endDate: null,
                segments: null,
                journey: node,
                tenantId: TenantB,
                id: HistoricalBySkuCampaignId);
        }

        /// <summary>Historical + taxonomy: sum dollar amount for items in an included category (IncludedTreeNodes); rule true when sum >= threshold. Use StubTaxonomyDataAdapter with Category set to a string that contains includedCategory (e.g. stub.Category = "Electronics.TV & Audio") and RegisterSku(sku) for the event items.</summary>
        public static Campaign GetHistoricalByCategoryCampaign(string taxonomyId = "test-category-taxonomy", string includedCategory = "Electronics.TV & Audio", decimal spendThreshold = 500m)
        {
            EnsurePointAccountsTenantB();
            const string rootId = "HistoricalByCategoryRoot";
            var taxonomyRule = new TaxonomicRule
            {
                LeftProvider = new PathValueProvider("event.items"),
                RightProvider = new ConstantValueProvider("alwaystrue"),
                Evaluator = new StringEvaluation(StringEvalType.Equal),
                IncludedTreeNodes = new System.Collections.Generic.List<string> { includedCategory },
                IncludedIds = new System.Collections.Generic.List<string>(),
                ExcludedTreeNodes = new System.Collections.Generic.List<string>(),
                ExcludedIds = new System.Collections.Generic.List<string>(),
                TaxonomyType = "TreeRoot",
                TaxonomyId = taxonomyId,
                KeySymbolPath = "sku"
            };
            var provider = new SimpleCalculationProvider
            {
                Id = "spend-category-30d",
                AggregateType = AggregateType.Sum,
                InstanceValueProvider = new AggregateValueProvider(AggregateType.Sum, new PathValueProvider("event.items"), new PathValueProvider("event.price")),
                TemporalConstraint = new TemporalConstraintRule(
                    new PathValueProvider("event.timestamp"),
                    new TemporalEvaluation(TemporalEvalType.After),
                    TimeSpan.FromDays(30))
            };
            var historicalRule = new HistoricalRule
            {
                Id = "spend-category-30d",
                AggregateType = AggregateType.Sum,
                AggregationValueProvider = provider,
                HistoricalValueProvider = provider,
                RightProvider = new ConstantValueProvider(spendThreshold)
            };
            var andRule = new AndRule { Children = new System.Collections.Generic.List<RuleBase> { taxonomyRule, historicalRule } };
            var node = new JourneyNode("Historical By Category", rules: new System.Collections.Generic.List<RuleSet>(), id: rootId, rootNodeId: rootId, navigation: null, children: null);
            node.NavigationCriteria = new System.Collections.Generic.Dictionary<NavigationType, INavigationCriteria>
            {
                [NavigationType.Entry] = new SimpleNavigationCriteria("Entry", NavigationType.Entry,
                    new SimpleRule<bool>(new ConstantValueProvider(true), new ConstantValueProvider(true), new BoolEvaluation()), null)
            };
            node.Rules.Add(new RuleSet
            {
                Id = "rule-set-category-30d",
                Name = "Spend for category in 30d",
                RuleTree = andRule,
                Outcomes = new System.Collections.Generic.List<OutcomeBase>
                {
                    new DepositPointsOutcome
                    {
                        Id = "outcome-category-threshold",
                        AffectedPointAccountTypeIds = new System.Collections.Generic.List<string> { SpendablePointAccountIdTenantB },
                        EarnDateProvider = new PathValueProvider("event.timestamp"),
                        EventIdProvider = new PathValueProvider("event.orderId"),
                        DollarAmountProvider = new AggregateValueProvider(AggregateType.Sum, new PathValueProvider("event.items"), new PathValueProvider("event.price")),
                        PointsPerDollar = 0.01m
                    }
                }
            });

            return new Campaign(
                extCampaignId: "HistoricalByCategory",
                status: CampaignStatusStrings.Live,
                name: "Historical by category (taxonomy + sum 30d)",
                events: new System.Collections.Generic.List<string> { "OrderEvent" },
                startDate: DateTimeOffset.UtcNow.AddDays(-30),
                endDate: null,
                segments: null,
                journey: node,
                tenantId: TenantB,
                id: HistoricalByCategoryCampaignId);
        }

        /// <summary>Minimal campaign: root only, entry always true, one DepositPointsOutcome; TenantB.</summary>
        public static Campaign GetMinimalCampaign()
        {
            EnsurePointAccountsTenantB();
            const string rootId = "MinimalRoot";
            var node = new JourneyNode("Minimal", rules: new System.Collections.Generic.List<RuleSet>(), id: rootId, rootNodeId: rootId, navigation: null, children: null);
            node.NavigationCriteria = new System.Collections.Generic.Dictionary<NavigationType, INavigationCriteria>
            {
                [NavigationType.Entry] = new SimpleNavigationCriteria("Entry", NavigationType.Entry,
                    new SimpleRule<bool>(new ConstantValueProvider(true), new ConstantValueProvider(true), new BoolEvaluation()), null)
            };
            node.Rules.Add(new RuleSet
            {
                Name = "Minimal Earn",
                RuleTree = new SimpleRule<bool>(new ConstantValueProvider(true), new ConstantValueProvider(true), new BoolEvaluation()),
                Outcomes = new System.Collections.Generic.List<OutcomeBase>
                {
                    new DepositPointsOutcome
                    {
                        AffectedPointAccountTypeIds = new System.Collections.Generic.List<string> { SpendablePointAccountIdTenantB },
                        EarnDateProvider = new PathValueProvider("event.timestamp"),
                        EventIdProvider = new PathValueProvider("event.orderId"),
                        DollarAmountProvider = new AggregateValueProvider(AggregateType.Sum, new PathValueProvider("event.items"), new PathValueProvider("event.price")),
                        PointsPerDollar = 0.01m
                    }
                }
            });

            return new Campaign(
                extCampaignId: "Minimal",
                status: CampaignStatusStrings.Live,
                name: "Minimal Campaign",
                events: new System.Collections.Generic.List<string> { "OrderEvent" },
                startDate: DateTimeOffset.UtcNow.AddDays(-30),
                endDate: null,
                segments: null,
                journey: node,
                tenantId: TenantB,
                id: MinimalCampaignId);
        }

        /// <summary>Campaign that has not started (StartDate > now); used for date-filter tests.</summary>
        public static Campaign GetNotStartedCampaign()
        {
            var c = GetMinimalCampaign();
            return new Campaign(
                c.ExtCampaignId, c.Status, c.Name, c.Events,
                DateTimeOffset.UtcNow.AddDays(1),
                c.EndDate, c.Segments, c.Journey,
                TenantB, NotStartedCampaignId, c.DeployedDate, c.ArchivedDate);
        }

        /// <summary>Campaign that has ended (EndDate < now); used for date-filter tests.</summary>
        public static Campaign GetEndedCampaign()
        {
            var c = GetMinimalCampaign();
            return new Campaign(
                c.ExtCampaignId, c.Status, c.Name, c.Events,
                c.StartDate,
                DateTimeOffset.UtcNow.AddDays(-1),
                c.Segments, c.Journey,
                TenantB, EndedCampaignId, c.DeployedDate, c.ArchivedDate);
        }
    }
}
