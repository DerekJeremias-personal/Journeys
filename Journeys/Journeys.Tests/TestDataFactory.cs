

using Backend.Dto.Dynamic;
using Journeys.Core;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.FileStorage;
using Journeys.Core.JsonConverters;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine;
using Journeys.Core.RulesEngine.Comparitors;
using Journeys.Core.RulesEngine.Comparitors.Enums;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.RulesEngine.Journey.Enums;
using Journeys.Core.RulesEngine.Outcomes;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Providers.Enums;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.RulesEngine.Rules.Composite;
using Journeys.Core.Services;
using Journeys.Core.Utility;
using Journeys.Tests.Stubs;
using System.Data;
using System.IO;
using System.Text.Json;

namespace Journeys.Tests
{
    public static class TestDataFactory
    {
        public const string TENANT_ID = "Tenant1";

        public static RulesEngineState GetSecondTransactionPayload(RulesEngineState obj)
        {
            obj.Event = DynamicHelper.Import(GenerateOrder2());
            obj.EventType = "Journeys.Models.Order";
            obj.EventId = "202411201824";

            return obj;
        }
        public static RulesEngineState GetHydratedEnginePayload()
        {
            return GetProfileObject();
        }

        public static RulesEngineState GetProfileObject()
        {
            var state = new RulesEngineState( new List<Campaign>(), true);
            state.LoyaltyAccountId = "test@Journeys.com";
            state.TenantId = TENANT_ID;
            var order = GenerateOrder1();
            state.EventModelId = "NULL MODELID ON ORDER OBJECT";
            state.EventType = "Journeys.Models.Order";
            state.EventId = "202411191824";
            state.ImportDynamicModels(
                globals: GenerateGlobals(),
                account: GenerateRichard(),
                evt: order
            );
            state.LoyaltyAccount = GenerateRichard();
            state.LoyaltyAccountService = CreateService();
            return state;
        }

        public static PointAccountType GetExpPointAccount()
        {
            return new PointAccountType(
                null, // extAccountId
                "Active", // status
                "Expired Points", // name
                "Expiration", // pointSourceId
                "EXPIRED", // ledgerType
                730, // pointsLifespanDays
                null, // pointsLifespanEndDate
                null, // expiresToPointAccountTypeId
                false, // isSpendable
                "AwayFromZero", 0,
                TENANT_ID,
                "expired-1"
            );
        }

        public static PointAccountType GetEscrowPointAccount()
        {
            return new PointAccountType(
                null, // extAccountId
                "Active", // status
                "Escrow Points", // name
                "Escrow", // pointSourceId
                "ESCROW", // ledgerType
                30, // pointsLifespanDays
                null, // pointsLifespanEndDate
                null, // expiresToPointAccountTypeId - will be set in test
                false, // isSpendable
                "AwayFromZero", 0,
                TENANT_ID,
                "escrow-1"
            );
        }

        public static PointAccountType GetSpendablePointAccount()
        {
            return new PointAccountType(
                null, // extAccountId
                "Active", // status
                "Spendable Points", // name
                "Purchase", // pointSourceId
                "SPENDABLE", // ledgerType
                30, // pointsLifespanDays
                null, // pointsLifespanEndDate
                null, // expiresToPointAccountTypeId - will be set in test
                true, // isSpendable
                "AwayFromZero", 0,
                TENANT_ID,
                "spendable-1"
            );
        }

        public static Dictionary<string, object> GenerateGlobals()
        {
            return new Dictionary<string, object>
            {
                { "pointsMultiplier", 2.0 },
                { "excludedPaymentTypes", new List<string> { "giftcard" } }
            };
        }

        public static LoyaltyAccount GenerateRichard()
        {
            var u = new LoyaltyAccount(
                    extAccountId: "test@bishoplabs.com",
                    type: "account",
                    status: "Active",
                    lockLeaseKey: null,
                    lockLeaseExpiration: null,
                    tags: null,
                    journeys: null,
                    null,
                    tenantId: TENANT_ID,
                    id: "test@bishoplabs.com"
                );

            u.PointLedgers = GeneratePointLedgers();
            u.AccountDetails = new Dictionary<string, object> { ["isdisabled"] = false };

            return u;
        }

        public static List<PointLedger> GeneratePointLedgers()
        {
            return new List<PointLedger>
            {
                GenerateEscrowLedger(),
                GenerateExpLedger(),
                GenerateSpendableLedger(),
            };
        }

