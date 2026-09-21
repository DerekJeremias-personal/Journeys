using Journeys.Core.Caching;
using Journeys.Core.Extensions;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.FileStorage;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine.Comparitors.Enums;
using Journeys.Core.RulesEngine.Outcomes;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.Services;
using Journeys.DTO.Models;
using Journeys.Tests.Stubs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Journeys.Tests.RulesEngine.Outcomes
{
    public class ExpirePointsOutcomeTests
    {

        PointAccountType _escrowPointAccountType;
        PointAccountType _expPointAccountType;
        PointAccountType _spendablePointAccountType;
        StubPointAccountTypeCache _cache;

        public ExpirePointsOutcomeTests()
        {
            _cache = new StubPointAccountTypeCache();

            _escrowPointAccountType = TestDataFactory.GetEscrowPointAccount();
            _spendablePointAccountType = TestDataFactory.GetSpendablePointAccount();
            _expPointAccountType = TestDataFactory.GetExpPointAccount();

            // Make sure expiration type is properly linked
            _spendablePointAccountType.ExpiresToPointAccountTypeId = _expPointAccountType.Id;
            _escrowPointAccountType.ExpiresToPointAccountTypeId = _spendablePointAccountType.Id;

            _cache.CachePointAccountType(_escrowPointAccountType.TenantId, _escrowPointAccountType);
            _cache.CachePointAccountType(_spendablePointAccountType.TenantId, _spendablePointAccountType);
            _cache.CachePointAccountType(_expPointAccountType.TenantId, _expPointAccountType);
        }

        #region Basic Validation Tests

        [Theory]
        [InlineData(-1)]
        [InlineData(100001)]
        public async Task ExpirePoints_WithInvalidAmount_ThrowsException(decimal amount)
        {
            // Arrange
            var profile = TestDataFactory.GetProfileObject();
            var outcome = await CreateBaseOutcome(profile.TenantId, amount, null, _spendablePointAccountType.Id, "event-123");
            var loyaltyAccountService = CreateLoyaltyAccountService();

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() =>
                outcome.AwardOutcomeAsync(profile, loyaltyAccountService, default));
        }

        [Theory]
        [InlineData(-0.1)]
        [InlineData(1.1)]
        public async Task ExpirePoints_WithInvalidPercent_ThrowsException(decimal percent)
        {
            // Arrange
            var profile = TestDataFactory.GetProfileObject();
            var outcome = await CreateBaseOutcome(profile.TenantId, null, percent, _spendablePointAccountType.Id, "event-123");
            var loyaltyAccountService = CreateLoyaltyAccountService();

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() =>
                outcome.AwardOutcomeAsync(profile, loyaltyAccountService, default));
        }
        #endregion

        #region Point Lifecycle Tests
        [Fact]
        public async Task PointLifecycle_EscrowToSpendableToExpired()
        {
            // Arrange
            var profile = TestDataFactory.GetProfileObject();
            var loyaltyAccountService = CreateLoyaltyAccountService();

            // Setup initial points in ESCROW
            var escrowPoints = await CreatePointLedger(
                profile.TenantId,
                profile.LoyaltyAccountId,
                _escrowPointAccountType,
                loyaltyAccountService,
                earnDate: DateTimeOffset.UtcNow.AddDays(-45),
                amount: 100m,
                eventId: "purchase-123"
            );

            // Move from ESCROW to SPENDABLE
            var escrowToSpendable = await CreateBaseOutcome(
                tenantId: profile.TenantId,
                amount: null,
                percent: 1.0m,
                ledgerTypeId: _escrowPointAccountType.Id,
                eventId: "return-period-end",
                earnDate: DateTimeOffset.UtcNow.AddDays(-30)
            );

            await escrowToSpendable.CalculateOutcomeAsync(profile, default);
            await escrowToSpendable.AwardOutcomeAsync(profile, loyaltyAccountService, default);

            // Verify intermediate state
            var midPoints = await loyaltyAccountService.GetLoyaltyAccountPointsAsync(profile.TenantId, profile.LoyaltyAccountId);
            var spendableLedger = midPoints.Find(l => l.PointAccountTypeId == _spendablePointAccountType.Id);

            Assert.Equal(0m, midPoints.Find(l => l.PointAccountTypeId == _escrowPointAccountType.Id)?.CurrentBalance ?? 0m);
            Assert.Equal(100m, spendableLedger?.CurrentBalance);

            // Move from SPENDABLE to EXPIRED after 1 year
            var spendableToExpired = await CreateBaseOutcome(
                tenantId: profile.TenantId,
                amount: null,
                percent: 1.0m,
                ledgerTypeId: _spendablePointAccountType.Id,
                eventId: "annual-expiration",
                expirationDate: DateTimeOffset.UtcNow.AddYears(1)
            );

            await spendableToExpired.CalculateOutcomeAsync(profile, default);
            await spendableToExpired.AwardOutcomeAsync(profile, loyaltyAccountService, default);

            // Verify final state
            var finalPoints = await loyaltyAccountService.GetLoyaltyAccountPointsAsync(profile.TenantId, profile.LoyaltyAccountId);
            Assert.Equal(0m, finalPoints.Find(l => l.PointAccountTypeId == _spendablePointAccountType.Id)?.CurrentBalance ?? 0m);
            Assert.Equal(100m, finalPoints.Find(l => l.PointAccountTypeId == _expPointAccountType.Id)?.CurrentBalance);
        }

        [Fact]
        public async Task PointLifecycle_PartialExpirationFromEscrow()
        {
            // Arrange
            var profile = TestDataFactory.GetProfileObject();
            var loyaltyAccountService = CreateLoyaltyAccountService();

            // Setup points in ESCROW with different earn dates
            var olderPoints = await CreatePointLedger(
                profile.TenantId,
                profile.LoyaltyAccountId,
                _escrowPointAccountType,
                loyaltyAccountService,
                earnDate: DateTimeOffset.UtcNow.AddDays(-45),
                amount: 60m,
                eventId: "purchase-1"
            );

            var newerPoints = await CreatePointLedger(
                profile.TenantId,
                profile.LoyaltyAccountId,
                _escrowPointAccountType,
                loyaltyAccountService,
                earnDate: DateTimeOffset.UtcNow.AddDays(-15),
                amount: 40m,
                eventId: "purchase-2"
            );

            // Expire points older than 30 days
            var outcome = await CreateBaseOutcome(
                tenantId: profile.TenantId,
                amount: null,
                percent: 1.0m,
                ledgerTypeId: _escrowPointAccountType.Id,
                eventId: "partial-expiration",
                earnDate: DateTimeOffset.UtcNow.AddDays(-30)
            );

            await outcome.CalculateOutcomeAsync(profile, default);
            await outcome.AwardOutcomeAsync(profile, loyaltyAccountService, default);

            // Assert
            var points = await loyaltyAccountService.GetLoyaltyAccountPointsAsync(profile.TenantId, profile.LoyaltyAccountId);
            Assert.Equal(40m, points.Find(l => l.PointAccountTypeId == _escrowPointAccountType.Id)?.CurrentBalance);
            Assert.Equal(60m, points.Find(l => l.PointAccountTypeId == _spendablePointAccountType.Id)?.CurrentBalance);
        }
        #endregion

        #region Expiration Method Tests
        [Fact]
        public async Task ExpireByAmount_MovesCorrectPoints()
        {
            // Arrange
            var profile = TestDataFactory.GetProfileObject();
            var loyaltyAccountService = CreateLoyaltyAccountService();

            // Setup points in SPENDABLE
            var points = await CreatePointLedger(
                profile.TenantId,
                profile.LoyaltyAccountId,
                _spendablePointAccountType,
                loyaltyAccountService,
                earnDate: DateTimeOffset.UtcNow.AddDays(-45),
                amount: 100m,
                eventId: "purchase-1"
            );

            // Expire fixed amount
            var outcome = await CreateBaseOutcome(
                tenantId: profile.TenantId,
                amount: 40m,
                percent: null,
                ledgerTypeId: _spendablePointAccountType.Id,
                eventId: "amount-expiration"
            );

            await outcome.CalculateOutcomeAsync(profile, default);
            await outcome.AwardOutcomeAsync(profile, loyaltyAccountService, default);

            // Assert
            var finalPoints = await loyaltyAccountService.GetLoyaltyAccountPointsAsync(profile.TenantId, profile.LoyaltyAccountId);
            Assert.Equal(60m, finalPoints.Find(l => l.PointAccountTypeId == _spendablePointAccountType.Id)?.CurrentBalance);
            Assert.Equal(40m, finalPoints.Find(l => l.PointAccountTypeId == _expPointAccountType.Id)?.CurrentBalance);
        }

        [Fact]
        public async Task ExpireByEventId_ExpiresCorrectPoints()
        {
            // Arrange
            var profile = TestDataFactory.GetProfileObject();
            var loyaltyAccountService = CreateLoyaltyAccountService();

            await loyaltyAccountService.UpsertLoyaltyAccountAsync(profile.TenantId, profile.LoyaltyAccount.ToDto());

            // Setup points with specific event IDs
            var points1 = await CreatePointLedger(
                profile.TenantId,
                profile.LoyaltyAccountId,
                _spendablePointAccountType,
                loyaltyAccountService,
                earnDate: DateTimeOffset.UtcNow.AddDays(-45),
                amount: 100m,
                eventId: "order-123"
            );

            var points2 = await CreatePointLedger(
                profile.TenantId,
                profile.LoyaltyAccountId,
                _spendablePointAccountType,
                loyaltyAccountService,
                earnDate: DateTimeOffset.UtcNow.AddDays(-45),
                amount: 50m,
                eventId: "order-456"
            );

            // Expire specific event
            var outcome = await CreateBaseOutcome(
                tenantId: profile.TenantId,
                amount: null,
                percent: 1.0m,
                ledgerTypeId: _spendablePointAccountType.Id,
                eventId: "order-123"
            );

            await outcome.CalculateOutcomeAsync(profile, default);
            await outcome.AwardOutcomeAsync(profile, loyaltyAccountService, default);

            // Assert
            var finalPoints = await loyaltyAccountService.GetLoyaltyAccountPointsAsync(profile.TenantId, profile.LoyaltyAccountId);
            Assert.Equal(50m, finalPoints.Find(l => l.PointAccountTypeId == _spendablePointAccountType.Id)?.CurrentBalance);
            Assert.Equal(100m, finalPoints.Find(l => l.PointAccountTypeId == _expPointAccountType.Id)?.CurrentBalance);
        }
        #endregion

        #region Helper Methods
        private async Task<ExpirePointsOutcome> CreateBaseOutcome(
            string tenantId,
            decimal? amount,
            decimal? percent,
            //string ledgerId,
            string ledgerTypeId,
            string eventId,
            DateTimeOffset? earnDate = null,
            DateTimeOffset? expirationDate = null)
        {
            var pat = await _cache.GetPointAccountTypeAsync(tenantId, ledgerTypeId);
            return new ExpirePointsOutcome
            {
                ExpirationAmount = amount,
                ExpireAmountProvider = new ConstantValueProvider(amount),
                ExpirationPercent = percent,
                AffectedPointAccountTypeIds = new List<string> { ledgerTypeId },
                AffectedPointAccountTypes = new List<PointAccountType> { pat },
                EventIdProvider = new ConstantValueProvider(eventId),
                EarnDateProvider = new ConstantValueProvider(earnDate),
                ExpirationDateProvider = new ConstantValueProvider(expirationDate)
            };
        }

        private async Task<PointLedgerDto> CreatePointLedger(
            string tenantId,
            string loyaltyAccountId,
            PointAccountType pointAccountType,
            LoyaltyAccountService loyaltyAccountService,
            DateTimeOffset earnDate,
            decimal amount,
            string eventId)
        {
            var ledger = new PointLedgerDto
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                AccountId = loyaltyAccountId,
                PointAccountTypeId = pointAccountType.Id,
                LedgerEntries = new List<LedgerEntryDto>
                {
                    new LedgerEntryDto
                    {
                        EventId = eventId,
                        EventType = "evtType",
                        EarnDate = earnDate,
                        ExpirationDate = earnDate.AddDays((double)pointAccountType.PointsLifespanDays),
                        PointsDeposited = amount,
                        SpendablePoints = amount
                    }
                }
            };

            var acct = TestDataFactory.GenerateRichard();
            var accountDto = acct.ToDto();
            accountDto.Id = loyaltyAccountId;
            accountDto.ExtAccountId = loyaltyAccountId;
            await loyaltyAccountService.UpsertLoyaltyAccountAsync(tenantId, accountDto);
            return await loyaltyAccountService.UpsertLoyaltyAccountPointsAsync(tenantId, ledger, acct);
        }

        private LoyaltyAccountService CreateLoyaltyAccountService()
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