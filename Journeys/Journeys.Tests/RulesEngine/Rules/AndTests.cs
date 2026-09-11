using Journeys.Core.JsonConverters;
using Journeys.Core.RulesEngine.Comparitors;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.RulesEngine.Comparitors.Enums;
using Journeys.Core.RulesEngine.Providers.Enums;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.RulesEngine.Rules.Composite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection.Metadata;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Journeys.Core.Services;
using Journeys.Tests.Stubs;
using Journeys.Core.Models;

namespace Journeys.Tests.RulesEngine.Rules
{
    public class AndTests
    {
        private readonly PointAccountType _escrowPointAccountType;
        private readonly PointAccountType _expPointAccountType;
        private readonly PointAccountType _spendablePointAccountType;

        public AndTests()
        {
            var cache = new StubPointAccountTypeCache();

            // Initialize point account types
            _escrowPointAccountType = TestDataFactory.GetEscrowPointAccount();
            _spendablePointAccountType = TestDataFactory.GetSpendablePointAccount();
            _expPointAccountType = TestDataFactory.GetExpPointAccount();

            // Cache the point account types before using them
            cache.CachePointAccountType(_escrowPointAccountType.TenantId, _escrowPointAccountType);
            cache.CachePointAccountType(_spendablePointAccountType.TenantId, _spendablePointAccountType);
            cache.CachePointAccountType(_expPointAccountType.TenantId, _expPointAccountType);

            // Make sure expiration type is properly linked
            _spendablePointAccountType.ExpiresToPointAccountTypeId = _expPointAccountType.Id;
            _escrowPointAccountType.ExpiresToPointAccountTypeId = _expPointAccountType.Id;

        }

        [Fact]
        public async Task Sum()
        {
            var engineEvent = TestDataFactory.GetHydratedEnginePayload();
            var and = new AndRule();

            //The SUM of Order Items, no Tax Totals, is greater than or equal to 100
            var spendOver100 = new NumericPropertyRule();
            spendOver100.LeftProvider = new AggregateValueProvider(AggregateType.Sum, new PathValueProvider("event.items"), new PathValueProvider("event.noTaxTotal"));
            spendOver100.Evaluator = new NumericEvaluation(NumEvalType.GreaterThanOrEqual);
            spendOver100.RightProvider = new ConstantValueProvider(100.0M);
            and.Children.Add(spendOver100);

            //Sum of payments other than giftcards is greater than or equal to 100
            var validPaymentsOver100 = new NumericPropertyRule();
            validPaymentsOver100.LeftProvider = new AggregateValueProvider(AggregateType.Sum, new PathValueProvider("event.payments"), new PathValueProvider("event.amount"))
            {
                Constraint = new NotRule
                {
                    Children = new List<RuleBase>() 
                    {
                        new StringPropertyRule
                        {
                            LeftProvider = new PathValueProvider("event.type"),
                            Evaluator = new StringEvaluation(StringEvalType.InCollection),
                            RightProvider = new PathValueProvider("globals.excludedPaymentTypes")
                        }
                    }
                }
            };
            validPaymentsOver100.Evaluator = new NumericEvaluation(NumEvalType.GreaterThanOrEqual);
            validPaymentsOver100.RightProvider = new ConstantValueProvider(100.0M);
            and.Children.Add(validPaymentsOver100);

            var result = await and.Evaluate(engineEvent, CancellationToken.None);
            Assert.True(result);
        }

        [Fact]
        public async Task Complex()
        {
            try
            {
                var engineEvent = TestDataFactory.GetHydratedEnginePayload();
                var and = new AndRule();

                //The SUM of Order Items, no Tax Totals, is greater than or equal to 100
                var spendOver100 = new NumericPropertyRule();
                spendOver100.LeftProvider = new AggregateValueProvider(AggregateType.Sum, new PathValueProvider("event.items"), new PathValueProvider("event.noTaxTotal"));
                spendOver100.Evaluator = new NumericEvaluation(NumEvalType.GreaterThanOrEqual);
                spendOver100.RightProvider = new ConstantValueProvider(100.0M);
                and.Children.Add(spendOver100);

                //Sum of payments other than giftcards is greater than or equal to 100
                var validPaymentsOver100 = new NumericPropertyRule();
                validPaymentsOver100.LeftProvider = new AggregateValueProvider(AggregateType.Sum, new PathValueProvider("event.payments"), new PathValueProvider("event.amount"))
                {
                    Constraint = new NotRule
                    {
                        Children = new List<RuleBase>()
                    {
                        new StringPropertyRule
                        {
                            LeftProvider = new PathValueProvider("event.type"),
                            Evaluator = new StringEvaluation(StringEvalType.InCollection),
                            RightProvider = new PathValueProvider("globals.excludedPaymentTypes")
                        }
                    }
                    }
                };
                validPaymentsOver100.Evaluator = new NumericEvaluation(NumEvalType.GreaterThanOrEqual);
                validPaymentsOver100.RightProvider = new ConstantValueProvider(100.0M);
                and.Children.Add(validPaymentsOver100);


                var serializeOptions = new JsonSerializerOptions();

                serializeOptions.Converters.Add(new RuleBaseJsonConverter());

                var strAnd = JsonSerializer.Serialize(and, serializeOptions);


                //var output = JsonSerializer.Deserialize<AndRule>(strAnd, serializeOptions);


                var rehydAndRule = JsonSerializer.Deserialize<AndRule>(strAnd, serializeOptions);
                if (rehydAndRule is AndRule typedResult)
                {
                    var typedEval = await typedResult.Evaluate(engineEvent, CancellationToken.None);
                    Assert.True(typedEval);
                }


                var result = await and.Evaluate(engineEvent, CancellationToken.None);
                Assert.True(result);
            }
            catch (Exception ex)
            {
            }
        }

    }
    
}