        public static PointLedger GenerateEscrowLedger()
        {
            var lst = new List<LedgerEntry>
                {
                    new LedgerEntry("deposit", Guid.NewGuid().ToString(), "evtType1", "evt1", null, 100,
                                null, 100, DateTime.UtcNow.AddDays(10), DateTime.UtcNow, null, null, null, null)
                };
            var pat = GetEscrowPointAccount();
            var ledger = new PointLedger("test@bishoplabs.com", pat.Id, 100, 100, null,
                lst
                .GroupBy(e => EventKeyUtility.ToEventKey(e.EventType, e.EventId))
                .ToDictionary(
                        group => group.Key,
                        group => group.Select(g => g).ToList()
                    ), TENANT_ID, "escrowledger");
            ledger.PointAccountType = pat;
            return ledger;
        }

        public static PointLedger GenerateExpLedger()
        {
            var pat = GetExpPointAccount();
            var ledger = new PointLedger("test@bishoplabs.com", pat.Id, 0, 0, null, null, TENANT_ID, "expledger");
            ledger.PointAccountType = pat;
            return ledger;
        }

        public static PointLedger GenerateSpendableLedger()
        {
            var lst = new List<LedgerEntry>
            {
                new LedgerEntry("deposit", Guid.NewGuid().ToString(), "evtType1", "evt1", null, 100,
                            null, 100, DateTime.UtcNow.AddHours(-1).AddDays(10), DateTime.UtcNow.AddHours(-1), null, null, null, null),
                new LedgerEntry("deposit", Guid.NewGuid().ToString(), "evtType1", "evt2", null, 100,
                            null, 100, DateTime.UtcNow.AddDays(10), DateTime.UtcNow, null, null, null, null)
            };
            var pat = GetSpendablePointAccount();
            var ledger = new PointLedger("test@bishoplabs.com", pat.Id, 200, 200, null,
                lst
                .GroupBy(e => EventKeyUtility.ToEventKey(e.EventType, e.EventId))
                .ToDictionary(
                        group => group.Key,
                        group => group.Select(g => g).ToList()
                    ), TENANT_ID, "spendableledger");
            ledger.PointAccountType = pat;
            return ledger;
        }

        public static object GenerateOrder2()
        {
            return new
            {
                ExtOrderId = "",
                Type = "outsidepatio",
                UserId = "test@bishoplabs.com",
                Applyoutcomes = true,
                SubTotal = 30,
                TaxAmount = 2.10m,
                TotalAmount = 32.10m,
                TransactionDate = DateTimeOffset.UtcNow,
                LocationId = "404Pumpkin",
                EventType = "Journeys.Models.Order",
                TenantId = TENANT_ID,
                Id = "202411191824",
                Items = new[]
                {
                    new
                    {
                        ExtItemId = "QRS-278",
                        UnitPrice = 1.0M,
                        Quantity = 10M,
                        NoTaxTotal = 10.0M,
                        LineTotal = 10.70M,
                    },
                    new
                    {
                        ExtItemId = "TUV-266",
                        UnitPrice = 5.0M,
                        Quantity = 4M,
                        NoTaxTotal = 20.0M,
                        LineTotal = 21.40M
                    }
                },
                ModelId = "Order"
                //payments: new List<Payment>
                //{
                //    new Payment(
                //        extPaymentId: Guid.NewGuid().ToString(),
                //        type: "credit",
                //        amount: 32.10,
                //        paymentName: null,
                //        description: null
                //    )
                //},
                //new List<Discount>(),
            };
        }
        public static object GenerateOrder1() 
        {
            return new
            {
                ExtOrderId = "12345",
                Type = "outsidepatio",
                UserId = "test@bishoplabs.com",
                Applyoutcomes = true,
                SubTotal = 150,
                TaxAmount = 1.0m,
                TotalAmount = 151.0m,
                TransactionDate = DateTimeOffset.UtcNow.AddDays(-3),
                LocationId = "123 hgjkghj",
                EventType = "Journeys.Models.Order",
                TenantId = TENANT_ID,
                Id = "202411191824",
                Items = new[]
                {
                    new
                    {
                        ExtItemId = "ABC-123",
                        UnitPrice = 100.0M,
                        Quantity = 1M,
                        NoTaxTotal = 100.0M,
                        LineTotal = 107.50M,
                    },
                    new
                    {
                        ExtItemId = "DEF-456",
                        UnitPrice = 50.0M,
                        Quantity = 1M,
                        NoTaxTotal = 50.0M,
                        LineTotal = 50.75M
                    }
                },
                Payments = new[] { new { Type = "credit", Amount = 151.0m } },
                ModelId = "Order"
            };
        }

