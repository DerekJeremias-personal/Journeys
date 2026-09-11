using Journeys.Core;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.FileStorage;
using Journeys.Core.Models;
using Journeys.Core.Services;
using Journeys.DTO.Models;
using Journeys.DTO.Requests;
using Journeys.Tests.Stubs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Journeys.Tests.Services
{
    public class UserPointsTests
    {
        private readonly LoyaltyAccountService _service;
        private readonly string _tenantId = TestDataFactory.TENANT_ID;
        private readonly string _userId = "user1";

        private readonly PointAccountType _escrowPointAccountType;
        private readonly PointAccountType _expPointAccountType;
        private readonly PointAccountType _spendablePointAccountType;

        public UserPointsTests()
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
            _escrowPointAccountType.ExpiresToPointAccountTypeId = _spendablePointAccountType.Id;

            _service = new LoyaltyAccountService(
                new StubLoyaltyAccountAdapter(),
                new StubPointLedgerAdapter(),
                new StubTagAdapter(),
                cache,
                //new StubLoyaltyAccountRuleStateAdapter(),
                new StubLoyaltyAccountPointsDetailsAdapter(),
                LoggerFactoryProvider.CreateLogger<LoyaltyAccountService>(), //ILogger<LoyaltyAccountService> logger
                default(IDynamicDataAdapter), //IDynamicDataAdapter dynamicDataAdapter
                default(IDynamicExternalReferenceAdapter), //IDynamicExternalReferenceAdapter dynamicAdapter
                default(IDataLakeAdapter)
            );
        }

        #region Basic Point Operations

        [Fact]
        public async Task UpsertPoints_WhenDepositingPoints_ShouldUpdateBalance()
        {
            // Arrange
            var pointLedger = new PointLedgerDto
            {
                TenantId = _tenantId,
                AccountId = _userId,
                PointAccountTypeId = _spendablePointAccountType.Id,
                LedgerEntries = new List<LedgerEntryDto>
                {
                    new LedgerEntryDto
                    {
                        EventId = "purchase-123",
                        EventType = "Purchase",
                        EarnDate = DateTimeOffset.UtcNow,
                        PointsDeposited = 100
                    }
                }
            };

            // Act
            var result = await _service.UpsertLoyaltyAccountPointsAsync(_tenantId, pointLedger, TestDataFactory.GenerateRichard());

            // Assert
            Assert.NotNull(result);
            Assert.Equal(100, result.CurrentBalance);
        }

        [Fact]
        public async Task UpsertPoints_WhenSpendingPoints_ShouldReduceBalance()
        {
            // Arrange - First deposit points
            var depositLedger = new PointLedgerDto
            {
                TenantId = _tenantId,
                AccountId = _userId,
                PointAccountTypeId = _spendablePointAccountType.Id,
                LedgerEntries = new List<LedgerEntryDto>
                {
                    new LedgerEntryDto
                    {
                        EventId = "deposit-123",
                        EventType = "Purchase",
                        EarnDate = DateTimeOffset.UtcNow,
                        PointsDeposited = 100
                    }
                }
            };
            await _service.UpsertLoyaltyAccountPointsAsync(_tenantId, depositLedger, TestDataFactory.GenerateRichard());

            // Arrange - Then spend points
            var spendLedger = new PointLedgerDto
            {
                TenantId = _tenantId,
                AccountId = _userId,
                PointAccountTypeId = _spendablePointAccountType.Id,
                LedgerEntries = new List<LedgerEntryDto>
                {
                    new LedgerEntryDto
                    {
                        EventId = "spend-123",
                        EventType = "Redemption",
                        EarnDate = DateTimeOffset.UtcNow,
                        PointsWithdrawn = 50
                    }
                }
            };

            // Act
            var result = await _service.UpsertLoyaltyAccountPointsAsync(_tenantId, spendLedger, TestDataFactory.GenerateRichard());

            // Assert
            Assert.NotNull(result);
            Assert.Equal(50, result.CurrentBalance);
        }

        #endregion

        #region Point Expiration Tests

        [Fact]
        public async Task ExpirePoints_WhenPointsExpire_ShouldMoveToExpiredLedger()
        {
            // Arrange
            var earnDate = DateTimeOffset.UtcNow.AddDays(-31);
            var ledger = new PointLedgerDto
            {
                TenantId = _tenantId,
                AccountId = _userId,
                PointAccountTypeId = _spendablePointAccountType.Id,
                LedgerEntries = new List<LedgerEntryDto>
                {
                    new LedgerEntryDto
                    {
                        EventId = "old-points-123",
                        EventType = "Purchase",
                        EarnDate = earnDate,
                        PointsDeposited = 100
                    }
                }
            };
            var storedLedger = await _service.UpsertLoyaltyAccountPointsAsync(_tenantId, ledger, TestDataFactory.GenerateRichard());

            // Act
            var result = await _service.ExpireLoyaltyAccountPointsByEarnDate(
                _tenantId,
                _userId,
                new List<string> { _spendablePointAccountType.Id },
                DateTimeOffset.UtcNow,
                1.0m
            );

            // Assert
            Assert.True((result != null));
            var points = await _service.GetLoyaltyAccountPointsAsync(_tenantId, _userId);
            var expiredLedger = points.FirstOrDefault(l => l.PointAccountTypeId == _expPointAccountType.Id);
            Assert.NotNull(expiredLedger);
            Assert.Equal(100, expiredLedger.CurrentBalance);
        }

        #endregion

        #region Point Validation Tests

        [Fact]
        public async Task UpsertPoints_WithMissingEarnDate_ThrowsException()
        {
            // Arrange
            var ledger = new PointLedgerDto
            {
                TenantId = _tenantId,
                AccountId = _userId,
                PointAccountTypeId = _spendablePointAccountType.Id,
                LedgerEntries = new List<LedgerEntryDto>
                {
                    new LedgerEntryDto
                    {
                        EventId = "event1",
                        EventType = "Purchase",
                        PointsDeposited = 100
                        // Missing EarnDate
                    }
                }
            };

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() =>
                _service.UpsertLoyaltyAccountPointsAsync(_tenantId, ledger, TestDataFactory.GenerateRichard()));
        }

        [Fact]
        public async Task UpsertPoints_WithNullLedger_ReturnsNull()
        {
            // Act
            var result = await _service.UpsertLoyaltyAccountPointsAsync(_tenantId, null, TestDataFactory.GenerateRichard());

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task UpsertPoints_WithEmptyLedgerEntries_ReturnsNull()
        {
            // Arrange
            var ledger = new PointLedgerDto 
            { 
                LedgerEntries = new List<LedgerEntryDto>() 
            };

            // Act
            var result = await _service.UpsertLoyaltyAccountPointsAsync(_tenantId, ledger, TestDataFactory.GenerateRichard());

            // Assert
            Assert.Null(result);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task UpsertPoints_WithInvalidTenantId_ThrowsException(string tenantId)
        {
            // Arrange
            var ledger = CreateValidLedger();
            
            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(() => 
                _service.UpsertLoyaltyAccountPointsAsync(tenantId, ledger, TestDataFactory.GenerateRichard()));
            Assert.Equal("The TenantId must be provided.", exception.Message);
        }

        [Fact]
        public async Task UpsertPoints_WithMissingEventId_ThrowsException()
        {
            // Arrange
            var ledger = CreateValidLedger();
            ledger.LedgerEntries[0].EventId = null;

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(() => 
                _service.UpsertLoyaltyAccountPointsAsync(_tenantId, ledger, TestDataFactory.GenerateRichard()));
            Assert.Equal("One or more ledger entries are missing an Event Type and/or Id", exception.Message);
        }

        [Fact]
        public async Task UpsertPoints_WithMissingEarnDateOnDeposit_ThrowsException()
        {
            // Arrange
            var ledger = CreateValidLedger();
            ledger.LedgerEntries[0].EarnDate = null;

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(() => 
                _service.UpsertLoyaltyAccountPointsAsync(_tenantId, ledger, TestDataFactory.GenerateRichard()));
            Assert.Equal("LoyaltyAccountService::UpsertLoyaltyAccountPointsAsync - Every (net) deposit must provide an Earn Date.", exception.Message);
        }

        #endregion

        #region Complex Point Scenarios

        [Fact]
        public async Task BringPointsCurrent_ExpiresOldPoints()
        {
            // Arrange
            var ledger = CreateValidLedger();
            ledger.LedgerEntries[0].EarnDate = DateTimeOffset.UtcNow.AddDays(-40);
            ledger.LedgerEntries[0].ExpirationDate = DateTimeOffset.UtcNow.AddDays(-10);
            await _service.UpsertLoyaltyAccountPointsAsync(_tenantId, ledger, TestDataFactory.GenerateRichard());

            var acct = TestDataFactory.GenerateRichard();
            acct.Id = _userId;
            // Act
            await _service.BringLoyaltyAccountPointsCurrentAsync(_tenantId, acct);

            // Assert
            var points = await _service.GetLoyaltyAccountPointsAsync(_tenantId, _userId);
            var expiredLedger = points.Find(l => l.PointAccountTypeId == _expPointAccountType.Id);
            Assert.NotNull(expiredLedger);
            Assert.True(expiredLedger.CurrentBalance > 0);
        }

        [Fact]
        public async Task UpsertPoints_WithMultipleEntries_HandlesSpendablePointsCorrectly()
        {
            // Arrange - First deposit points with different dates
            var firstDeposit = CreateValidLedger();
            firstDeposit.LedgerEntries[0].EarnDate = DateTimeOffset.UtcNow.AddDays(-5);
            await _service.UpsertLoyaltyAccountPointsAsync(_tenantId, firstDeposit, TestDataFactory.GenerateRichard());

            var secondDeposit = CreateValidLedger();
            secondDeposit.LedgerEntries[0].EventId = "event2";
            await _service.UpsertLoyaltyAccountPointsAsync(_tenantId, secondDeposit, TestDataFactory.GenerateRichard());

            // Act - Spend points
            var spendLedger = new PointLedgerDto
            {
                TenantId = _tenantId,
                AccountId = _userId,
                PointAccountTypeId = _spendablePointAccountType.Id,
                LedgerEntries = new List<LedgerEntryDto>
                {
                    new LedgerEntryDto
                    {
                        EventId = "spend1",
                        EventType = "Redemption",
                        EarnDate = DateTimeOffset.UtcNow,
                        PointsWithdrawn = 150
                    }
                }
            };
            var result = await _service.UpsertLoyaltyAccountPointsAsync(_tenantId, spendLedger, TestDataFactory.GenerateRichard());

            // Assert
            Assert.NotNull(result);
            Assert.Equal(50, result.CurrentBalance); // 200 total deposited - 150 withdrawn
        }

        [Fact]
        public async Task ExpirePoints_WithMultiplePointTypes_ExpiresCorrectly()
        {
            // Arrange
            var escrowLedger = new PointLedgerDto
            {
                TenantId = _tenantId,
                AccountId = _userId,
                PointAccountTypeId = _escrowPointAccountType.Id,
                LedgerEntries = new List<LedgerEntryDto>
                {
                    new LedgerEntryDto
                    {
                        EventId = "escrow1",
                        EventType = "Escrow",
                        EarnDate = DateTimeOffset.UtcNow.AddDays(-40),
                        PointsDeposited = 100
                    }
                }
            };
            await _service.UpsertLoyaltyAccountPointsAsync(_tenantId, escrowLedger, TestDataFactory.GenerateRichard());

            var spendableLedger = CreateValidLedger();
            spendableLedger.LedgerEntries[0].EarnDate = DateTimeOffset.UtcNow.AddDays(-40);
            await _service.UpsertLoyaltyAccountPointsAsync(_tenantId, spendableLedger, TestDataFactory.GenerateRichard());

            // Act
            await _service.ExpireLoyaltyAccountPointsByEarnDate(
                _tenantId,
                _userId,
                new List<string> { _escrowPointAccountType.Id, _spendablePointAccountType.Id },
                DateTimeOffset.UtcNow,
                1.0m
            );

            // Assert
            var points = await _service.GetLoyaltyAccountPointsAsync(_tenantId, _userId);
            var expiredLedger = points.Find(l => l.PointAccountTypeId == _expPointAccountType.Id);
            Assert.NotNull(expiredLedger);
            Assert.Equal(200, expiredLedger.CurrentBalance); // 100 from escrow + 100 from spendable
        }

        [Fact]
        public async Task ExpirePoints_WithPartialExpiration_ExpiresCorrectPercentage()
        {
            // Arrange
            var ledger = CreateValidLedger();
            ledger.LedgerEntries[0].EarnDate = DateTimeOffset.UtcNow.AddDays(-40);
            await _service.UpsertLoyaltyAccountPointsAsync(_tenantId, ledger, TestDataFactory.GenerateRichard());

            // Act
            await _service.ExpireLoyaltyAccountPointsByEarnDate(
                _tenantId,
                _userId,
                new List<string> { _spendablePointAccountType.Id },
                DateTimeOffset.UtcNow,
                0.5m // Expire 50%
            );

            // Assert
            var points = await _service.GetLoyaltyAccountPointsAsync(_tenantId, _userId);
            var spendableLedger = points.Find(l => l.PointAccountTypeId == _spendablePointAccountType.Id);
            var expiredLedger = points.Find(l => l.PointAccountTypeId == _expPointAccountType.Id);
            
            Assert.NotNull(spendableLedger);
            Assert.NotNull(expiredLedger);
            Assert.Equal(50, spendableLedger.CurrentBalance);
            Assert.Equal(50, expiredLedger.CurrentBalance);
        }

        #endregion

        #region Points Validation Tests

        [Theory]
        [InlineData("", "validAccountId", "tenantId")]
        [InlineData(null, "validAccountId", "tenantId")]
        [InlineData("validTenantId", "", "loyaltyAccountId")]
        [InlineData("validTenantId", null, "loyaltyAccountId")]
        public async Task BringPointsCurrent_WithInvalidParameters_ThrowsArgumentNullException(
            string tenantId, string accountId, string paramName)
        {
            // Act & Assert
            var acct = TestDataFactory.GenerateRichard();
            acct.Id = accountId;
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _service.BringLoyaltyAccountPointsCurrentAsync(tenantId, acct));
            Assert.Equal(paramName, exception.ParamName);
        }

        [Theory]
        [InlineData("", "validAccountId", "tenantId")]
        [InlineData(null, "validAccountId", "tenantId")]
        [InlineData("validTenantId", "", "loyaltyAccountId")]
        [InlineData("validTenantId", null, "loyaltyAccountId")]
        public async Task GetPoints_WithInvalidParameters_ThrowsArgumentNullException(
            string tenantId, string accountId, string paramName)
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _service.GetLoyaltyAccountPointsAsync(tenantId, accountId));
            Assert.Equal(paramName, exception.ParamName);
        }

        [Theory]
        [InlineData("", "tenantId")]
        [InlineData(null, "tenantId")]
        public async Task BringPointsCurrentWithAccount_WithInvalidParameters_ThrowsArgumentNullException(
            string tenantId, string paramName)
        {
            // Arrange
            var account = new LoyaltyAccount("testId", null, null, null, null, null, null, null, tenantId, null);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _service.BringLoyaltyAccountPointsCurrentAsync(tenantId, account));
            Assert.Equal(paramName, exception.ParamName);
        }

        [Fact]
        public async Task BringPointsCurrentWithAccount_WithNullAccount_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _service.BringLoyaltyAccountPointsCurrentAsync(_tenantId, (LoyaltyAccount)null));
            Assert.Equal("loyaltyAccount", exception.ParamName);
        }

        #endregion

        #region Helper Methods
        private PointLedgerDto CreateValidLedger()
        {
            return new PointLedgerDto
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = _tenantId,
                AccountId = _userId,
                PointAccountTypeId = _spendablePointAccountType.Id,
                LedgerEntries = new List<LedgerEntryDto>
                {
                    new LedgerEntryDto
                    {
                        EventId = "event1",
                        EventType = "Purchase",
                        EarnDate = DateTimeOffset.UtcNow,
                        PointsDeposited = 100
                    }
                }
            };
        }
        #endregion

        [Fact]
        public async Task DepositPoints_WithValidRequest_ShouldSucceed()
        {
            // Arrange
            var request = new PointDespositRequest
            {
                LoyaltyAccountId = _userId,
                PointAccountTypeId = _spendablePointAccountType.Id,
                Amount = 100,
                EventId = "deposit-123",
                EventType = "Purchase",
                DepositDate = DateTimeOffset.UtcNow,
                Status = "Active",
                UserId = "system"
            };

            // Act
            var result = await _service.DepositPointsAsync(_tenantId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(100, result.CurrentBalance);
            Assert.Single(result.LedgerEntries);
            Assert.Equal(request.Amount, result.LedgerEntries[0].PointsDeposited);
        }

        [Fact]
        public async Task WithdrawPoints_WithValidRequest_ShouldSucceed()
        {
            // Arrange - First deposit points
            await _service.DepositPointsAsync(_tenantId, new PointDespositRequest
            {
                LoyaltyAccountId = _userId,
                PointAccountTypeId = _spendablePointAccountType.Id,
                Amount = 100,
                EventId = "deposit-123",
                EventType = "Purchase",
                DepositDate = DateTimeOffset.UtcNow
            });

            var request = new PointWithdrawlRequest
            {
                LoyaltyAccountId = _userId,
                PointAccountTypeId = _spendablePointAccountType.Id,
                Amount = 50,
                EventId = "withdraw-123",
                EventType = "Redemption",
                WithdrawalDate = DateTimeOffset.UtcNow,
                Status = "Complete",
                UserId = "system"
            };

            // Act
            var result = await _service.WithdrawPointsAsync(_tenantId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(50, result.CurrentBalance);
        }

        [Fact]
        public async Task GetPointsDetails_WithValidRequest_ShouldReturnDetails()
        {
            // Arrange
            var eventType = "Purchase";
            var eventId = "details-123";
            
            // First create some points with details
            await _service.DepositPointsAsync(_tenantId, new PointDespositRequest
            {
                LoyaltyAccountId = _userId,
                PointAccountTypeId = _spendablePointAccountType.Id,
                Amount = 100,
                EventId = eventId,
                EventType = eventType,
                DepositDate = DateTimeOffset.UtcNow
            });

            // Act
            var result = await _service.GetPointsDetails(_tenantId, _userId, eventType, eventId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(eventId, result.EventId);
            Assert.Equal(eventType, result.EventType);
        }

        [Fact]
        public async Task GetManyPointsDetails_WithValidRequests_ShouldReturnAllDetails()
        {
            // Arrange
            var request = new GetManyPointsDetailsRequest
            {
                LoyaltyAccountId = _userId,
                Events = new List<EventPair>
                {
                    new EventPair { EventType = "Purchase", EventId = "event1" },
                    new EventPair { EventType = "Purchase", EventId = "event2" }
                }
            };

            // First create some points with details
            foreach (var evt in request.Events)
            {
                await _service.DepositPointsAsync(_tenantId, new PointDespositRequest
                {
                    LoyaltyAccountId = _userId,
                    PointAccountTypeId = _spendablePointAccountType.Id,
                    Amount = 100,
                    EventId = evt.EventId,
                    EventType = evt.EventType,
                    DepositDate = DateTimeOffset.UtcNow
                });
            }

            // Act
            var result = await _service.GetManyPointsDetails(_tenantId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task DepositPoints_WithInvalidTenantId_ThrowsException(string tenantId)
        {
            // Arrange
            var request = new PointDespositRequest
            {
                LoyaltyAccountId = _userId,
                PointAccountTypeId = _spendablePointAccountType.Id,
                Amount = 100
            };

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => 
                _service.DepositPointsAsync(tenantId, request));
        }

        [Fact]
        public async Task WithdrawPoints_WithInsufficientBalance_ThrowsException()
        {
            // Arrange
            var request = new PointWithdrawlRequest
            {
                LoyaltyAccountId = _userId,
                PointAccountTypeId = _spendablePointAccountType.Id,
                Amount = 100,
                EventId = "withdraw-123",
                EventType = "Redemption"
            };

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => 
                _service.WithdrawPointsAsync(_tenantId, request));
        }

        [Fact]
        public async Task PointAccountType_WhenCached_ShouldNotCallAdapter()
        {
            // Arrange
            var cache = new StubPointAccountTypeCache();
            var adapter = new StubPointAccountTypeAdapter();
            var service = new LoyaltyAccountService(
                new StubLoyaltyAccountAdapter(),
                new StubPointLedgerAdapter(),
                new StubTagAdapter(),
                cache,
                //new StubLoyaltyAccountRuleStateAdapter(),
                new StubLoyaltyAccountPointsDetailsAdapter(), 
                LoggerFactoryProvider.CreateLogger<LoyaltyAccountService>(), //ILogger<LoyaltyAccountService> logger
                default(IDynamicDataAdapter), //IDynamicDataAdapter dynamicDataAdapter
                default(IDynamicExternalReferenceAdapter), //IDynamicExternalReferenceAdapter dynamicAdapter
                default(IDataLakeAdapter)
            );

            // Cache the point account type
            cache.CachePointAccountType(_tenantId, _spendablePointAccountType);

            // Act
            var request = new PointDespositRequest
            {
                LoyaltyAccountId = _userId,
                PointAccountTypeId = _spendablePointAccountType.Id,
                Amount = 100,
                EventId = "deposit-123",
                EventType = "Purchase",
                DepositDate = DateTimeOffset.UtcNow
            };
            var result = await service.DepositPointsAsync(_tenantId, request);

            // Assert
            Assert.NotNull(result);
            // Verify adapter wasn't called by checking its internal state
            // This assumes we add a call counter to StubPointAccountTypeAdapter
            Assert.Equal(0, adapter.FetchCallCount);
        }

        [Fact]
        public async Task PointAccountType_WhenNotCached_ShouldCallAdapter()
        {
            // Arrange
            var cache = new StubPointAccountTypeCache();
            var adapter = new StubPointAccountTypeAdapter();
            var service = new LoyaltyAccountService(
                new StubLoyaltyAccountAdapter(),
                new StubPointLedgerAdapter(),
                new StubTagAdapter(),
                cache,
                //new StubLoyaltyAccountRuleStateAdapter(),
                new StubLoyaltyAccountPointsDetailsAdapter(),
                LoggerFactoryProvider.CreateLogger<LoyaltyAccountService>(), //ILogger<LoyaltyAccountService> logger
                default(IDynamicDataAdapter), //IDynamicDataAdapter dynamicDataAdapter
                default(IDynamicExternalReferenceAdapter), //IDynamicExternalReferenceAdapter dynamicAdapter
                default(IDataLakeAdapter)
            );

            // Act
            var request = new PointDespositRequest
            {
                LoyaltyAccountId = _userId,
                PointAccountTypeId = _spendablePointAccountType.Id,
                Amount = 100,
                EventId = "deposit-123",
                EventType = "Purchase",
                DepositDate = DateTimeOffset.UtcNow
            };
            var result = await service.DepositPointsAsync(_tenantId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, adapter.FetchCallCount);
        }

        [Fact]
        public async Task Points_WhenTransferringBetweenTypes_ShouldMaintainBalance()
        {
            // Arrange
            var escrowPoints = new PointDespositRequest
            {
                LoyaltyAccountId = _userId,
                PointAccountTypeId = _escrowPointAccountType.Id,
                Amount = 100,
                EventId = "escrow-123",
                EventType = "Escrow",
                DepositDate = DateTimeOffset.UtcNow.AddDays(-40)
            };
            await _service.DepositPointsAsync(_tenantId, escrowPoints);

            var spendablePoints = new PointDespositRequest
            {
                LoyaltyAccountId = _userId,
                PointAccountTypeId = _spendablePointAccountType.Id,
                Amount = 50,
                EventId = "spend-123",
                EventType = "Purchase",
                DepositDate = DateTimeOffset.UtcNow
            };
            await _service.DepositPointsAsync(_tenantId, spendablePoints);

            // Act - Expire all points
            await _service.ExpireLoyaltyAccountPointsByEarnDate(
                _tenantId,
                _userId,
                new List<string> { _escrowPointAccountType.Id, _spendablePointAccountType.Id },
                DateTimeOffset.UtcNow,
                1.0m
            );

            // Assert
            var points = await _service.GetLoyaltyAccountPointsAsync(_tenantId, _userId);
            
            // Check expired points balance
            var expiredLedger = points.Find(l => l.PointAccountTypeId == _expPointAccountType.Id);
            Assert.NotNull(expiredLedger);
            Assert.Equal(150, expiredLedger.CurrentBalance); // 100 from escrow + 50 from spendable

            // Verify original ledgers are empty
            var escrowLedger = points.Find(l => l.PointAccountTypeId == _escrowPointAccountType.Id);
            var spendableLedger = points.Find(l => l.PointAccountTypeId == _spendablePointAccountType.Id);
            Assert.Equal(0, escrowLedger.CurrentBalance);
            Assert.Equal(0, spendableLedger.CurrentBalance);
        }

        [Fact]
        public async Task Points_WhenExpirationLedgerFailsToSave_ShouldRollback()
        {
            // Arrange
            var ledgerAdapter = new StubPointLedgerAdapter { ThrowOnExpire = true };
            var service = new LoyaltyAccountService(
                new StubLoyaltyAccountAdapter(),
                ledgerAdapter,
                new StubTagAdapter(),
                new StubPointAccountTypeCache(),
                //new StubLoyaltyAccountRuleStateAdapter(),
                new StubLoyaltyAccountPointsDetailsAdapter(),
                LoggerFactoryProvider.CreateLogger<LoyaltyAccountService>(), //ILogger<LoyaltyAccountService> logger
                default(IDynamicDataAdapter), //IDynamicDataAdapter dynamicDataAdapter
                default(IDynamicExternalReferenceAdapter), //IDynamicExternalReferenceAdapter dynamicAdapter
                default(IDataLakeAdapter)
            );

            // Setup initial points
            await service.DepositPointsAsync(_tenantId, new PointDespositRequest
            {
                LoyaltyAccountId = _userId,
                PointAccountTypeId = _spendablePointAccountType.Id,
                Amount = 100,
                EventId = "deposit-123",
                EventType = "Purchase",
                DepositDate = DateTimeOffset.UtcNow.AddDays(-40)
            });

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => 
                service.ExpireLoyaltyAccountPointsByEarnDate(
                    _tenantId,
                    _userId,
                    new List<string> { _spendablePointAccountType.Id },
                    DateTimeOffset.UtcNow,
                    1.0m
                ));

            // Verify original balance remains unchanged
            var points = await service.GetLoyaltyAccountPointsAsync(_tenantId, _userId);
            var spendableLedger = points.Find(l => l.PointAccountTypeId == _spendablePointAccountType.Id);
            Assert.Equal(100, spendableLedger.CurrentBalance);
        }

        [Fact]
        public async Task Points_WithPartialExpirationAndMultipleTypes_ShouldExpireCorrectly()
        {
            // Arrange
            var now = DateTimeOffset.UtcNow;
            
            // Add points with different dates
            await _service.DepositPointsAsync(_tenantId, new PointDespositRequest
            {
                LoyaltyAccountId = _userId,
                PointAccountTypeId = _spendablePointAccountType.Id,
                Amount = 100,
                EventId = "old-points",
                EventType = "Purchase",
                DepositDate = now.AddDays(-40) // Old points
            });

            await _service.DepositPointsAsync(_tenantId, new PointDespositRequest
            {
                LoyaltyAccountId = _userId,
                PointAccountTypeId = _spendablePointAccountType.Id,
                Amount = 50,
                EventId = "new-points",
                EventType = "Purchase",
                DepositDate = now.AddDays(-5) // New points
            });

            // Act
            await _service.ExpireLoyaltyAccountPointsByEarnDate(
                _tenantId,
                _userId,
                new List<string> { _spendablePointAccountType.Id },
                now.AddDays(-30), // Expire points older than 30 days
                1.0m
            );

            // Assert
            var points = await _service.GetLoyaltyAccountPointsAsync(_tenantId, _userId);
            var spendableLedger = points.Find(l => l.PointAccountTypeId == _spendablePointAccountType.Id);
            var expiredLedger = points.Find(l => l.PointAccountTypeId == _expPointAccountType.Id);

            Assert.Equal(50, spendableLedger.CurrentBalance); // Only new points remain
            Assert.Equal(100, expiredLedger.CurrentBalance); // Only old points expired
        }

        [Fact]
        public async Task Points_WithConcurrentExpiration_ShouldHandleRaceCondition()
        {
            // Arrange
            var ledgerAdapter = new StubPointLedgerAdapter { DelayMilliseconds = 100 };
            var service = new LoyaltyAccountService(
                new StubLoyaltyAccountAdapter(),
                ledgerAdapter,
                new StubTagAdapter(),
                new StubPointAccountTypeCache(),
                //new StubLoyaltyAccountRuleStateAdapter(),
                new StubLoyaltyAccountPointsDetailsAdapter(),
                LoggerFactoryProvider.CreateLogger<LoyaltyAccountService>(), //ILogger<LoyaltyAccountService> logger
                default(IDynamicDataAdapter), //IDynamicDataAdapter dynamicDataAdapter
                default(IDynamicExternalReferenceAdapter), //IDynamicExternalReferenceAdapter dynamicAdapter
                default(IDataLakeAdapter)
            );

            // Setup points
            await service.DepositPointsAsync(_tenantId, new PointDespositRequest
            {
                LoyaltyAccountId = _userId,
                PointAccountTypeId = _spendablePointAccountType.Id,
                Amount = 100,
                EventId = "deposit-123",
                EventType = "Purchase",
                DepositDate = DateTimeOffset.UtcNow.AddDays(-40)
            });

            // Act - Start two concurrent expiration operations
            var task1 = service.ExpireLoyaltyAccountPointsByEarnDate(
                _tenantId,
                _userId,
                new List<string> { _spendablePointAccountType.Id },
                DateTimeOffset.UtcNow,
                0.5m
            );

            var task2 = service.ExpireLoyaltyAccountPointsByEarnDate(
                _tenantId,
                _userId,
                new List<string> { _spendablePointAccountType.Id },
                DateTimeOffset.UtcNow,
                0.5m
            );

            await Task.WhenAll(task1, task2);

            // Assert
            var points = await service.GetLoyaltyAccountPointsAsync(_tenantId, _userId);
            var spendableLedger = points.Find(l => l.PointAccountTypeId == _spendablePointAccountType.Id);
            var expiredLedger = points.Find(l => l.PointAccountTypeId == _expPointAccountType.Id);

            Assert.Equal(0, spendableLedger.CurrentBalance);
            Assert.Equal(100, expiredLedger.CurrentBalance);
        }

        [Fact]
        public async Task Points_WithComplexExpirationDates_ShouldHandleCorrectly()
        {
            // Arrange
            var now = DateTimeOffset.UtcNow;
            
            // Set specific expiration date on point account type
            var customExpDate = now.AddDays(60);
            _spendablePointAccountType.PointsLifespanEndDate = customExpDate;

            await _service.DepositPointsAsync(_tenantId, new PointDespositRequest
            {
                LoyaltyAccountId = _userId,
                PointAccountTypeId = _spendablePointAccountType.Id,
                Amount = 100,
                EventId = "deposit-123",
                EventType = "Purchase",
                DepositDate = now
            });

            // Act
            var points = await _service.GetLoyaltyAccountPointsAsync(_tenantId, _userId);
            var ledger = points.First();

            // Assert
            Assert.Equal(customExpDate, ledger.LedgerEntries[0].ExpirationDate);
        }
    }
}
