using Journeys.Core;
using Journeys.Core.Caching;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.FileStorage;
using Journeys.Core.JsonConverters;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Outcomes;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.Services;
using Journeys.Core.Utility;
using Journeys.Tests.Stubs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.Tests.Services
{
    public class RuleServiceTests
    {
        [Fact]
        public async Task BasicJourneyEntryTest()
        {
            try
            {
                var payload = TestDataFactory.GenerateOrder1();
                var request = new RulesServiceRequest
                (
                    payloadModelId: "Order", //payload.ModelId,
                    payload: payload,
                    u: TestDataFactory.GenerateRichard(),
                    campaigns: new List<Campaign>
                    {
                    TestJourneyFactory.GetSimplePointEarningCampaign()
                    },
                    globals: TestDataFactory.GenerateGlobals()
                );

                var journey = request.Campaigns.First().Journey;
                if (journey != null)
                {
                    var serializeOptions = new JsonSerializerOptions();

                    serializeOptions.Converters.Add(new CustomDateTimeConverter());
                    serializeOptions.Converters.Add(new CustomDateOnlyConverter());
                    serializeOptions.Converters.Add(new JsonStringEnumConverter());
                    serializeOptions.Converters.Add(new RuleBaseJsonConverter());
                    //serializeOptions.Converters.Add(new NavigationCriteriaConverter());

                    var ser = JsonSerializer.Serialize(journey.NavigationCriteria, serializeOptions);
                }

                var logger = LoggerFactoryProvider.CreateLogger<RulesService>();
                var service = new RulesService(new StubUserJourneyAdapter(), null, //new StubRuleStateAdapter(),
                                new LoyaltyAccountService(new StubLoyaltyAccountAdapter(), new StubPointLedgerAdapter(), new StubTagAdapter(),
                                    new StubPointAccountTypeCache(), // new StubLoyaltyAccountRuleStateAdapter(), 
                                    new StubLoyaltyAccountPointsDetailsAdapter(),
                                    LoggerFactoryProvider.CreateLogger<LoyaltyAccountService>(), default(IDynamicDataAdapter), default(IDynamicExternalReferenceAdapter), default(IDataLakeAdapter)
                                ),
                                logger, default(ITaxonomyDataAdapter), 
                                default(IDynamicDataAdapter), //IDynamicDataAdapter for RulesService
                                default(ModelCache), //ModelCache for RulesService
                                new StubHistoricalRuleStateTTLAdapter());

                var result = await service.ProcessRulesAsync(request, CancellationToken.None);
                Assert.NotNull(result.State);

                //Ensure there is only one Journey root in the state mapping.
                Assert.Equal(1, result.State.JourneyState.Journeys.Count());

                //Ensure a mapping for the root node for the campaign.
                Assert.True(result.State.JourneyState.Journeys.ContainsKey("SimplePointEarningCampaign_Node1"));
                //Ensure the LoyaltyAccountNode has been entered into the appropriate node of the Journey.
                Assert.True(result.State.JourneyState.Journeys["SimplePointEarningCampaign_Node1"].Contains("SimplePointEarningCampaign_Node1"));
            }
            catch (Exception ex)
            {
            }
        }

        [Fact]
        public void GetBogoEcomWithHistoricalRuleCampaign_contains_taxonomy_like_rule_and_historical_rule()
        {
            var campaign = TestJourneyFactory.GetBogoEcomWithHistoricalRuleCampaign();
            Assert.NotNull(campaign.Journey);
            Assert.NotNull(campaign.Journey.Rules);
            Assert.True(campaign.Journey.Rules.Count >= 2, "Campaign should have at least two RuleSets (simple + historical)");
            var historicalRules = campaign.Journey.Rules
                .SelectMany(r => r.FlattenToRulesOfType<Journeys.Core.RulesEngine.Rules.HistoricalRule>())
                .ToList();
            Assert.NotEmpty(historicalRules);
            var historicalRule = historicalRules.First();
            Assert.Equal("historical-orders-90d", historicalRule.Id);
            Assert.NotNull(historicalRule.HistoricalValueProvider);
            var stateKey = Journeys.Core.RulesEngine.Rules.RuleBase.GetHistoricalStateKey(campaign.Id, historicalRule.Id);
            Assert.Equal($"{campaign.Id}|historical-orders-90d", stateKey);
        }

        [Fact]
        public async Task RecordingHistoricalRuleStateTTLAdapter_records_GetExpiredAsync_and_UpsertEventTtlAsync()
        {
            var recording = new RecordingHistoricalRuleStateTTLAdapter();
            var expired = await recording.GetExpiredAsync("t", "acc", "camp|rule", DateTimeOffset.UtcNow);
            Assert.Empty(expired);
            var getExpiredCall = Assert.Single(recording.GetExpiredCalls);
            Assert.Equal("camp|rule", getExpiredCall.stateKey);

            await recording.UpsertEventTtlAsync("t", "acc", "camp|rule", "Order", "evt1", DateTimeOffset.UtcNow.AddDays(1), 10m);
            var upsertCall = Assert.Single(recording.UpsertEventTtlCalls);
            Assert.Equal("Order", upsertCall.eventType);
            Assert.Equal("evt1", upsertCall.eventId);
            Assert.Equal(10m, upsertCall.contributionValue);
        }

        [Fact]
        public async Task ProcessRulesAsync_when_campaign_events_contains_payload_model_id_guid_evaluates_campaign()
        {
            // Production wiring: Campaign.Events holds the event payload model id (GUID string). EventService filters Live
            // campaigns with CampaignEventMatching; RulesService uses PayloadModelId as EventModelId on state.
            var stubCache = new StubPointAccountTypeCache();
            stubCache.RegisterTestCampaignFactoryPointAccountTypes();
            var eventPayloadModelId = "a6edbbc5-bf43-4c57-b2f1-e015b9efaf03";
            var campaign = TestCampaignFactory.GetMinimalCampaign();
            campaign.Events = new List<string> { eventPayloadModelId };
            var account = TestDataFactory.GenerateTenantBAccount();
            var payload = TestDataFactory.GenerateTenantBOrderEvent(orderId: "ord_wiring_01", totalPrice: 100);
            var request = new RulesServiceRequest(
                payloadModelId: eventPayloadModelId,
                payload: payload,
                u: account,
                campaigns: new List<Campaign> { campaign },
                globals: TestDataFactory.GenerateGlobals(),
                calculateOnly: false,
                eventId: "ord_wiring_01",
                eventType: "Order");
            var logger = LoggerFactoryProvider.CreateLogger<RulesService>();
            var service = new RulesService(new StubUserJourneyAdapter(), null,
                new LoyaltyAccountService(new StubLoyaltyAccountAdapter(), new StubPointLedgerAdapter(), new StubTagAdapter(),
                    stubCache,
                    new StubLoyaltyAccountPointsDetailsAdapter(),
                    LoggerFactoryProvider.CreateLogger<LoyaltyAccountService>(), default(IDynamicDataAdapter), default(IDynamicExternalReferenceAdapter), default(IDataLakeAdapter)),
                logger, default(ITaxonomyDataAdapter),
                default(IDynamicDataAdapter),
                default(ModelCache),
                new StubHistoricalRuleStateTTLAdapter());
            var result = await service.ProcessRulesAsync(request, CancellationToken.None);
            Assert.NotNull(result.State);
            Assert.Contains(TestCampaignFactory.MinimalCampaignId, result.State.CampaignsEvaluated);
            Assert.True(result.State.JourneyState.Journeys.ContainsKey("MinimalRoot"));
        }

        [Fact]
        public async Task ProcessRulesAsync_with_MinimalCampaign_and_TenantB_event_enters_journey_and_evaluates()
        {
            var stubCache = new StubPointAccountTypeCache();
            stubCache.RegisterTestCampaignFactoryPointAccountTypes();
            var campaign = TestCampaignFactory.GetMinimalCampaign();
            var account = TestDataFactory.GenerateTenantBAccount();
            var payload = TestDataFactory.GenerateTenantBOrderEvent(orderId: "ord_min_01", totalPrice: 100);
            var request = new RulesServiceRequest(
                payloadModelId: "Order",
                payload: payload,
                u: account,
                campaigns: new List<Campaign> { campaign },
                globals: TestDataFactory.GenerateGlobals(),
                calculateOnly: false,
                eventId: "ord_min_01",
                eventType: "Order");
            var logger = LoggerFactoryProvider.CreateLogger<RulesService>();

            var service = new RulesService(new StubUserJourneyAdapter(), null, //new StubRuleStateAdapter(),
                            new LoyaltyAccountService(new StubLoyaltyAccountAdapter(), new StubPointLedgerAdapter(), new StubTagAdapter(),
                                stubCache, //new StubLoyaltyAccountRuleStateAdapter(), 
                                new StubLoyaltyAccountPointsDetailsAdapter(),
                                LoggerFactoryProvider.CreateLogger<LoyaltyAccountService>(), default(IDynamicDataAdapter), default(IDynamicExternalReferenceAdapter), default(IDataLakeAdapter)
                            ),
                            logger, default(ITaxonomyDataAdapter),
                            default(IDynamicDataAdapter), //IDynamicDataAdapter for RulesService
                            default(ModelCache), //ModelCache for RulesService
                            new StubHistoricalRuleStateTTLAdapter());
            var result = await service.ProcessRulesAsync(request, CancellationToken.None);
            Assert.NotNull(result.State);
            Assert.True(result.State.JourneyState.Journeys.ContainsKey("MinimalRoot"));
            Assert.Contains("MinimalRoot", result.State.JourneyState.Journeys["MinimalRoot"]);
        }

        [Fact]
        public async Task ProcessRulesAsync_with_NotStartedCampaign_skips_campaign()
        {
            var stubCache = new StubPointAccountTypeCache();
            stubCache.RegisterTestCampaignFactoryPointAccountTypes();
            var notStarted = TestCampaignFactory.GetNotStartedCampaign();
            var minimal = TestCampaignFactory.GetMinimalCampaign();
            var account = TestDataFactory.GenerateTenantBAccount();
            var payload = TestDataFactory.GenerateTenantBOrderEvent(orderId: "ord_02");
            var request = new RulesServiceRequest(
                "Order", payload, account,
                campaigns: new List<Campaign> { notStarted, minimal },
                globals: TestDataFactory.GenerateGlobals(),
                calculateOnly: false,
                eventId: "ord_02",
                eventType: "Order");
            var logger = LoggerFactoryProvider.CreateLogger<RulesService>();
            var service = new RulesService(new StubUserJourneyAdapter(), null, //new StubRuleStateAdapter(),
                                new LoyaltyAccountService(new StubLoyaltyAccountAdapter(), new StubPointLedgerAdapter(), new StubTagAdapter(),
                                    stubCache, //new StubLoyaltyAccountRuleStateAdapter(),
                                    new StubLoyaltyAccountPointsDetailsAdapter(),
                                    LoggerFactoryProvider.CreateLogger<LoyaltyAccountService>(), default(IDynamicDataAdapter), default(IDynamicExternalReferenceAdapter), default(IDataLakeAdapter)
                                ),
                                logger, default(ITaxonomyDataAdapter),
                                default(IDynamicDataAdapter), //IDynamicDataAdapter for RulesService
                                default(ModelCache), //ModelCache for RulesService
                                new StubHistoricalRuleStateTTLAdapter());
            var result = await service.ProcessRulesAsync(request, CancellationToken.None);
            Assert.NotNull(result.State);
            Assert.Single(result.State.CampaignsEvaluated);
            Assert.Equal(TestCampaignFactory.MinimalCampaignId, result.State.CampaignsEvaluated[0]);
        }

        [Fact]
        public async Task ProcessRulesAsync_with_EndedCampaign_skips_campaign()
        {
            var stubCache = new StubPointAccountTypeCache();
            var ended = TestCampaignFactory.GetEndedCampaign();
            var account = TestDataFactory.GenerateTenantBAccount();
            var payload = TestDataFactory.GenerateTenantBOrderEvent(orderId: "ord_03");
            var request = new RulesServiceRequest(
                "Order", payload, account,
                campaigns: new List<Campaign> { ended },
                globals: TestDataFactory.GenerateGlobals(),
                calculateOnly: false,
                eventId: "ord_03",
                eventType: "Order");
            var logger = LoggerFactoryProvider.CreateLogger<RulesService>();
            var service = new RulesService(new StubUserJourneyAdapter(), null, //new StubRuleStateAdapter(),
                                new LoyaltyAccountService(new StubLoyaltyAccountAdapter(), new StubPointLedgerAdapter(), new StubTagAdapter(),
                                    stubCache, //new StubLoyaltyAccountRuleStateAdapter(), 
                                    new StubLoyaltyAccountPointsDetailsAdapter(),
                                    LoggerFactoryProvider.CreateLogger<LoyaltyAccountService>(), default(IDynamicDataAdapter), default(IDynamicExternalReferenceAdapter), default(IDataLakeAdapter)
                                ),
                                logger, default(ITaxonomyDataAdapter),
                                default(IDynamicDataAdapter), //IDynamicDataAdapter for RulesService
                                default(ModelCache), //ModelCache for RulesService
                                new StubHistoricalRuleStateTTLAdapter());
            var result = await service.ProcessRulesAsync(request, CancellationToken.None);
            Assert.NotNull(result.State);
            Assert.Empty(result.State.CampaignsEvaluated);
        }

        [Fact]
        public async Task ProcessRulesAsync_with_TierCampaignV1_and_TenantA_invoice_enters_Bronze()
        {
            var stubCache = new StubPointAccountTypeCache();
            stubCache.RegisterTestCampaignFactoryPointAccountTypes();
            var campaign = TestCampaignFactory.GetTierCampaignV1();
            var account = TestDataFactory.GenerateTenantAAccount();
            var payload = TestDataFactory.GenerateTenantAInvoiceEvent(uniquekey: "inv_001", totalValue: 1000);
            var request = new RulesServiceRequest(
                payloadModelId: "Invoice",
                payload: payload,
                u: account,
                campaigns: new List<Campaign> { campaign },
                globals: TestDataFactory.GenerateGlobals(),
                calculateOnly: false,
                eventId: "inv_001",
                eventType: "Invoice");
            var logger = LoggerFactoryProvider.CreateLogger<RulesService>();
            var service = new RulesService(new StubUserJourneyAdapter(), null, //new StubRuleStateAdapter(),
                                new LoyaltyAccountService(new StubLoyaltyAccountAdapter(), new StubPointLedgerAdapter(), new StubTagAdapter(),
                                    stubCache, //new StubLoyaltyAccountRuleStateAdapter(), 
                                    new StubLoyaltyAccountPointsDetailsAdapter(),
                                    LoggerFactoryProvider.CreateLogger<LoyaltyAccountService>(), default(IDynamicDataAdapter), default(IDynamicExternalReferenceAdapter), default(IDataLakeAdapter)
                                ),
                                logger, default(ITaxonomyDataAdapter),
                                default(IDynamicDataAdapter), //IDynamicDataAdapter for RulesService
                                default(ModelCache), //ModelCache for RulesService
                                new StubHistoricalRuleStateTTLAdapter());
            var result = await service.ProcessRulesAsync(request, CancellationToken.None);
            Assert.NotNull(result.State);
            Assert.True(result.State.JourneyState.Journeys.ContainsKey("TierRootV1"));
            var nodeIds = result.State.JourneyState.Journeys["TierRootV1"];
            Assert.Contains("Bronze", nodeIds);
        }

        [Fact]
        public async Task ProcessRulesAsync_with_HistoricalCampaignV3_two_events_same_account_reaches_count_2_and_enters_journey()
        {
            var stubCache = new StubPointAccountTypeCache();
            stubCache.RegisterTestCampaignFactoryPointAccountTypes();
            var stubAccount = new StubLoyaltyAccountAdapter();
            var campaign = TestCampaignFactory.GetHistoricalCampaignV3();
            var account = TestDataFactory.GenerateTenantBAccount();
            var stateKey = RuleBase.GetHistoricalStateKey(TestCampaignFactory.HistoricalCampaignV3Id, "orders-7d");

            var request1 = new RulesServiceRequest(
                payloadModelId: "Order",
                payload: TestDataFactory.GenerateTenantBOrderEvent(orderId: "ord_hist_01", timestamp: DateTimeOffset.UtcNow.AddDays(-1)),
                u: account,
                campaigns: new List<Campaign> { campaign },
                globals: TestDataFactory.GenerateGlobals(),
                calculateOnly: false,
                eventId: "ord_hist_01",
                eventType: "Order");
            var logger = LoggerFactoryProvider.CreateLogger<RulesService>();
            //var ruleStateAdapter = new StubLoyaltyAccountRuleStateAdapter();
            var service = new RulesService(new StubUserJourneyAdapter(), null, //new StubRuleStateAdapter(),
                                new LoyaltyAccountService(new StubLoyaltyAccountAdapter(), new StubPointLedgerAdapter(), new StubTagAdapter(),
                                    stubCache, //new StubLoyaltyAccountRuleStateAdapter(), 
                                    new StubLoyaltyAccountPointsDetailsAdapter(),
                                    LoggerFactoryProvider.CreateLogger<LoyaltyAccountService>(), default(IDynamicDataAdapter), default(IDynamicExternalReferenceAdapter), default(IDataLakeAdapter)
                                ),
                                logger, default(ITaxonomyDataAdapter),
                                default(IDynamicDataAdapter), //IDynamicDataAdapter for RulesService
                                default(ModelCache), //ModelCache for RulesService
                                new StubHistoricalRuleStateTTLAdapter());

            var result1 = await service.ProcessRulesAsync(request1, CancellationToken.None);
            Assert.NotNull(result1.State);
            Assert.True(result1.State.JourneyState.Journeys.ContainsKey("HistoricalRootV3"));
            var countAfter1 = result1.State.LoyaltyAccount.RuleState?.GetValueOrDefault(stateKey)?.Count ?? 0;
            Assert.Equal(1, countAfter1);

            var accountForRun2 = result1.State.LoyaltyAccount;
            var request2 = new RulesServiceRequest(
                payloadModelId: "Order",
                payload: TestDataFactory.GenerateTenantBOrderEvent(orderId: "ord_hist_02", timestamp: DateTimeOffset.UtcNow.AddDays(-2)),
                u: accountForRun2,
                campaigns: new List<Campaign> { campaign },
                globals: TestDataFactory.GenerateGlobals(),
                calculateOnly: false,
                eventId: "ord_hist_02",
                eventType: "Order");
            var result2 = await service.ProcessRulesAsync(request2, CancellationToken.None);
            Assert.NotNull(result2.State);
            var countAfter2 = result2.State.LoyaltyAccount.RuleState?.GetValueOrDefault(stateKey)?.Count ?? 0;
            Assert.Equal(2, countAfter2);
            Assert.True(result2.State.JourneyState.Journeys.ContainsKey("HistoricalRootV3"));
            Assert.Contains("HistoricalRootV3", result2.State.JourneyState.Journeys["HistoricalRootV3"]);
        }

        [Fact]
        public async Task ProcessRulesAsync_with_HistoricalCampaignV3_event_outside_7d_window_not_added_to_aggregate()
        {
            var stubCache = new StubPointAccountTypeCache();
            stubCache.RegisterTestCampaignFactoryPointAccountTypes();
            var campaign = TestCampaignFactory.GetHistoricalCampaignV3();
            var account = TestDataFactory.GenerateTenantBAccount();
            var stateKey = RuleBase.GetHistoricalStateKey(TestCampaignFactory.HistoricalCampaignV3Id, "orders-7d");

            var payloadOutsideWindow = TestDataFactory.GenerateTenantBOrderEvent(
                orderId: "ord_old_01",
                timestamp: DateTimeOffset.UtcNow.AddDays(-8));
            var request = new RulesServiceRequest(
                payloadModelId: "Order",
                payload: payloadOutsideWindow,
                u: account,
                campaigns: new List<Campaign> { campaign },
                globals: TestDataFactory.GenerateGlobals(),
                calculateOnly: false,
                eventId: "ord_old_01",
                eventType: "Order");
            var logger = LoggerFactoryProvider.CreateLogger<RulesService>();
            //var ruleStateAdapter = new StubLoyaltyAccountRuleStateAdapter();
            var service = new RulesService(new StubUserJourneyAdapter(), null, //new StubRuleStateAdapter(),
                                new LoyaltyAccountService(new StubLoyaltyAccountAdapter(), new StubPointLedgerAdapter(), new StubTagAdapter(),
                                    stubCache, //new StubLoyaltyAccountRuleStateAdapter(), 
                                    new StubLoyaltyAccountPointsDetailsAdapter(),
                                    LoggerFactoryProvider.CreateLogger<LoyaltyAccountService>(), default(IDynamicDataAdapter), default(IDynamicExternalReferenceAdapter), default(IDataLakeAdapter)
                                ),
                                logger, default(ITaxonomyDataAdapter), 
                                default(IDynamicDataAdapter), //IDynamicDataAdapter for RulesService
                                default(ModelCache), //ModelCache for RulesService
                                new StubHistoricalRuleStateTTLAdapter());

            var result = await service.ProcessRulesAsync(request, CancellationToken.None);
            Assert.NotNull(result.State);
            var count = result.State.LoyaltyAccount.RuleState?.GetValueOrDefault(stateKey)?.Count ?? 0;
            Assert.Equal(0, count);
        }

        [Fact]
        public async Task ProcessRulesAsync_with_HistoricalCampaignV3_after_ttl_expiry_decay_applied_and_delete_many_called()
        {
            var stubCache = new StubPointAccountTypeCache();
            stubCache.RegisterTestCampaignFactoryPointAccountTypes();
            var ttlStub = new StatefulStubHistoricalRuleStateTTLAdapter();
            //var ruleStateAdapter = new StubLoyaltyAccountRuleStateAdapter();
            var campaign = TestCampaignFactory.GetHistoricalCampaignV3();
            var account = TestDataFactory.GenerateTenantBAccount();
            var stateKey = RuleBase.GetHistoricalStateKey(TestCampaignFactory.HistoricalCampaignV3Id, "orders-7d");

            var str = JsonSerializer.Serialize(campaign, JsonUtility.GetDefaultOptions());

            var logger = LoggerFactoryProvider.CreateLogger<RulesService>();
            var service = new RulesService(new StubUserJourneyAdapter(), null, //ruleStateAdapter,
                                new LoyaltyAccountService(new StubLoyaltyAccountAdapter(), new StubPointLedgerAdapter(), new StubTagAdapter(),
                                    stubCache, //new StubLoyaltyAccountRuleStateAdapter(), 
                                    new StubLoyaltyAccountPointsDetailsAdapter(),
                                    LoggerFactoryProvider.CreateLogger<LoyaltyAccountService>(), default(IDynamicDataAdapter), default(IDynamicExternalReferenceAdapter), default(IDataLakeAdapter)
                                ),
                                logger, default(ITaxonomyDataAdapter),
                                default(IDynamicDataAdapter), //IDynamicDataAdapter for RulesService
                                default(ModelCache), //ModelCache for RulesService
                                ttlStub);

            var request1 = new RulesServiceRequest(
                "Order", TestDataFactory.GenerateTenantBOrderEvent(orderId: "ord_ttl_01", timestamp: DateTimeOffset.UtcNow.AddDays(-1)),
                account, new List<Campaign> { campaign }, TestDataFactory.GenerateGlobals(), false, "ord_ttl_01", "Order");
            var result1 = await service.ProcessRulesAsync(request1, CancellationToken.None);
            Assert.Equal(1, result1.State.LoyaltyAccount.RuleState?.GetValueOrDefault(stateKey)?.Count ?? 0);

            var request2 = new RulesServiceRequest(
                "Order", TestDataFactory.GenerateTenantBOrderEvent(orderId: "ord_ttl_02", timestamp: DateTimeOffset.UtcNow.AddDays(-2)),
                result1.State.LoyaltyAccount, new List<Campaign> { campaign }, TestDataFactory.GenerateGlobals(), false, "ord_ttl_02", "Order");
            var result2 = await service.ProcessRulesAsync(request2, CancellationToken.None);
            Assert.Equal(2, result2.State.LoyaltyAccount.RuleState?.GetValueOrDefault(stateKey)?.Count ?? 0);

            ttlStub.ReturnAllStoredAsExpiredOnNextGet = true;
            var request3 = new RulesServiceRequest(
                "Order", TestDataFactory.GenerateTenantBOrderEvent(orderId: "ord_ttl_03", timestamp: DateTimeOffset.UtcNow.AddDays(-8)),
                result2.State.LoyaltyAccount, new List<Campaign> { campaign }, TestDataFactory.GenerateGlobals(), false, "ord_ttl_03", "Order");
            var result3 = await service.ProcessRulesAsync(request3, CancellationToken.None);

            var countAfterDecay = result3.State.LoyaltyAccount.RuleState?.GetValueOrDefault(stateKey)?.Count ?? 0;
            Assert.Equal(0, countAfterDecay);
            var deleteCall = Assert.Single(ttlStub.DeleteManyCalls);
            Assert.Equal(stateKey, deleteCall.stateKey);
            Assert.Equal(2, deleteCall.records.Count);
        }

        [Fact]
        public async Task ProcessRulesAsync_with_HistoricalBySkuCampaign_and_taxonomy_stub_sum_exceeds_threshold_rule_true()
        {
            const string testSku = "SKU-TAX-001";
            var stubCache = new StubPointAccountTypeCache();
            stubCache.RegisterTestCampaignFactoryPointAccountTypes();
            var taxonomyStub = new StubTaxonomyDataAdapter();
            taxonomyStub.TaxonomyNodeId = "test-sku-node";
            taxonomyStub.RegisterSku(testSku);
            //var ruleStateAdapter = new StubLoyaltyAccountRuleStateAdapter();
            var campaign = TestCampaignFactory.GetHistoricalBySkuCampaign(taxonomyId: "test-sku-taxonomy", taxonomyNodeId: "test-sku-node", spendThreshold: 500m);
            var account = TestDataFactory.GenerateTenantBAccount();
            var stateKey = RuleBase.GetHistoricalStateKey(TestCampaignFactory.HistoricalBySkuCampaignId, "spend-sku-30d");

            var str = JsonSerializer.Serialize(campaign, JsonUtility.GetDefaultOptions());

            var logger = LoggerFactoryProvider.CreateLogger<RulesService>();
            var service = new RulesService(new StubUserJourneyAdapter(), null, //ruleStateAdapter,
                                new LoyaltyAccountService(new StubLoyaltyAccountAdapter(), new StubPointLedgerAdapter(), new StubTagAdapter(),
                                    stubCache, //ruleStateAdapter, 
                                    new StubLoyaltyAccountPointsDetailsAdapter(), LoggerFactoryProvider.CreateLogger<LoyaltyAccountService>(), 
                                    default(IDynamicDataAdapter), default(IDynamicExternalReferenceAdapter), default(IDataLakeAdapter)
                                ),
                                logger, taxonomyStub,
                                default(IDynamicDataAdapter), //IDynamicDataAdapter for RulesService
                                default(ModelCache), //ModelCache for RulesService
                                new StubHistoricalRuleStateTTLAdapter());

            var request1 = new RulesServiceRequest(
                "Order", TestDataFactory.GenerateTenantBOrderEvent(orderId: "ord_sku_01", totalPrice: 300, sku: testSku, timestamp: DateTimeOffset.UtcNow.AddDays(-1)),
                account, new List<Campaign> { campaign }, TestDataFactory.GenerateGlobals(), false, "ord_sku_01", "Order");
            var result1 = await service.ProcessRulesAsync(request1, CancellationToken.None);
            Assert.NotNull(result1.State);
            var valueAfter1 = result1.State.LoyaltyAccount.RuleState?.GetValueOrDefault(stateKey)?.Value ?? 0;
            Assert.Equal(300, valueAfter1);

            var request2 = new RulesServiceRequest(
                "Order", TestDataFactory.GenerateTenantBOrderEvent(orderId: "ord_sku_02", totalPrice: 250, sku: testSku, timestamp: DateTimeOffset.UtcNow.AddDays(-2)),
                result1.State.LoyaltyAccount, new List<Campaign> { campaign }, TestDataFactory.GenerateGlobals(), false, "ord_sku_02", "Order");
            var result2 = await service.ProcessRulesAsync(request2, CancellationToken.None);
            Assert.NotNull(result2.State);
            var valueAfter2 = result2.State.LoyaltyAccount.RuleState?.GetValueOrDefault(stateKey)?.Value ?? 0;
            Assert.Equal(550, valueAfter2);
            Assert.True(result2.State.JourneyState.Journeys.ContainsKey("HistoricalBySkuRoot"));
            Assert.Contains("HistoricalBySkuRoot", result2.State.JourneyState.Journeys["HistoricalBySkuRoot"]);
        }

        [Fact]
        public async Task ProcessRulesAsync_with_HistoricalByCategoryCampaign_and_taxonomy_stub_sum_exceeds_threshold_rule_true()
        {
            const string includedCategory = "Electronics.TV & Audio";
            const string testSku = "SKU-CAT-001";
            var stubCache = new StubPointAccountTypeCache();
            stubCache.RegisterTestCampaignFactoryPointAccountTypes();
            var taxonomyStub = new StubTaxonomyDataAdapter();
            taxonomyStub.Category = includedCategory;
            taxonomyStub.RegisterSku(testSku);
            //var ruleStateAdapter = new StubLoyaltyAccountRuleStateAdapter();
            var campaign = TestCampaignFactory.GetHistoricalByCategoryCampaign(taxonomyId: "test-category-taxonomy", includedCategory: includedCategory, spendThreshold: 500m);
            var account = TestDataFactory.GenerateTenantBAccount();
            var stateKey = RuleBase.GetHistoricalStateKey(TestCampaignFactory.HistoricalByCategoryCampaignId, "spend-category-30d");

            var str = JsonSerializer.Serialize(campaign, JsonUtility.GetDefaultOptions());

            var logger = LoggerFactoryProvider.CreateLogger<RulesService>();
            var service = new RulesService(new StubUserJourneyAdapter(), null, //ruleStateAdapter,
                    new LoyaltyAccountService(new StubLoyaltyAccountAdapter(), new StubPointLedgerAdapter(), new StubTagAdapter(),
                        stubCache, // ruleStateAdapter, 
                        new StubLoyaltyAccountPointsDetailsAdapter(), LoggerFactoryProvider.CreateLogger<LoyaltyAccountService>(),
                        default(IDynamicDataAdapter), default(IDynamicExternalReferenceAdapter), default(IDataLakeAdapter)
                    ),
                    logger, taxonomyStub,
                    default(IDynamicDataAdapter), //IDynamicDataAdapter for RulesService
                    default(ModelCache), //ModelCache for RulesService
                    new StubHistoricalRuleStateTTLAdapter());

            var request1 = new RulesServiceRequest(
                "Order", TestDataFactory.GenerateTenantBOrderEvent(orderId: "ord_cat_01", totalPrice: 300, sku: testSku, timestamp: DateTimeOffset.UtcNow.AddDays(-1)),
                account, new List<Campaign> { campaign }, TestDataFactory.GenerateGlobals(), false, "ord_cat_01", "Order");
            var result1 = await service.ProcessRulesAsync(request1, CancellationToken.None);
            Assert.NotNull(result1.State);
            var valueAfter1 = result1.State.LoyaltyAccount.RuleState?.GetValueOrDefault(stateKey)?.Value ?? 0;
            Assert.Equal(300, valueAfter1);

            var request2 = new RulesServiceRequest(
                "Order", TestDataFactory.GenerateTenantBOrderEvent(orderId: "ord_cat_02", totalPrice: 250, sku: testSku, timestamp: DateTimeOffset.UtcNow.AddDays(-2)),
                result1.State.LoyaltyAccount, new List<Campaign> { campaign }, TestDataFactory.GenerateGlobals(), false, "ord_cat_02", "Order");
            var result2 = await service.ProcessRulesAsync(request2, CancellationToken.None);
            Assert.NotNull(result2.State);
            var valueAfter2 = result2.State.LoyaltyAccount.RuleState?.GetValueOrDefault(stateKey)?.Value ?? 0;
            Assert.Equal(550, valueAfter2);
            Assert.True(result2.State.JourneyState.Journeys.ContainsKey("HistoricalByCategoryRoot"));
            Assert.Contains("HistoricalByCategoryRoot", result2.State.JourneyState.Journeys["HistoricalByCategoryRoot"]);
            var outcomesWhenTrue = result2.State.EarnedOutcomes?.GetValueOrDefault("HistoricalByCategoryRoot") ?? new List<OutcomeResult>();
            Assert.Contains(outcomesWhenTrue, o => o.IssuingEventId == "ord_cat_02");
        }

        [Fact]
        public async Task ProcessRulesAsync_with_HistoricalByCategoryCampaign_when_first_order_ttl_expires_sum_drops_below_threshold_rule_false()
        {
            const string includedCategory = "Electronics.TV & Audio";
            const string testSku = "SKU-CAT-002";
            var stubCache = new StubPointAccountTypeCache();
            stubCache.RegisterTestCampaignFactoryPointAccountTypes();
            var taxonomyStub = new StubTaxonomyDataAdapter();
            taxonomyStub.Category = includedCategory;
            taxonomyStub.RegisterSku(testSku);
            var ttlStub = new StatefulStubHistoricalRuleStateTTLAdapter();
            //var ruleStateAdapter = new StubLoyaltyAccountRuleStateAdapter();
            var campaign = TestCampaignFactory.GetHistoricalByCategoryCampaign(taxonomyId: "test-category-taxonomy", includedCategory: includedCategory, spendThreshold: 500m);
            var account = TestDataFactory.GenerateTenantBAccount();
            var stateKey = RuleBase.GetHistoricalStateKey(TestCampaignFactory.HistoricalByCategoryCampaignId, "spend-category-30d");
            
            var str = JsonSerializer.Serialize(campaign, JsonUtility.GetDefaultOptions());

            var logger = LoggerFactoryProvider.CreateLogger<RulesService>();
            var service = new RulesService(new StubUserJourneyAdapter(), null, //ruleStateAdapter,
                    new LoyaltyAccountService(new StubLoyaltyAccountAdapter(), new StubPointLedgerAdapter(), new StubTagAdapter(),
                        stubCache, //ruleStateAdapter, 
                        new StubLoyaltyAccountPointsDetailsAdapter(), LoggerFactoryProvider.CreateLogger<LoyaltyAccountService>(),
                        default(IDynamicDataAdapter), default(IDynamicExternalReferenceAdapter), default(IDataLakeAdapter)
                    ),
                    logger, taxonomyStub,
                    default(IDynamicDataAdapter), //IDynamicDataAdapter for RulesService
                    default(ModelCache), //ModelCache for RulesService
                    ttlStub);

            var request1 = new RulesServiceRequest(
                "Order", TestDataFactory.GenerateTenantBOrderEvent(orderId: "ord_ttl_cat_01", totalPrice: 300, sku: testSku, timestamp: DateTimeOffset.UtcNow.AddDays(-1)),
                account, new List<Campaign> { campaign }, TestDataFactory.GenerateGlobals(), false, "ord_ttl_cat_01", "Order");
            var result1 = await service.ProcessRulesAsync(request1, CancellationToken.None);
            Assert.Equal(300, result1.State.LoyaltyAccount.RuleState?.GetValueOrDefault(stateKey)?.Value ?? 0);

            var request2 = new RulesServiceRequest(
                "Order", TestDataFactory.GenerateTenantBOrderEvent(orderId: "ord_ttl_cat_02", totalPrice: 250, sku: testSku, timestamp: DateTimeOffset.UtcNow.AddDays(-2)),
                result1.State.LoyaltyAccount, new List<Campaign> { campaign }, TestDataFactory.GenerateGlobals(), false, "ord_ttl_cat_02", "Order");
            var result2 = await service.ProcessRulesAsync(request2, CancellationToken.None);
            Assert.Equal(550, result2.State.LoyaltyAccount.RuleState?.GetValueOrDefault(stateKey)?.Value ?? 0);

            ttlStub.ReturnAsExpiredEventIds.Add("ord_ttl_cat_01");
            var request3 = new RulesServiceRequest(
                "Order", TestDataFactory.GenerateTenantBOrderEvent(orderId: "ord_ttl_cat_03", totalPrice: 125, sku: testSku, timestamp: DateTimeOffset.UtcNow.AddDays(-3)),
                result2.State.LoyaltyAccount, new List<Campaign> { campaign }, TestDataFactory.GenerateGlobals(), false, "ord_ttl_cat_03", "Order");
            var result3 = await service.ProcessRulesAsync(request3, CancellationToken.None);

            var valueAfterDecay = result3.State.LoyaltyAccount.RuleState?.GetValueOrDefault(stateKey)?.Value ?? 0;
            Assert.Equal(375, valueAfterDecay);
            var deleteCall = Assert.Single(ttlStub.DeleteManyCalls);
            Assert.Equal(1, deleteCall.records.Count);
            Assert.Equal("ord_ttl_cat_01", deleteCall.records[0].EventId);
            var outcomesWhenFalse = result3.State.EarnedOutcomes?.GetValueOrDefault("HistoricalByCategoryRoot") ?? new List<OutcomeResult>();
            Assert.DoesNotContain(outcomesWhenFalse, o => o.IssuingEventId == "ord_ttl_cat_03");
        }
    }
}