        /// <summary>TenantA (dealer) account for tier/taxonomy/historical test campaigns.</summary>
        public static LoyaltyAccount GenerateTenantAAccount(string extAccountId = "test_dealer_01")
        {
            var u = new LoyaltyAccount(
                extAccountId: extAccountId,
                type: "dealer",
                status: "Active",
                lockLeaseKey: null,
                lockLeaseExpiration: null,
                tags: null,
                journeys: null,
                null,
                tenantId: TestCampaignFactory.TenantA,
                id: extAccountId);
            u.PointLedgers = new List<PointLedger>();
            u.AccountDetails = new Dictionary<string, object> { ["CustomerId"] = extAccountId, ["IsApproved"] = true, ["isdisabled"] = false };
            return u;
        }

        /// <summary>TenantA invoice event — paths: invoiceDate, uniquekey, items[].value.</summary>
        public static object GenerateTenantAInvoiceEvent(string uniquekey = "uniquekey_0041", decimal totalValue = 1000, DateTimeOffset? invoiceDate = null, string eventType = "Invoice")
        {
            var date = invoiceDate ?? DateTimeOffset.UtcNow;
            return new Dictionary<string, object>
            {
                ["sfoqId"] = "test_dealer_01",
                ["invoiceNumber"] = "inv-001",
                ["invoiceDate"] = date,
                ["uniquekey"] = uniquekey,
                ["eventtype"] = eventType,
                ["eventType"] = eventType,
                ["items"] = new[]
                {
                    new { ItemNumber = "ULGHT001", value = totalValue, quantity = 23.0, desc = "LIGHTING" }
                }
            };
        }

        /// <summary>TenantB (ecom) account for tier/taxonomy/historical test campaigns.</summary>
        public static LoyaltyAccount GenerateTenantBAccount(string extAccountId = "test_user_005")
        {
            var u = new LoyaltyAccount(
                extAccountId: extAccountId,
                type: "user",
                status: "Active",
                lockLeaseKey: null,
                lockLeaseExpiration: null,
                tags: null,
                journeys: null,
                null,
                tenantId: TestCampaignFactory.TenantB,
                id: extAccountId);
            u.PointLedgers = new List<PointLedger>();
            u.AccountDetails = new Dictionary<string, object> { ["profileid"] = extAccountId, ["isloyaltymember"] = true, ["isdisabled"] = false };
            return u;
        }

        /// <summary>TenantB order event — paths: timestamp, orderId, items[].price, items[].sku.</summary>
        public static object GenerateTenantBOrderEvent(string orderId = "wxyz_027", decimal totalPrice = 200, string sku = "CT-43695-47047", DateTimeOffset? timestamp = null, string eventType = "Order")
        {
            var ts = timestamp ?? DateTimeOffset.UtcNow;
            return new Dictionary<string, object>
            {
                ["orderId"] = orderId,
                ["profileId"] = "test_user_005",
                ["ordertotal"] = totalPrice,
                ["timestamp"] = ts,
                ["eventtype"] = eventType,
                ["eventType"] = eventType,
                ["items"] = new[]
                {
                    new { sku = sku, price = totalPrice, quantity = 1.0, description = "PLACEHOLDER" }
                }
            };
        }

