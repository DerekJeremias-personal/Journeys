using Journeys.Core.RulesEngine.Comparitors;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Providers.Historical;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.RulesEngine.Comparitors.Enums;
using Journeys.Core.RulesEngine.Providers.Enums;
using Journeys.Core.RulesEngine.Providers.Historical.State;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Journeys.Core.Models;
using Journeys.Tests.Stubs;
using Journeys.Core.Utility;

namespace Journeys.Tests.RulesEngine.Providers
{
    public class SimpleCalculationProviderTests
    {
        private readonly PointAccountType _escrowPointAccountType;
        private readonly PointAccountType _expPointAccountType;
        private readonly PointAccountType _spendablePointAccountType;

        public SimpleCalculationProviderTests()
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
            var provider = new SimpleCalculationProvider();
            provider.InstanceValueProvider = new AggregateValueProvider(AggregateType.Sum, new PathValueProvider("event.items"), new PathValueProvider("event.noTaxTotal"));
            provider.AggregateType = AggregateType.Sum;
            provider.TemporalConstraint = new TemporalConstraintRule(new PathValueProvider("event.transactionDate"), new TemporalEvaluation(TemporalEvalType.After), TimeSpan.FromDays(5));
            var logger = LoggerFactoryProvider.CreateLogger<SimpleCalculationProvider>();
            var json = JsonUtility.Serialize(provider, logger);
            var startStateTask = provider.LoadStateAsync(engineEvent, CancellationToken.None);
            //await engineEvent.RuleStateAdapter.LoadEnqueuedStatesAsync(engineEvent.TenantId, engineEvent.LoyaltyAccountId);
            var startState = await startStateTask;
            var getValueState = await provider.GetValue<SimpleState>(engineEvent, CancellationToken.None);
            Assert.True(engineEvent.LoadedState.ContainsKey(provider.Id));
            DateTimeOffset? firstOccurrence = null;
            if (engineEvent.LoadedState[provider.Id] is SimpleState smplState)
            {
                firstOccurrence = await provider.TemporalConstraint.TimeOfOccurrenceProvider.GetValue<DateTimeOffset>(engineEvent, CancellationToken.None);
                Assert.Equal(150M, smplState.Value);
                Assert.Equal(firstOccurrence, smplState.FirstOccurrence);
                Assert.NotNull(smplState.ContributorTTLs);
                Assert.Equal(1, smplState.ContributorTTLs.Count());
                Assert.True(smplState.ContributorTTLs.ContainsKey(EventKeyUtility.ToEventKey("Journeys.Models.Order", "202411191824")));
            }
            else
            {
                Assert.Fail("State is not of type SimpleState");
            }

            //This is a bit of a hack to not have to mock persistence entirely.
            //With persistence mocked fully, a new engine event with load state would hydrate DB mocked state
            var event2 = TestDataFactory.GetSecondTransactionPayload(engineEvent);
            var secondValueState = await provider.GetValue<SimpleState>(event2, CancellationToken.None);
            Assert.True(event2.LoadedState.ContainsKey(provider.Id));
            if (event2.LoadedState[provider.Id] is SimpleState smplState2)
            {
                Assert.Equal(180M, smplState2.Value);
                //Ensure that First Occurrence did not change.
                Assert.Equal(firstOccurrence, smplState2.FirstOccurrence);
                Assert.Equal(2, smplState2.ContributorTTLs.Count());
                Assert.True(smplState2.ContributorTTLs.ContainsKey(EventKeyUtility.ToEventKey("Journeys.Models.Order", "202411201824")));
            }
            else
            {
                Assert.Fail("State is not of type SimpleState");
            }
        }

        [Fact]
        public async Task TemporalExclusion()
        {
            var engineEvent = TestDataFactory.GetHydratedEnginePayload();
            var provider = new SimpleCalculationProvider();
            //SUM of Orders.Items Across the noTaxTotal property. 
            provider.InstanceValueProvider = new AggregateValueProvider(AggregateType.Sum, new PathValueProvider("event.items"), new PathValueProvider("event.noTaxTotal"));
            provider.AggregateType = AggregateType.Sum;
            //Where the purchase is within the last 1 Day
            provider.TemporalConstraint = new TemporalConstraintRule(new PathValueProvider("event.transactionDate"), new TemporalEvaluation(TemporalEvalType.After), TimeSpan.FromDays(1));

            var shouldCalculate = await provider.ShouldCalculateAsync(engineEvent, CancellationToken.None);
            Assert.False(shouldCalculate, "Expected should calculate to be false, encountered true");
            
            var startStateTask = provider.LoadStateAsync(engineEvent, CancellationToken.None);
            //await engineEvent.RuleStateAdapter.LoadEnqueuedStatesAsync(engineEvent.TenantId, engineEvent.LoyaltyAccountId);
            var startState = await startStateTask;

            //Because the Order is outside the TemporalConstraint, state should not be loaded.
            Assert.Null(startState);

            //Because the Order is outside the TemporalConstraint, it is not possible to return a 
            //value for the object.
            Assert.Null(await provider.GetValue<SimpleState>(engineEvent, CancellationToken.None));
        }
    }
}
