using Journeys.Core;
using Journeys.Core.Caching;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.FileStorage;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine;
using Journeys.Core.RulesEngine.Engine;
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
using Journeys.Core.Services;
using Journeys.Tests.Stubs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Journeys.Tests.Services
{
    public class HydrateCollectTests
    {
        private const string ChildHistoricalRuleId = "child-orders-7d";
        private const string NavHistoricalRuleId = "nav-orders-7d";
        private const string CompositeNavHistoricalRuleId = "nav-composite-orders-7d";
        private const string GrandchildHistoricalRuleId = "grandchild-orders-7d";
        private const string ChildTaxonomyId = "hydrate-child-taxonomy";
        private const string ChildTaxonomyNodeId = "hydrate-child-tax-node";
        private const string ChildTaxSku = "SKU-HYDRATE-CHILD";

        [Fact]
        public async Task Child_node_HistoricalRule_is_collected_and_second_event_applies_ttl_decay_and_RuleState_load()
        {
            var stubCache = new StubPointAccountTypeCache();
            stubCache.RegisterTestCampaignFactoryPointAccountTypes();
            var ttlStub = new StatefulStubHistoricalRuleStateTTLAdapter();
            const string campaignId = "campaign-hydrate-child-hist";
            var campaign = CreateLiveCampaign(
                campaignId,
                "HydrateChildRoot",
                rootRules: new List<RuleSet>(),
                children: new List<JourneyNode>
                {
                    CreateChildNode(
                        "HydrateChildEarn",
                        "HydrateChildRoot",
                        new List<RuleSet> { CreateHistoricalCountRuleSet(ChildHistoricalRuleId) })
                });
            var stateKey = RuleBase.GetHistoricalStateKey(campaignId, ChildHistoricalRuleId);
            var service = CreateRulesService(ttlStub);
            var account = TestDataFactory.GenerateTenantBAccount("hydrate_child_hist");

            var result1 = await service.ProcessRulesAsync(OrderRequest(campaign, account, "ord_child_ttl_01", DateTimeOffset.UtcNow.AddDays(-1)), CancellationToken.None);
            Assert.Equal(1, result1.State.LoyaltyAccount.RuleState?.GetValueOrDefault(stateKey)?.Count ?? 0);

            var result2 = await service.ProcessRulesAsync(OrderRequest(campaign, result1.State.LoyaltyAccount, "ord_child_ttl_02", DateTimeOffset.UtcNow.AddDays(-2)), CancellationToken.None);
            Assert.Equal(2, result2.State.LoyaltyAccount.RuleState?.GetValueOrDefault(stateKey)?.Count ?? 0);

            ttlStub.ReturnAllStoredAsExpiredOnNextGet = true;
            var result3 = await service.ProcessRulesAsync(OrderRequest(campaign, result2.State.LoyaltyAccount, "ord_child_ttl_03", DateTimeOffset.UtcNow.AddDays(-8)), CancellationToken.None);

            Assert.Equal(0, result3.State.LoyaltyAccount.RuleState?.GetValueOrDefault(stateKey)?.Count ?? 0);
            var deleteCall = Assert.Single(ttlStub.DeleteManyCalls);
            Assert.Equal(stateKey, deleteCall.stateKey);
            Assert.Equal(2, deleteCall.records.Count);
        }

        [Fact]
        public async Task Child_node_TaxonomicRule_is_collected_on_the_same_hydrate_path()
        {
            var stubCache = new StubPointAccountTypeCache();
            stubCache.RegisterTestCampaignFactoryPointAccountTypes();
            var taxonomyStub = new RecordingTaxonomyDataAdapter();
            taxonomyStub.TaxonomyNodeId = ChildTaxonomyNodeId;
            taxonomyStub.RegisterSku(ChildTaxSku);
            const string campaignId = "campaign-hydrate-child-tax";
            var campaign = CreateLiveCampaign(
                campaignId,
                "HydrateTaxRoot",
                rootRules: new List<RuleSet>(),
                children: new List<JourneyNode>
                {
                    CreateChildNode(
                        "HydrateTaxChild",
                        "HydrateTaxRoot",
                        new List<RuleSet>
                        {
                            new RuleSet
                            {
                                Name = "Child taxonomic",
                                RuleTree = CreateTaxonomicRule(),
                                Outcomes = new List<OutcomeBase>()
                            }
                        })
                });
            var service = CreateRulesService(new StubHistoricalRuleStateTTLAdapter(), taxonomyStub);
            var account = TestDataFactory.GenerateTenantBAccount("hydrate_child_tax");

            var result = await service.ProcessRulesAsync(
                OrderRequest(campaign, account, "ord_child_tax_01", DateTimeOffset.UtcNow.AddDays(-1), sku: ChildTaxSku),
                CancellationToken.None);

            Assert.NotNull(result.State);
            Assert.Contains(campaignId, result.State.CampaignsEvaluated);
            Assert.NotEmpty(taxonomyStub.GetManyCalls);
            Assert.True(result.State.Taxonomies != null && result.State.Taxonomies.ContainsKey(ChildTaxonomyId));
        }

        [Fact]
        public async Task Nested_NavConstraint_HistoricalRule_is_collected()
        {
            var recording = new RecordingHistoricalRuleStateTTLAdapter();
            const string campaignId = "campaign-hydrate-nav-hist";
            var campaign = CreateLiveCampaign(
                campaignId,
                "HydrateNavRoot",
                rootRules: new List<RuleSet>(),
                rootEntryConstraint: CreateHistoricalCountRule(NavHistoricalRuleId));
            var stateKey = RuleBase.GetHistoricalStateKey(campaignId, NavHistoricalRuleId);
            var service = CreateRulesService(recording);
            var account = TestDataFactory.GenerateTenantBAccount("hydrate_nav_hist");

            var result = await service.ProcessRulesAsync(OrderRequest(campaign, account, "ord_nav_01", DateTimeOffset.UtcNow.AddDays(-1)), CancellationToken.None);

            Assert.NotNull(result.State);
            Assert.Contains(recording.GetExpiredCalls, c => c.stateKey == stateKey);
        }

        [Fact]
        public async Task Empty_Rules_on_child_or_nav_does_not_throw()
        {
            const string campaignId = "campaign-hydrate-empty-rules";
            var emptyNav = new AndRule { Children = new List<RuleBase>() };
            var campaign = CreateLiveCampaign(
                campaignId,
                "HydrateEmptyRoot",
                rootRules: new List<RuleSet>(),
                rootEntryConstraint: emptyNav,
                children: new List<JourneyNode>
                {
                    CreateChildNode(
                        "HydrateEmptyChild",
                        "HydrateEmptyRoot",
                        new List<RuleSet>
                        {
                            new RuleSet { Name = "Empty child ruleset", RuleTree = null, Outcomes = new List<OutcomeBase>() }
                        })
                });
            campaign.Journey!.Children![0].Rules!.Add(null!);
            var service = CreateRulesService(new StubHistoricalRuleStateTTLAdapter());
            var account = TestDataFactory.GenerateTenantBAccount("hydrate_empty_rules");

            var result = await service.ProcessRulesAsync(OrderRequest(campaign, account, "ord_empty_01", DateTimeOffset.UtcNow.AddDays(-1)), CancellationToken.None);

            Assert.NotNull(result.State);
            Assert.Contains(campaignId, result.State.CampaignsEvaluated);
        }

        [Fact]
        public async Task Empty_collect_still_runs_hydrate_pipeline_and_upsert()
        {
            var accountAdapter = new StubLoyaltyAccountAdapter();
            var recording = new RecordingHistoricalRuleStateTTLAdapter();
            const string campaignId = "campaign-hydrate-empty-collect";
            var campaign = CreateLiveCampaign(
                campaignId,
                "HydrateEmptyCollectRoot",
                rootRules: new List<RuleSet>(),
                children: new List<JourneyNode>
                {
                    CreateChildNode("HydrateEmptyCollectChild", "HydrateEmptyCollectRoot", new List<RuleSet>())
                });
            var service = CreateRulesService(recording, accountAdapter: accountAdapter);
            var account = TestDataFactory.GenerateTenantBAccount("hydrate_empty_collect");

            var result = await service.ProcessRulesAsync(OrderRequest(campaign, account, "ord_empty_pipe_01", DateTimeOffset.UtcNow.AddDays(-1)), CancellationToken.None);

            Assert.NotNull(result.State);
            Assert.Contains(campaignId, result.State.CampaignsEvaluated);
            Assert.True(result.State.JourneyState.Journeys.ContainsKey("HydrateEmptyCollectRoot"));
            var persisted = await accountAdapter.FetchLoyaltyAccountAsync(TestCampaignFactory.TenantB, account.Id, "Active");
            Assert.NotNull(persisted);
        }

        [Fact]
        public async Task Root_only_historical_hydrate_still_works()
        {
            var stubCache = new StubPointAccountTypeCache();
            stubCache.RegisterTestCampaignFactoryPointAccountTypes();
            var recording = new RecordingHistoricalRuleStateTTLAdapter();
            var campaign = TestCampaignFactory.GetHistoricalCampaignV3();
            var stateKey = RuleBase.GetHistoricalStateKey(TestCampaignFactory.HistoricalCampaignV3Id, "orders-7d");
            var service = CreateRulesService(recording);
            var account = TestDataFactory.GenerateTenantBAccount("hydrate_root_only");

            var result = await service.ProcessRulesAsync(OrderRequest(campaign, account, "ord_root_01", DateTimeOffset.UtcNow.AddDays(-1)), CancellationToken.None);

            Assert.NotNull(result.State);
            Assert.Equal(1, result.State.LoyaltyAccount.RuleState?.GetValueOrDefault(stateKey)?.Count ?? 0);
            Assert.Contains(recording.GetExpiredCalls, c => c.stateKey == stateKey);
        }

        [Fact]
        public async Task Nested_composite_NavConstraint_is_walked()
        {
            var recording = new RecordingHistoricalRuleStateTTLAdapter();
            const string campaignId = "campaign-hydrate-nav-composite";
            var composite = new AndRule
            {
                Children = new List<RuleBase>
                {
                    new SimpleRule<bool>(new ConstantValueProvider(true), new ConstantValueProvider(true), new BoolEvaluation()),
                    CreateHistoricalCountRule(CompositeNavHistoricalRuleId)
                }
            };
            var campaign = CreateLiveCampaign(
                campaignId,
                "HydrateNavCompositeRoot",
                rootRules: new List<RuleSet>(),
                rootEntryConstraint: composite);
            var stateKey = RuleBase.GetHistoricalStateKey(campaignId, CompositeNavHistoricalRuleId);
            var service = CreateRulesService(recording);
            var account = TestDataFactory.GenerateTenantBAccount("hydrate_nav_composite");

            var result = await service.ProcessRulesAsync(OrderRequest(campaign, account, "ord_nav_comp_01", DateTimeOffset.UtcNow.AddDays(-1)), CancellationToken.None);

            Assert.NotNull(result.State);
            Assert.Contains(recording.GetExpiredCalls, c => c.stateKey == stateKey);
        }

        [Fact]
        public async Task Grandchild_earn_RuleSet_is_collected()
        {
            var recording = new RecordingHistoricalRuleStateTTLAdapter();
            const string campaignId = "campaign-hydrate-grandchild";
            var grandchild = CreateChildNode(
                "HydrateGrandchild",
                "HydrateGrandRoot",
                new List<RuleSet> { CreateHistoricalCountRuleSet(GrandchildHistoricalRuleId) });
            var child = CreateChildNode("HydrateMidChild", "HydrateGrandRoot", new List<RuleSet>());
            child.Children = new List<JourneyNode> { grandchild };
            var campaign = CreateLiveCampaign(
                campaignId,
                "HydrateGrandRoot",
                rootRules: new List<RuleSet>(),
                children: new List<JourneyNode> { child });
            var stateKey = RuleBase.GetHistoricalStateKey(campaignId, GrandchildHistoricalRuleId);
            var service = CreateRulesService(recording);
            var account = TestDataFactory.GenerateTenantBAccount("hydrate_grandchild");

            var result = await service.ProcessRulesAsync(OrderRequest(campaign, account, "ord_grand_01", DateTimeOffset.UtcNow.AddDays(-1)), CancellationToken.None);

            Assert.NotNull(result.State);
            Assert.Contains(recording.GetExpiredCalls, c => c.stateKey == stateKey);
        }

        private static RulesServiceRequest OrderRequest(
            Campaign campaign,
            LoyaltyAccount account,
            string orderId,
            DateTimeOffset timestamp,
            string sku = "CT-43695-47047")
        {
            return new RulesServiceRequest(
                payloadModelId: "Order",
                payload: TestDataFactory.GenerateTenantBOrderEvent(orderId: orderId, timestamp: timestamp, sku: sku),
                u: account,
                campaigns: new List<Campaign> { campaign },
                globals: TestDataFactory.GenerateGlobals(),
                calculateOnly: false,
                eventId: orderId,
                eventType: "Order");
        }

        private static RulesService CreateRulesService(
            IHistoricalRuleStateTTLAdapter ttlAdapter,
            ITaxonomyDataAdapter? taxonomyAdapter = null,
            StubLoyaltyAccountAdapter? accountAdapter = null)
        {
            var stubCache = new StubPointAccountTypeCache();
            stubCache.RegisterTestCampaignFactoryPointAccountTypes();
            var logger = LoggerFactoryProvider.CreateLogger<RulesService>();
            return new RulesService(
                new StubUserJourneyAdapter(),
                null,
                new LoyaltyAccountService(
                    accountAdapter ?? new StubLoyaltyAccountAdapter(),
                    new StubPointLedgerAdapter(),
                    new StubTagAdapter(),
                    stubCache,
                    new StubLoyaltyAccountPointsDetailsAdapter(),
                    LoggerFactoryProvider.CreateLogger<LoyaltyAccountService>(),
                    default(IDynamicDataAdapter),
                    default(IDynamicExternalReferenceAdapter),
                    default(IDataLakeAdapter)),
                logger,
                taxonomyAdapter ?? default(ITaxonomyDataAdapter),
                default(IDynamicDataAdapter),
                default(ModelCache),
                ttlAdapter);
        }

        private static Campaign CreateLiveCampaign(
            string campaignId,
            string rootId,
            List<RuleSet> rootRules,
            List<JourneyNode>? children = null,
            RuleBase? rootEntryConstraint = null)
        {
            var root = new JourneyNode(rootId, rules: rootRules, id: rootId, rootNodeId: rootId, navigation: null, children: children);
            root.NavigationCriteria = new Dictionary<NavigationType, INavigationCriteria>
            {
                [NavigationType.Entry] = new SimpleNavigationCriteria(
                    "Entry",
                    NavigationType.Entry,
                    rootEntryConstraint ?? new SimpleRule<bool>(
                        new ConstantValueProvider(true),
                        new ConstantValueProvider(true),
                        new BoolEvaluation()),
                    null)
            };
            return new Campaign(
                extCampaignId: campaignId,
                status: CampaignStatusStrings.Live,
                name: campaignId,
                events: new List<string> { "OrderEvent" },
                startDate: DateTimeOffset.UtcNow.AddDays(-30),
                endDate: null,
                segments: null,
                journey: root,
                tenantId: TestCampaignFactory.TenantB,
                id: campaignId);
        }

        private static JourneyNode CreateChildNode(string id, string rootId, List<RuleSet> rules)
        {
            var node = new JourneyNode(id, rules: rules, id: id, rootNodeId: rootId, navigation: null, children: null);
            node.NavigationCriteria = new Dictionary<NavigationType, INavigationCriteria>
            {
                [NavigationType.Entry] = new SimpleNavigationCriteria(
                    "Entry",
                    NavigationType.Entry,
                    new SimpleRule<bool>(new ConstantValueProvider(true), new ConstantValueProvider(true), new BoolEvaluation()),
                    null)
            };
            return node;
        }

        private static RuleSet CreateHistoricalCountRuleSet(string ruleId)
        {
            return new RuleSet
            {
                Name = ruleId,
                RuleTree = CreateHistoricalCountRule(ruleId),
                Outcomes = new List<OutcomeBase>()
            };
        }

        private static HistoricalRule CreateHistoricalCountRule(string ruleId)
        {
            var provider = new SimpleCalculationProvider
            {
                Id = ruleId,
                AggregateType = AggregateType.Count,
                InstanceValueProvider = new ConstantValueProvider(1m),
                TemporalConstraint = new TemporalConstraintRule(
                    new PathValueProvider("event.timestamp"),
                    new TemporalEvaluation(TemporalEvalType.After),
                    TimeSpan.FromDays(7))
            };
            return new HistoricalRule
            {
                Id = ruleId,
                AggregateType = AggregateType.Count,
                AggregationValueProvider = provider,
                HistoricalValueProvider = provider,
                RightProvider = new ConstantValueProvider(1m)
            };
        }

        private static TaxonomicRule CreateTaxonomicRule()
        {
            return new TaxonomicRule
            {
                LeftProvider = new PathValueProvider("event.items"),
                RightProvider = new ConstantValueProvider("alwaystrue"),
                Evaluator = new StringEvaluation(StringEvalType.Equal),
                IncludedTreeNodes = new List<string>(),
                IncludedIds = new List<string> { ChildTaxonomyNodeId },
                ExcludedTreeNodes = new List<string>(),
                ExcludedIds = new List<string>(),
                TaxonomyType = "TreeRoot",
                TaxonomyId = ChildTaxonomyId,
                KeySymbolPath = "sku"
            };
        }

        private sealed class RecordingTaxonomyDataAdapter : StubTaxonomyDataAdapter
        {
            public List<(string tenantId, List<string> lookupKeys)> GetManyCalls { get; } = new();

            public override Task<List<Backend.Dto.Structures.Taxonomy.TaxonomyDto>> GetManyTaxonomiesByXidAsync(
                string tenantId,
                List<string> lookupKeys,
                CancellationToken token = default)
            {
                GetManyCalls.Add((tenantId, lookupKeys?.ToList() ?? new List<string>()));
                return base.GetManyTaxonomiesByXidAsync(tenantId, lookupKeys, token);
            }
        }
    }
}