        public static Campaign GenerateCampaign()
        {

            var serializeOptions = new JsonSerializerOptions();
            serializeOptions.Converters.Add(new RuleBaseJsonConverter());

            var pat = GetSpendablePointAccount();

            //var navTier1 = new Dictionary<NavigationType, INavigationCriteria>
            //{
            //    {
            //        NavigationType.Entry,
            //        new SimpleNavigationCriteria(NavigationType.Entry, new SimpleRule<decimal>
            //        {
            //            LeftProvider = new PointBalanceProvider(pat.Id),
            //            RightProvider = new ConstantValueProvider(1000.0m),
            //            Evaluator = new NumericEvaluation(NumEvalType.Equal)
            //        }, null)
            //    }
            //};
            var navTier1 = GenEntranceCriteria(0, 1000, pat.Id);
            var strNavTier1 = JsonSerializer.Serialize(navTier1, serializeOptions);

            var navTier2 = GenEntranceCriteria(1000, 5000, pat.Id);
            var strNavTier2 = JsonSerializer.Serialize(navTier2, serializeOptions);

            var navTier3 = GenEntranceCriteria(5000, 50000, pat.Id);
            var strNavTier3 = JsonSerializer.Serialize(navTier3, serializeOptions);

            var navTier4 = GenEntranceCriteria(50000, 250000, pat.Id);
            var strNavTier4 = JsonSerializer.Serialize(navTier4, serializeOptions);


            var ruleSetTier1 = new RuleSet
            {
                RuleTree = new SimpleRule<bool>
                {
                    LeftProvider = new ConstantValueProvider(true),
                    RightProvider = new ConstantValueProvider(true),
                    Evaluator = new NumericEvaluation(NumEvalType.Equal)
                },
                Outcomes = new List<OutcomeBase>
                {
                    new DepositPointsOutcome
                    {
                        AffectedPointAccountTypeIds = new List<string> { pat.Id },
                        EarnDateProvider = new PathValueProvider("event.transactionDate"),
                        EventIdProvider = new PathValueProvider("event.id"),
                        DollarAmountProvider = new AggregateValueProvider
                        {
                            AggregateType = AggregateType.Sum,
                            RowPropertyProvider = new PathValueProvider("event.noTaxTotal"),
                            RowProvider = new PathValueProvider("event.items"),
                            Constraint = null
                        },
                        PointsPerDollar = 1
                    }
                }
            };
            var strRuleTier1 = JsonSerializer.Serialize(ruleSetTier1.RuleTree, serializeOptions);
            var strOutcomesTier1 = JsonSerializer.Serialize(ruleSetTier1.Outcomes, serializeOptions);


            var ruleSetTier2 = new RuleSet
            {
                RuleTree = new SimpleRule<bool>
                {
                    LeftProvider = new ConstantValueProvider(true),
                    RightProvider = new ConstantValueProvider(true),
                    Evaluator = new NumericEvaluation(NumEvalType.Equal)
                },
                Outcomes = new List<OutcomeBase>
                {
                    new DepositPointsOutcome
                    {
                        AffectedPointAccountTypeIds = new List<string> { pat.Id },
                        EarnDateProvider = new PathValueProvider("event.transactionDate"),
                        EventIdProvider = new PathValueProvider("event.id"),
                        DollarAmountProvider = new AggregateValueProvider
                        {
                            AggregateType = AggregateType.Sum,
                            RowPropertyProvider = new PathValueProvider("event.noTaxTotal"),
                            RowProvider = new PathValueProvider("event.items"),
                            Constraint = null
                        },
                        PointsPerDollar = 1.5m
                    }
                }
            };
            var strRuleTier2 = JsonSerializer.Serialize(ruleSetTier2.RuleTree, serializeOptions);
            var strOutcomesTier2 = JsonSerializer.Serialize(ruleSetTier2.Outcomes, serializeOptions);


            var ruleSetTier3 = new RuleSet
            {
                RuleTree = new SimpleRule<bool>
                {
                    LeftProvider = new ConstantValueProvider(true),
                    RightProvider = new ConstantValueProvider(true),
                    Evaluator = new NumericEvaluation(NumEvalType.Equal)
                },
                Outcomes = new List<OutcomeBase>
                {
                    new DepositPointsOutcome
                    {
                        AffectedPointAccountTypeIds = new List<string> { pat.Id },
                        EarnDateProvider = new PathValueProvider("event.transactionDate"),
                        EventIdProvider = new PathValueProvider("event.id"),
                        DollarAmountProvider = new AggregateValueProvider
                        {
                            AggregateType = AggregateType.Sum,
                            RowPropertyProvider = new PathValueProvider("event.noTaxTotal"),
                            RowProvider = new PathValueProvider("event.items"),
                            Constraint = null
                        },
                        PointsPerDollar = 2
                    }
                }
            };
            var strRuleTier3 = JsonSerializer.Serialize(ruleSetTier3.RuleTree, serializeOptions);
            var strOutcomesTier3 = JsonSerializer.Serialize(ruleSetTier3.Outcomes, serializeOptions);


            var ruleSetTier4 = new RuleSet
            {
                RuleTree = new SimpleRule<bool>
                {
                    LeftProvider = new ConstantValueProvider(true),
                    RightProvider = new ConstantValueProvider(true),
                    Evaluator = new NumericEvaluation(NumEvalType.Equal)
                },
                Outcomes = new List<OutcomeBase>
                {
                    new DepositPointsOutcome
                    {
                        AffectedPointAccountTypeIds = new List<string> { pat.Id },
                        EarnDateProvider = new PathValueProvider("event.transactionDate"),
                        EventIdProvider = new PathValueProvider("event.id"),
                        DollarAmountProvider = new AggregateValueProvider
                        {
                            AggregateType = AggregateType.Sum,
                            RowPropertyProvider = new PathValueProvider("event.noTaxTotal"),
                            RowProvider = new PathValueProvider("event.items"),
                            Constraint = null
                        },
                        PointsPerDollar = 2.5m
                    }
                },
                RootRuleDiscriminator = "SimpleRule"
            };
            var strRuleTier4 = JsonSerializer.Serialize(ruleSetTier4.RuleTree, serializeOptions);
            var strOutcomesTier4 = JsonSerializer.Serialize(ruleSetTier4.Outcomes, serializeOptions);



            var campaign = new Campaign(null, "Active", "TiersCampaign", new List<string> { "evergreen" }, DateTime.UtcNow.AddDays(-1), null, null,
                                new JourneyNode("tier1")
                                {
                                    Id = "tier1",
                                    NavigationCriteria = navTier1,
                                    Rules = new List<RuleSet>
                                    {
                                        ruleSetTier1
                                    },
                                    RootNodeId = "tier1",
                                    Children = new List<JourneyNode>
                                    {
                                        new JourneyNode("tier2")
                                        {
                                            Id = "tier2",
                                            NavigationCriteria = navTier2,
                                            Rules = new List<RuleSet>
                                            {
                                                ruleSetTier2
                                            },
                                            RootNodeId = "tier1"
                                        },
                                        new JourneyNode("tier3")
                                        {
                                            Id = "tier3",
                                            NavigationCriteria = navTier3,
                                            Rules = new List<RuleSet>
                                            {
                                                ruleSetTier3
                                            },
                                            RootNodeId = "tier1"
                                        },
                                        new JourneyNode("tier4")
                                        {
                                            Id = "tier4",
                                            NavigationCriteria = navTier4,
                                            Rules = new List<RuleSet>
                                            {
                                                ruleSetTier4
                                            },
                                            RootNodeId = "tier1"
                                        }
                                    }
                                }, TENANT_ID, "TiersCampaign");

            return campaign;
        }

