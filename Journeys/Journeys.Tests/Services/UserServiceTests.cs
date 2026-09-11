using Journeys.Core;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.FileStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.Core.Services;
using Journeys.DTO.Models;
using Journeys.Tests.Stubs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Journeys.Tests.Services
{
    public class UserServiceTests
    {
        private readonly ILoyaltyAccountService _service;
        private readonly string _tenantId = TestDataFactory.TENANT_ID;
        private readonly string _userId = "user1";

        public UserServiceTests()
        {
            var cache = new StubPointAccountTypeCache();

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

        #region Account Management Tests

        [Theory]
        [InlineData("", "validAccountId", "tenantId")]
        [InlineData(null, "validAccountId", "tenantId")]
        [InlineData("validTenantId", "", "accountId")]
        [InlineData("validTenantId", null, "accountId")]
        public async Task GetUser_WithInvalidParameters_ThrowsArgumentNullException(string tenantId, string accountId, string paramName)
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _service.GetLoyaltyAccountAsync(tenantId, accountId));
            Assert.Equal(paramName, exception.ParamName);
        }

        [Fact]
        public async Task GetUser_WithValidId_ReturnsUser()
        {
            // Arrange
            var user = new LoyaltyAccountDto
            {
                Id = _userId,
                TenantId = _tenantId,
                ExtAccountId = "ext123"
            };
            await _service.UpsertLoyaltyAccountAsync(_tenantId, user);

            // Act
            var result = await _service.GetLoyaltyAccountAsync(_tenantId, _userId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(_userId, result.Id);
            Assert.Equal("ext123", result.ExtAccountId);
        }

        [Theory]
        [InlineData("", "validExtId", "tenantId")]
        [InlineData(null, "validExtId", "tenantId")]
        [InlineData("validTenantId", "", "extId")]
        [InlineData("validTenantId", null, "extId")]
        public async Task GetUserByExtId_WithInvalidParameters_ThrowsArgumentNullException(string tenantId, string extId, string paramName)
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _service.GetLoyaltyAccountByExtIdAsync(tenantId, extId));
            Assert.Equal(paramName, exception.ParamName);
        }

        [Fact]
        public async Task GetUserByExtId_WithValidExtId_ReturnsUser()
        {
            // Arrange
            var user = new LoyaltyAccountDto
            {
                Id = _userId,
                TenantId = _tenantId,
                ExtAccountId = "ext123",
            };
            await _service.UpsertLoyaltyAccountAsync(_tenantId, user);

            // Act
            var result = await _service.GetLoyaltyAccountByExtIdAsync(_tenantId, "ext123");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(_userId, result.Id);
            Assert.Equal("ext123", result.ExtAccountId);
        }

        [Theory]
        [InlineData("", "tenantId")]
        [InlineData(null, "tenantId")]
        public async Task UpsertUser_WithInvalidTenantId_ThrowsArgumentNullException(string tenantId, string paramName)
        {
            // Arrange
            var user = new LoyaltyAccountDto { Id = _userId };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _service.UpsertLoyaltyAccountAsync(tenantId, user));
            Assert.Equal(paramName, exception.ParamName);
        }

        [Fact]
        public async Task UpsertUser_WithNullAccount_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _service.UpsertLoyaltyAccountAsync(_tenantId, null));
            Assert.Equal("account", exception.ParamName);
        }

        [Theory]
        [InlineData("", "validAccountId", "tenantId")]
        [InlineData(null, "validAccountId", "tenantId")]
        [InlineData("validTenantId", "", "accountId")]
        [InlineData("validTenantId", null, "accountId")]
        public async Task DeleteUser_WithInvalidParameters_ThrowsArgumentNullException(string tenantId, string accountId, string paramName)
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _service.DeleteLoyaltyAccountAsync(tenantId, accountId));
            Assert.Equal(paramName, exception.ParamName);
        }

        [Theory]
        [InlineData("", "tenantId")]
        [InlineData(null, "tenantId")]
        public async Task DeleteUserWithAccount_WithInvalidTenantId_ThrowsArgumentNullException(string tenantId, string paramName)
        {
            // Arrange
            var user = new LoyaltyAccountDto { Id = _userId };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _service.DeleteLoyaltyAccountAsync(tenantId, user));
            Assert.Equal(paramName, exception.ParamName);
        }

        [Fact]
        public async Task DeleteUserWithAccount_WithNullAccount_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _service.DeleteLoyaltyAccountAsync(_tenantId, (LoyaltyAccountDto)null));
            Assert.Equal("account", exception.ParamName);
        }

        [Fact]
        public async Task UpsertUser_UpdatesExistingUser()
        {
            // Arrange
            var user = new LoyaltyAccountDto
            {
                Id = _userId,
                TenantId = _tenantId,
                ExtAccountId = "ext123",
            };
            await _service.UpsertLoyaltyAccountAsync(_tenantId, user);

            // Update user
            user.Type = "Inactive";

            // Act
            var result = await _service.UpsertLoyaltyAccountAsync(_tenantId, user);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Inactive", result.Type);
        }

        [Fact]
        public async Task UpsertLoyaltyAccount_WithExistingJourneyState_PreservesState()
        {
            // Arrange
            var existingAccount = new LoyaltyAccountDto
            {
                Id = _userId,
                TenantId = _tenantId,
                ExtAccountId = "ext-123",
                Journeys = new List<LoyaltyAccountJourneyDto>
                {
                    new LoyaltyAccountJourneyDto
                    {
                        RootJourneyNodeId = "campaign-1",
                        JourneyNodeIds = new List<string> { "node-1" }
                    }
                }
            };
            await _service.UpsertLoyaltyAccountAsync(_tenantId, existingAccount);

            var updateAccount = new LoyaltyAccountDto
            {
                Id = _userId,
                TenantId = _tenantId,
                ExtAccountId = "ext-123-updated",
                Type = "Updated"
            };

            // Act
            var result = await _service.UpsertLoyaltyAccountAsync(_tenantId, updateAccount);

            // Assert
            Assert.NotNull(result.Journeys);
            Assert.Single(result.Journeys);
            Assert.Equal("campaign-1", result.Journeys[0].RootJourneyNodeId);
            Assert.Contains("node-1", result.Journeys[0].JourneyNodeIds);
        }

        [Fact]
        public async Task GetLoyaltyAccountByExtId_WithValidId_ReturnsAccount()
        {
            // Arrange
            var account = new LoyaltyAccountDto
            {
                Id = _userId,
                TenantId = _tenantId,
                ExtAccountId = "ext-123"
            };
            await _service.UpsertLoyaltyAccountAsync(_tenantId, account);

            // Act
            var result = await _service.GetLoyaltyAccountByExtIdAsync(_tenantId, "ext-123");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("ext-123", result.ExtAccountId);
        }

        [Fact]
        public async Task GetLoyaltyAccounts_WithFilters_ReturnsFilteredAccounts()
        {
            // Arrange
            var accounts = new[]
            {
                new LoyaltyAccountDto { Id = "user1", Type = "Active", TenantId = _tenantId },
                new LoyaltyAccountDto { Id = "user2", Type = "Inactive", TenantId = _tenantId },
                new LoyaltyAccountDto { Id = "user3", Type = "Active", TenantId = _tenantId }
            };

            foreach (var account in accounts)
            {
                await _service.UpsertLoyaltyAccountAsync(_tenantId, account);
            }

            // Act
            var result = await _service.GetLoyaltyAccountsAsync(_tenantId, new List<string> { "Status eq 'Active'" });

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.All(result, a => Assert.Equal("Active", a.Type));
        }

        #endregion

        #region Tag Management Tests

        [Theory]
        [InlineData("", "tenantId")]
        [InlineData(null, "tenantId")]
        public async Task TagUser_WithInvalidTenantId_ThrowsArgumentNullException(string tenantId, string paramName)
        {
            // Arrange
            var tag = new TagDto { EntityId = _userId };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _service.TagLoyaltyAccountAsync(tenantId, tag));
            Assert.Equal(paramName, exception.ParamName);
        }

        [Fact]
        public async Task TagUser_WithNullTag_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _service.TagLoyaltyAccountAsync(_tenantId, null));
            Assert.Equal("tag", exception.ParamName);
        }

        [Theory]
        [InlineData("", "validAccountId", "validType", "tenantId")]
        [InlineData(null, "validAccountId", "validType", "tenantId")]
        [InlineData("validTenantId", "", "validType", "loyaltyAccountId")]
        [InlineData("validTenantId", null, "validType", "loyaltyAccountId")]
        [InlineData("validTenantId", "validAccountId", "", "type")]
        [InlineData("validTenantId", "validAccountId", null, "type")]
        public async Task GetUserTags_WithInvalidParameters_ThrowsArgumentNullException(
            string tenantId, string accountId, string type, string paramName)
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _service.GetLoyaltyAccountTagsAsync(tenantId, accountId, type, 100));
            Assert.Equal(paramName, exception.ParamName);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task GetUserTags_WithInvalidPageSize_ThrowsArgumentException(int pageSize)
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => 
                _service.GetLoyaltyAccountTagsAsync(_tenantId, _userId, "UserTag", pageSize));
            Assert.Equal("pageSize", exception.ParamName);
        }

        [Fact]
        public async Task TagUser_WithValidTag_AddsTag()
        {
            // Arrange
            var tag = new TagDto
            {
                EntityId = _userId,
                TenantId = _tenantId,
                Type = "UserTag",
                Value = "VIP"
            };

            // Act
            var result = await _service.TagLoyaltyAccountAsync(_tenantId, tag);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("VIP", result.Value);
        }

        [Fact]
        public async Task GetUserTags_ReturnsUserTags()
        {
            // Arrange
            var tag1 = new TagDto
            {
                EntityId = _userId,
                TenantId = _tenantId,
                Type = "UserTag",
                Value = "VIP"
            };
            var tag2 = new TagDto
            {
                EntityId = _userId,
                TenantId = _tenantId,
                Type = "UserTag",
                Value = "Loyalty"
            };
            await _service.TagLoyaltyAccountAsync(_tenantId, tag1);
            await _service.TagLoyaltyAccountAsync(_tenantId, tag2);

            // Act
            var result = await _service.GetLoyaltyAccountTagsAsync(_tenantId, _userId, "UserTag", 100);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Entities.Count);
            Assert.Contains(result.Entities, t => t.Value == "VIP");
            Assert.Contains(result.Entities, t => t.Value == "Loyalty");
        }

        [Fact]
        public async Task TagLoyaltyAccount_WithMultipleTags_HandlesCorrectly()
        {
            // Arrange
            var account = new LoyaltyAccountDto
            {
                Id = _userId,
                TenantId = _tenantId
            };
            await _service.UpsertLoyaltyAccountAsync(_tenantId, account);

            var tags = new[]
            {
                new TagDto { EntityId = _userId, Type = "UserTag", Value = "VIP", TenantId = _tenantId },
                new TagDto { EntityId = _userId, Type = "UserTag", Value = "Loyalty", TenantId = _tenantId },
                new TagDto { EntityId = _userId, Type = "ProgramTag", Value = "Beta", TenantId = _tenantId }
            };

            // Act
            foreach (var tag in tags)
            {
                await _service.TagLoyaltyAccountAsync(_tenantId, tag);
            }

            // Assert - Check different tag types
            var userTags = await _service.GetLoyaltyAccountTagsAsync(_tenantId, _userId, "UserTag", 100);
            var programTags = await _service.GetLoyaltyAccountTagsAsync(_tenantId, _userId, "ProgramTag", 100);

            Assert.Equal(2, userTags.Entities.Count);
            Assert.Single(programTags.Entities);
        }

        [Fact]
        public async Task UpsertLoyaltyAccount_WithConcurrentUpdates_HandlesRaceCondition()
        {
            // Arrange
            var account = new LoyaltyAccountDto
            {
                Id = _userId,
                TenantId = _tenantId,
            };

            // Act - Simulate concurrent updates
            var task1 = _service.UpsertLoyaltyAccountAsync(_tenantId, new LoyaltyAccountDto
            {
                Id = _userId,
                TenantId = _tenantId,
                Type = "type1",
            });

            var task2 = _service.UpsertLoyaltyAccountAsync(_tenantId, new LoyaltyAccountDto
            {
                Id = _userId,
                TenantId = _tenantId,
                Type = "type2",
            });

            await Task.WhenAll(task1, task2);

            // Assert
            var result = await _service.GetLoyaltyAccountAsync(_tenantId, _userId);
            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetLoyaltyAccountTags_WithPagination_ReturnsCorrectPages()
        {
            // Arrange
            var account = new LoyaltyAccountDto
            {
                Id = _userId,
                TenantId = _tenantId
            };
            await _service.UpsertLoyaltyAccountAsync(_tenantId, account);

            // Create 25 tags
            for (int i = 1; i <= 25; i++)
            {
                await _service.TagLoyaltyAccountAsync(_tenantId, new TagDto
                {
                    EntityId = _userId,
                    Type = "UserTag",
                    Value = $"Tag{i}",
                    TenantId = _tenantId
                });
            }

            // Act - Get first page
            var firstPage = await _service.GetLoyaltyAccountTagsAsync(_tenantId, _userId, "UserTag", 10);
            
            // Get second page using continuation token
            var secondPage = await _service.GetLoyaltyAccountTagsAsync(_tenantId, _userId, "UserTag", 10, firstPage.ContinuationToken);

            // Assert
            Assert.Equal(10, firstPage.Entities.Count);
            Assert.NotNull(firstPage.ContinuationToken);
            Assert.Equal(10, secondPage.Entities.Count);
        }

        #endregion
    }
}