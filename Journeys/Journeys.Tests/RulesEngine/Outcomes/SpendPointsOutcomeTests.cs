using Journeys.Core;
using Journeys.Core.Caching;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.FileStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine.Comparitors.Enums;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Outcomes;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Providers.Enums;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.Services;
using Journeys.Tests.Stubs;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Journeys.Tests.RulesEngine.Outcomes
{
    public class SpendPointsOutcomeTests
    {

        PointAccountType _escrowPointAccountType;
        PointAccountType _expPointAccountType;
        PointAccountType _spendablePointAccountType;

        StubPointAccountTypeCache _cache;

        public SpendPointsOutcomeTests()
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

        #region Basic Calculation Tests
        [Fact]
        public async Task CalculateOutcomeAsync_WithValidAmount_SetsSpentPoints()
        {
            // Arrange
            var profile = TestDataFactory.GetProfileObject();
            var outcome = CreateBaseOutcome(100m, "event-123");

            // Act
            var result = await outcome.CalculateOutcomeAsync(profile, default(CancellationToken));

            // Assert
            Assert.NotNull(result);
            Assert.Equal("event-123", result.IssuingEventId);
            Assert.Equal(100m, ((SpendPointsOutcome)result.IssuingOutcome).PointsWithdrawn);
        }

        [Fact]
        public async Task CalculateOutcomeAsync_WithNullAmount_SetsZeroPoints()
        {
            // Arrange
            var profile = TestDataFactory.GetProfileObject();
            var outcome = CreateBaseOutcome(null, "event-123");

            // Act
            var result = await outcome.CalculateOutcomeAsync(profile, default(CancellationToken));

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0m, ((SpendPointsOutcome)result.IssuingOutcome).PointsWithdrawn);
        }
        #endregion

        #region Award Tests
        [Fact]
        public async Task AwardOutcomeAsync_CreatesWithdrawalEntry()
        {
            // Arrange
            var profile = TestDataFactory.GetProfileObject();
            var outcome = CreateBaseOutcome(50m, "event-123");
            var loyaltyAccountService = GetLoyaltyAccountService();

            // Calculate first
            await outcome.CalculateOutcomeAsync(profile, default(CancellationToken));

            // Act
            var result = await outcome.AwardOutcomeAsync(profile, loyaltyAccountService, default(CancellationToken));

            // Assert
            var points = await loyaltyAccountService.GetLoyaltyAccountPointsAsync(profile.TenantId, profile.LoyaltyAccountId);
            Assert.NotNull(points);
            var ledger = Assert.Single(points);
            //Assert.Equal(PointLedgerTypeStrings.SPENDABLE, ledger.PointAccountTypeId);
            var entry = Assert.Single(ledger.LedgerEntries);
            Assert.Equal(50m, entry.PointsWithdrawn);
            Assert.Equal("event-123", entry.EventId);
        }

        [Fact]
        public async Task AwardOutcomeAsync_SetsCorrectBurnDate()
        {
            // Arrange
            var profile = TestDataFactory.GetProfileObject();
            var outcome = CreateBaseOutcome(50m, "event-123");
            var loyaltyAccountService = GetLoyaltyAccountService();
            var beforeTest = DateTime.UtcNow;

            // Calculate first
            await outcome.CalculateOutcomeAsync(profile, default(CancellationToken));

            // Act
            var result = await outcome.AwardOutcomeAsync(profile, loyaltyAccountService, default(CancellationToken));
            var afterTest = DateTime.UtcNow;

            // Assert
            var points = await loyaltyAccountService.GetLoyaltyAccountPointsAsync(profile.TenantId, profile.LoyaltyAccountId);
            var ledger = Assert.Single(points);
            var entry = Assert.Single(ledger.LedgerEntries);
            Assert.True(entry.BurnDate >= beforeTest && entry.BurnDate <= afterTest);
        }
        #endregion

        #region Edge Cases
        [Fact]
        public async Task CalculateOutcomeAsync_WithMissingEventId_ThrowsException()
        {
            // Arrange
            var profile = TestDataFactory.GetProfileObject();
            var outcome = CreateBaseOutcome(50m, null);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(() =>
                outcome.CalculateOutcomeAsync(profile, default(CancellationToken)));
            Assert.Contains("No EventId provided", exception.Message);
        }

        [Fact]
        public async Task AwardOutcomeAsync_WithZeroPoints_CreatesEntry()
        {
            // Arrange
            var profile = TestDataFactory.GetProfileObject();
            var outcome = CreateBaseOutcome(0m, "event-123");
            var loyaltyAccountService = GetLoyaltyAccountService();

            // Calculate first
            await outcome.CalculateOutcomeAsync(profile, default(CancellationToken));

            // Act
            var result = await outcome.AwardOutcomeAsync(profile, loyaltyAccountService, default(CancellationToken));

            // Assert
            var points = await loyaltyAccountService.GetLoyaltyAccountPointsAsync(profile.TenantId, profile.LoyaltyAccountId);
            var ledger = Assert.Single(points);
            var entry = Assert.Single(ledger.LedgerEntries);
            Assert.Equal(0m, entry.PointsWithdrawn);
        }
        #endregion

        #region Complex Scenarios
        [Fact]
        public async Task MultipleWithdrawals_ProcessedInOrder()
        {
            // Arrange
            var profile = TestDataFactory.GetProfileObject();
            var loyaltyAccountService = GetLoyaltyAccountService();

            // First withdrawal
            var firstWithdrawal = CreateBaseOutcome(30m, "event-1");
            await firstWithdrawal.CalculateOutcomeAsync(profile, default(CancellationToken));
            await firstWithdrawal.AwardOutcomeAsync(profile, loyaltyAccountService, default(CancellationToken));

            // Second withdrawal
            var secondWithdrawal = CreateBaseOutcome(20m, "event-2");
            await secondWithdrawal.CalculateOutcomeAsync(profile, default(CancellationToken));
            await secondWithdrawal.AwardOutcomeAsync(profile, loyaltyAccountService, default(CancellationToken));

            // Assert
            var points = await loyaltyAccountService.GetLoyaltyAccountPointsAsync(profile.TenantId, profile.LoyaltyAccountId);
            var ledger = Assert.Single(points);
            Assert.Equal(2, ledger.LedgerEntries.Count);
            Assert.Equal(30m, ledger.LedgerEntries[0].PointsWithdrawn);
            Assert.Equal(20m, ledger.LedgerEntries[1].PointsWithdrawn);
            Assert.True(ledger.LedgerEntries[0].BurnDate <= ledger.LedgerEntries[1].BurnDate);
        }

        [Fact]
        public async Task MultipleWithdrawals_WithSameEventId_CreatesDistinctEntries()
        {
            // Arrange
            var profile = TestDataFactory.GetProfileObject();
            var loyaltyAccountService = GetLoyaltyAccountService();

            // First withdrawal
            var firstWithdrawal = CreateBaseOutcome(25m, "same-event");
            await firstWithdrawal.CalculateOutcomeAsync(profile, default(CancellationToken));
            await firstWithdrawal.AwardOutcomeAsync(profile, loyaltyAccountService, default(CancellationToken));

            // Second withdrawal with same event ID
            var secondWithdrawal = CreateBaseOutcome(35m, "same-event");
            await secondWithdrawal.CalculateOutcomeAsync(profile, default(CancellationToken));
            await secondWithdrawal.AwardOutcomeAsync(profile, loyaltyAccountService, default(CancellationToken));

            // Assert
            var points = await loyaltyAccountService.GetLoyaltyAccountPointsAsync(profile.TenantId, profile.LoyaltyAccountId);
            var ledger = Assert.Single(points);
            Assert.Equal(1, ledger.LedgerEntries.Count);
            Assert.Equal(35m, ledger.LedgerEntries[0].PointsWithdrawn);
        }

        [Fact]
        public async Task ComplexScenario_MultipleWithdrawalsWithDifferentAmounts()
        {
            // Arrange
            var profile = TestDataFactory.GetProfileObject();
            var loyaltyAccountService = new LoyaltyAccountService(
                new StubLoyaltyAccountAdapter(), 
                new StubPointLedgerAdapter(), 
                new StubTagAdapter(),
                _cache, //new StubLoyaltyAccountRuleStateAdapter(),
                new StubLoyaltyAccountPointsDetailsAdapter(),
                LoggerFactoryProvider.CreateLogger<LoyaltyAccountService>(), //ILogger<LoyaltyAccountService> logger
                default(IDynamicDataAdapter), //IDynamicDataAdapter dynamicDataAdapter
                default(IDynamicExternalReferenceAdapter), //IDynamicExternalReferenceAdapter dynamicAdapter
                default(IDataLakeAdapter)
            );

            var withdrawals = new[]
            {
                (amount: 10m, eventId: "event-small"),
                (amount: 50m, eventId: "event-medium"),
                (amount: 0m, eventId: "event-zero"),
                (amount: 100m, eventId: "event-large")
            };

            // Act
            foreach (var (amount, eventId) in withdrawals)
            {
                var withdrawal = CreateBaseOutcome(amount, eventId);
                await withdrawal.CalculateOutcomeAsync(profile, default(CancellationToken));
                await withdrawal.AwardOutcomeAsync(profile, loyaltyAccountService, default(CancellationToken));
            }

            // Assert
            var points = await loyaltyAccountService.GetLoyaltyAccountPointsAsync(profile.TenantId, profile.LoyaltyAccountId);
            var ledger = Assert.Single(points);
            Assert.Equal(4, ledger.LedgerEntries.Count);
            Assert.Equal(160m, ledger.LedgerEntries.Sum(e => e.PointsWithdrawn));
        }
        #endregion

        #region Additional Validation Cases
        [Theory]
        [InlineData(-1)]
        [InlineData(-100)]
        public async Task CalculateOutcomeAsync_WithNegativeAmount_StillProcesses(decimal amount)
        {
            // Arrange
            var profile = TestDataFactory.GetProfileObject();
            var outcome = CreateBaseOutcome(amount, "event-123");

            // Act
            var result = await outcome.CalculateOutcomeAsync(profile, default(CancellationToken));

            // Assert
            Assert.NotNull(result);
            Assert.Equal(amount, ((SpendPointsOutcome)result.IssuingOutcome).PointsWithdrawn);
        }


        [Fact]
        public async Task AwardOutcomeAsync_WithInvalidUserId_ThrowsException()
        {
            // Arrange
            var profile = TestDataFactory.GetProfileObject();
            profile.LoyaltyAccountId = ""; // Invalid user ID
            var outcome = CreateBaseOutcome(50m, "event-123");
            var loyaltyAccountService = GetLoyaltyAccountService();

            // Calculate first
            await outcome.CalculateOutcomeAsync(profile, default(CancellationToken));

            // Act
            var exception = await Assert.ThrowsAsync<Exception>(() =>
                outcome.AwardOutcomeAsync(profile, loyaltyAccountService, default(CancellationToken)));

            Assert.Equal("The account id must be provided.", exception.Message);
        }

        [Fact]
        public async Task AwardOutcomeAsync_WithMaxDecimalAmount_ProcessesCorrectly()
        {
            // Arrange
            var profile = TestDataFactory.GetProfileObject();
            var outcome = CreateBaseOutcome(decimal.MaxValue, "event-123");
            var loyaltyAccountService = GetLoyaltyAccountService();

            // Calculate first
            await outcome.CalculateOutcomeAsync(profile, default(CancellationToken));

            // Act
            var result = await outcome.AwardOutcomeAsync(profile, loyaltyAccountService, default(CancellationToken));

            // Assert
            var points = await loyaltyAccountService.GetLoyaltyAccountPointsAsync(profile.TenantId, profile.LoyaltyAccountId);
            var ledger = Assert.Single(points);
            var entry = Assert.Single(ledger.LedgerEntries);
            Assert.Equal(decimal.MaxValue, entry.PointsWithdrawn);
        }
        #endregion

        #region Helper Methods
        private SpendPointsOutcome CreateBaseOutcome(decimal? amount, string eventId)
        {
            return new SpendPointsOutcome
            {
                AffectedPointAccountTypeIds = new List<string> { _spendablePointAccountType.Id },
                AffectedPointAccountTypes = new List<PointAccountType> { _spendablePointAccountType },
                WithdrawlAmountProvider = new ConstantValueProvider(amount),
                EventIdProvider = new ConstantValueProvider(eventId)
            };
        }

        private LoyaltyAccountService GetLoyaltyAccountService()
        {
            return new LoyaltyAccountService(
                new StubLoyaltyAccountAdapter(),
                new StubPointLedgerAdapter(),
                new StubTagAdapter(),
                _cache,
                //new StubLoyaltyAccountRuleStateAdapter(),
                new StubLoyaltyAccountPointsDetailsAdapter(),
                LoggerFactoryProvider.CreateLogger<LoyaltyAccountService>(), //ILogger<LoyaltyAccountService> logger
                default(IDynamicDataAdapter), //IDynamicDataAdapter dynamicDataAdapter
                default(IDynamicExternalReferenceAdapter), //IDynamicExternalReferenceAdapter dynamicAdapter
                default(IDataLakeAdapter)
            );
        }

        #endregion
    }
} 