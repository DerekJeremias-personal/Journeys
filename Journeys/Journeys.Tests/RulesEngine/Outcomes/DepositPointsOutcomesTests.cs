using Journeys.Core;
using Journeys.Core.Caching;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.FileStorage;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine.Comparitors.Enums;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Outcomes;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Providers.Enums;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.Services;
using Journeys.Core.Services;
using Journeys.Tests.Stubs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Journeys.Tests.RulesEngine.Outcomes
{
    public class DepositPointsOutcomesTests
    {
        PointAccountType _escrowPointAccountType;
        PointAccountType _expPointAccountType;
        PointAccountType _spendablePointAccountType;

        StubPointAccountTypeCache _cache;

        public DepositPointsOutcomesTests()
        {
            _cache = new StubPointAccountTypeCache();

            _escrowPointAccountType = TestDataFactory.GetEscrowPointAccount();
            _spendablePointAccountType = TestDataFactory.GetSpendablePointAccount();
            _expPointAccountType = TestDataFactory.GetExpPointAccount();

            // Make sure expiration type is properly linked
            _spendablePointAccountType.ExpiresToPointAccountTypeId = _expPointAccountType.Id;
            _escrowPointAccountType.ExpiresToPointAccountTypeId = _expPointAccountType.Id;

            _cache.CachePointAccountType(_escrowPointAccountType.TenantId, _escrowPointAccountType);
            _cache.CachePointAccountType(_spendablePointAccountType.TenantId, _spendablePointAccountType);
            _cache.CachePointAccountType(_expPointAccountType.TenantId, _expPointAccountType);
        }

        [Fact]
        public async Task CalculateOutcomeAsync_WithValidSumAggregation_ReturnsCorrectPoints()
        {
            // Arrange
            var profile = TestDataFactory.GetProfileObject();
            var outcome = CreateBaseOutcome(
                tenantId: profile.TenantId,
                aggregateType: AggregateType.Sum,
                constraint: null
            );

            // Act
            var result = await outcome.CalculateOutcomeAsync(profile, default(CancellationToken));

            // Assert
            Assert.NotNull(result);
            var depositOutcome = (DepositPointsOutcome)result.IssuingOutcome;
            Assert.Equal(150, depositOutcome.AwardedPoints);
        }

        [Fact]
        public async Task CalculateOutcomeAsync_WhenDollarAmountProviderNull_ThrowsInvalidOperationException()
        {
            RulesEngineState state = TestDataFactory.GetProfileObject();
            var outcome = new DepositPointsOutcome
            {
                DollarAmountProvider = null,
                PointsPerDollar = 1m
            };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                outcome.CalculateOutcomeAsync(state, default));

            Assert.Contains("DollarAmountProvider", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task AwardOutcomeAsync_WithoutAffectedPointAccountTypes_ThrowsInvalidOperationException()
        {
            var profile = TestDataFactory.GetProfileObject();
            var outcome = CreateBaseOutcome(
                tenantId: profile.TenantId,
                aggregateType: AggregateType.Sum,
                constraint: null);
            outcome.AffectedPointAccountTypeIds = new List<string>();
            outcome.AffectedPointAccountTypes = new List<PointAccountType>();

            await outcome.CalculateOutcomeAsync(profile, default(CancellationToken));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                outcome.AwardOutcomeAsync(profile, GetLoyaltyAccountService(), default(CancellationToken)));

            Assert.Contains("point account type", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Desposit", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task CalculateOutcomeAsync_WithConstraint_FilteredPointsCalculatedCorrectly()
        {
            // Arrange
            var profile = TestDataFactory.GetProfileObject();
            var outcome = CreateBaseOutcome(
                tenantId: profile.TenantId,
                aggregateType: AggregateType.Sum,
                constraint: new StringPropertyRule(
                    "event.sku",
                    StringEvalType.NotEqual,
                    new ConstantValueProvider("sku2")
                )
            );

            // Act
            var result = await outcome.CalculateOutcomeAsync(profile, default(CancellationToken));

            // Assert
            Assert.NotNull(result);
            var depositOutcome = (DepositPointsOutcome)result.IssuingOutcome;
            Assert.Equal(100, depositOutcome.AwardedPoints);
        }

        [Fact]
        public async Task CalculateOutcomeAsync_WithMinAggregation_ReturnsMinimumPoints()
        {
            // Arrange
            var profile = TestDataFactory.GetProfileObject();
            var outcome = CreateBaseOutcome(
                tenantId: profile.TenantId,
                aggregateType: AggregateType.Min,
                constraint: null
            );

            // Act
            var result = await outcome.CalculateOutcomeAsync(profile, default(CancellationToken));

            // Assert
            Assert.NotNull(result);
            var depositOutcome = (DepositPointsOutcome)result.IssuingOutcome;
            Assert.Equal(50, depositOutcome.AwardedPoints);
        }

        [Fact]
        public async Task CalculateOutcomeAsync_WithUnresolvedEventIdPath_ReturnsNull()
        {
            var profile = TestDataFactory.GetProfileObject();
            var outcome = CreateBaseOutcome(
                tenantId: profile.TenantId,
                aggregateType: AggregateType.Sum,
                constraint: null,
                eventIdPath: "blah");

            var result = await outcome.CalculateOutcomeAsync(profile, default);

            Assert.Null(result);
        }

        #region PointsPerDollar Tests
        [Theory]
        [InlineData(2, 300)]    // 150 * 2
        [InlineData(0.5, 75)]   // 150 * 0.5
        [InlineData(0, 0)]      // 150 * 0
        public async Task CalculateOutcomeAsync_WithDifferentPointsPerDollar_CalculatesCorrectly(
            decimal pointsPerDollar, decimal expectedPoints)
        {
            // Arrange
            var profile = TestDataFactory.GetProfileObject();
            var outcome = CreateBaseOutcome(
                tenantId: profile.TenantId,
                aggregateType: AggregateType.Sum,
                constraint: null
            );
            outcome.PointsPerDollar = pointsPerDollar;

            // Act
            var result = await outcome.CalculateOutcomeAsync(profile, default(CancellationToken));

            // Assert
            Assert.NotNull(result);
            var depositOutcome = (DepositPointsOutcome)result.IssuingOutcome;
            Assert.Equal(expectedPoints, depositOutcome.AwardedPoints);
        }
        #endregion

        #region EarnDate Tests
        [Fact]
        public async Task AwardOutcomeAsync_WithCustomEarnDate_UsesProvidedDate()
        {
            // Arrange
            var profile = TestDataFactory.GetProfileObject();
            var customDate = DateTimeOffset.UtcNow.AddDays(-5);
            var outcome = CreateBaseOutcome(
                tenantId: profile.TenantId,
                aggregateType: AggregateType.Sum,
                constraint: null
            );
            outcome.EarnDateProvider = new ConstantValueProvider(customDate);

            // Calculate first
            await outcome.CalculateOutcomeAsync(profile, default(CancellationToken));

            // Act
            var result = await outcome.AwardOutcomeAsync(
                profile,
                GetLoyaltyAccountService(),
                default(CancellationToken)
            );

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(outcome.PointResults);
            Assert.Equal(customDate, outcome.PointResults.First().LedgerEntries[0].EarnDate);
        }

        [Fact]
        public async Task AwardOutcomeAsync_WithoutEarnDateProvider_UsesCurrentTime()
        {
            // Arrange
            var profile = TestDataFactory.GetProfileObject();
            var outcome = CreateBaseOutcome(
                tenantId: profile.TenantId,
                aggregateType: AggregateType.Sum,
                constraint: null
            );
            var beforeTest = DateTimeOffset.UtcNow;

            // Calculate first
            await outcome.CalculateOutcomeAsync(profile, default(CancellationToken));

            // Act
            var result = await outcome.AwardOutcomeAsync(
                profile,
                GetLoyaltyAccountService(),
                default(CancellationToken)
            );
            var afterTest = DateTimeOffset.UtcNow;

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(outcome.PointResults);
            var earnDate = outcome.PointResults.First().LedgerEntries[0].EarnDate;
            Assert.True(earnDate >= beforeTest && earnDate <= afterTest);
        }
        #endregion

        #region Expiration Tests
        [Theory]
        [InlineData(30)]
        [InlineData(60)]
        [InlineData(90)]
        public async Task AwardOutcomeAsync_WithDifferentExpirationDays_SetsCorrectExpirationDate(int daysToExpiration)
        {
            // Arrange
            var profile = TestDataFactory.GetProfileObject();
            var outcome = CreateBaseOutcome(
                tenantId: profile.TenantId,
                aggregateType: AggregateType.Sum,
                constraint: null,
                ttl: daysToExpiration
            );
            var pat = SetSpendablePointAccount(profile.TenantId, daysToExpiration);
            outcome.AffectedPointAccountTypes = new List<PointAccountType> { pat };
            pat.PointsLifespanDays = daysToExpiration;

            // Calculate first
            await outcome.CalculateOutcomeAsync(profile, default(CancellationToken));

            // Act
            var result = await outcome.AwardOutcomeAsync(
                profile,
                GetLoyaltyAccountService(),
                default(CancellationToken)
            );

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(outcome.PointResults);
            var entry = outcome.PointResults.First().LedgerEntries[0];
            Assert.Equal(entry.EarnDate.Value.AddDays(daysToExpiration), entry.ExpirationDate);
        }
        #endregion

        #region Edge Cases
        [Fact]
        public async Task CalculateOutcomeAsync_WithNullAmount_ReturnsZeroPoints()
        {
            // Arrange
            var profile = TestDataFactory.GetProfileObject();
            var outcome = CreateBaseOutcome(
                tenantId: profile.TenantId,
                aggregateType: AggregateType.Sum,
                constraint: null
            );
            outcome.DollarAmountProvider = new ConstantValueProvider(null); // Force null amount

            // Act
            var result = await outcome.CalculateOutcomeAsync(profile, default(CancellationToken));

            // Assert
            Assert.NotNull(result);
            var depositOutcome = (DepositPointsOutcome)result.IssuingOutcome;
            Assert.Equal(0, depositOutcome.AwardedPoints);
        }

        [Fact]
        public async Task AwardOutcomeAsync_WithZeroPoints_StillCreatesLedgerEntry()
        {
            // Arrange
            var profile = TestDataFactory.GetProfileObject();
            var outcome = CreateBaseOutcome(
                tenantId: profile.TenantId,
                aggregateType: AggregateType.Sum,
                constraint: null
            );
            outcome.DollarAmountProvider = new ConstantValueProvider(0m);

            // Calculate first
            await outcome.CalculateOutcomeAsync(profile, default(CancellationToken));

            // Act
            var result = await outcome.AwardOutcomeAsync(
                profile,
                GetLoyaltyAccountService(),
                default(CancellationToken)
            );

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(outcome.PointResults);
            Assert.Single(outcome.PointResults.First().LedgerEntries);
            Assert.Equal(0, outcome.PointResults.First().LedgerEntries[0].PointsDeposited);
        }

        [Fact]
        public async Task CalculateOutcomeAsync_WhenEventIdProviderNullAndStateEventIdSet_UsesStateFallback()
        {
            var state = TestDataFactory.GetProfileObject();
            state.EventId = "fallback-event-id";
            state.EventType = "order";

            var outcome = CreateBaseOutcome(
                tenantId: state.TenantId,
                aggregateType: AggregateType.Sum,
                constraint: null);
            outcome.EventIdProvider = null;
            outcome.EventTypeProvider = null;

            var result = await outcome.CalculateOutcomeAsync(state, default);

            Assert.NotNull(result);
            Assert.Equal("fallback-event-id", result.IssuingEventId);
            Assert.Equal("order", result.IssuingEventType);
        }

        [Fact]
        public async Task CalculateOutcomeAsync_WhenEventIdProviderNullAndStateEventIdEmpty_ReturnsNullWithoutException()
        {
            var state = TestDataFactory.GetProfileObject();
            state.EventId = null;
            state.EventType = null;

            var outcome = CreateBaseOutcome(
                tenantId: state.TenantId,
                aggregateType: AggregateType.Sum,
                constraint: null);
            outcome.EventIdProvider = null;
            outcome.EventTypeProvider = null;

            var result = await outcome.CalculateOutcomeAsync(state, default);

            Assert.Null(result);
        }
        #endregion

        [Fact]
        public async Task CampaignTest1()
        {
            try
            {
                var campaign = TestDataFactory.GenerateCampaign();
            }
            catch (Exception ex)
            {
            }

        }


        #region Helper Methods
        private DepositPointsOutcome CreateBaseOutcome(
            string tenantId,
            AggregateType aggregateType,
            RuleBase constraint,
            string eventIdPath = "event.extOrderId",
            decimal ttl = 30.0m)
        {
            return new DepositPointsOutcome
            {
                AffectedPointAccountTypeIds = new List<string> { _spendablePointAccountType.Id }, 
                AffectedPointAccountTypes = new List<PointAccountType> { SetSpendablePointAccount(tenantId, ttl) },
                DollarAmountProvider = new AggregateValueProvider
                {
                    AggregateType = aggregateType,
                    RowPropertyProvider = new PathValueProvider("event.noTaxTotal"),
                    RowProvider = new PathValueProvider("event.items"),
                    Constraint = constraint
                },
                EventIdProvider = new PathValueProvider(eventIdPath),
                PointsPerDollar = 1
            };
        }

        private PointAccountType SetSpendablePointAccount(string tenantId, decimal expDays)
        {
            var ptacct = new PointAccountType("test", "Active", "TestPointAccountType", null,
                                        PointLedgerTypeStrings.SPENDABLE, expDays, null, PointLedgerTypeStrings.EXPIRED, //30, null, 
                                        true, "AwayFromZero", 0, tenantId, _spendablePointAccountType.Id);

            _cache.CachePointAccountType(tenantId, ptacct);
            return ptacct;
        }

        private LoyaltyAccountService GetLoyaltyAccountService()
        {
            return new LoyaltyAccountService(
                new StubLoyaltyAccountAdapter(), //ILoyaltyAccountAdapter loyaltyAccountAdapter,
                new StubPointLedgerAdapter(),  //ILoyaltyAccountPointLedgerAdapter loyaltyAccountPointLedgerAdapter
                new StubTagAdapter(),  //ITagAdapter tagAdapter
                _cache, //IPointAccountTypeCache cache
                //new StubLoyaltyAccountRuleStateAdapter(), //ILoyaltyAccountRuleStateAdapter loyaltyAccountRuleStateAdapter
                new StubLoyaltyAccountPointsDetailsAdapter(), //ILoyaltyAccountPointsDetailsAdapter pointsDetailsAdapter
                LoggerFactoryProvider.CreateLogger<LoyaltyAccountService>(), //ILogger<LoyaltyAccountService> logger
                default(IDynamicDataAdapter), //IDynamicDataAdapter dynamicDataAdapter
                default(IDynamicExternalReferenceAdapter), //IDynamicExternalReferenceAdapter dynamicAdapter
                default(IDataLakeAdapter)
                ); 
        }


        #endregion
    }
}