        private static Dictionary<NavigationType, INavigationCriteria> GenEntranceCriteria(decimal low, decimal high, string ptAcctId)
        {
            var andRule = new AndRule();

            var spendOverLow = new NumericPropertyRule();
            spendOverLow.LeftProvider = new PointBalanceProvider(ptAcctId);
            spendOverLow.Evaluator = new NumericEvaluation(NumEvalType.GreaterThanOrEqual);
            spendOverLow.RightProvider = new ConstantValueProvider(low);

            andRule.Children.Add(spendOverLow);

            var spendOverHigh = new NumericPropertyRule();
            spendOverHigh.LeftProvider = new PointBalanceProvider(ptAcctId);
            spendOverHigh.Evaluator = new NumericEvaluation(NumEvalType.LessThan);
            spendOverHigh.RightProvider = new ConstantValueProvider(high);

            andRule.Children.Add(spendOverHigh);

            var nav = new Dictionary<NavigationType, INavigationCriteria>
            {
                {
                    NavigationType.Entry,
                    new SimpleNavigationCriteria("simpleNav", NavigationType.Entry, andRule, null)
                }
            };

            return nav;
        }

        public static LoyaltyAccountService CreateService()
        {
            return new LoyaltyAccountService(
                new StubLoyaltyAccountAdapter(),
                new StubPointLedgerAdapter(),
                new StubTagAdapter(),
                new StubPointAccountTypeCache(),
                //new StubLoyaltyAccountRuleStateAdapter(),
                new StubLoyaltyAccountPointsDetailsAdapter(),
                LoggerFactoryProvider.CreateLogger<LoyaltyAccountService>(), //ILogger<LoyaltyAccountService> logger
                default(IDynamicDataAdapter), //IDynamicDataAdapter dynamicDataAdapter
                default(IDynamicExternalReferenceAdapter), //IDynamicExternalReferenceAdapter dynamicAdapter
                default(IDataLakeAdapter)
            );
        }
    }
}