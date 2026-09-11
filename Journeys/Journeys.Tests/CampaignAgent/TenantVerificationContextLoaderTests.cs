using Backend.Dto.Requests;
using Backend.Dto.Structures.Tenant;
using Journeys.API.CampaignAgent;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.DTO.Responses;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public class TenantVerificationContextLoaderTests
{
    [Fact]
    public async Task LoadAsync_returns_allowlist_from_tenant()
    {
        var adapter = new CountingTenantAdapter(_ => new TenantDto
        {
            Name = "primo",
            CampaignTestAccountExtIds = ["test_exp_01", "Test_Exp_02"]
        });

        var loader = CreateLoader(adapter);
        var result = await loader.LoadAsync("primo");

        Assert.True(result.LoadSucceeded);
        Assert.False(result.BlockedNoAllowlist);
        Assert.Equal(2, result.Allowlist.Count);
        Assert.Contains("test_exp_01", result.Allowlist, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadAsync_empty_allowlist_sets_blocked()
    {
        var adapter = new CountingTenantAdapter(_ => new TenantDto
        {
            Name = "primo",
            CampaignTestAccountExtIds = []
        });

        var loader = CreateLoader(adapter);
        var result = await loader.LoadAsync("primo");

        Assert.True(result.LoadSucceeded);
        Assert.True(result.BlockedNoAllowlist);
        Assert.Empty(result.Allowlist);
    }

    [Fact]
    public async Task LoadAsync_uses_cache_on_second_call()
    {
        var adapter = new CountingTenantAdapter(_ => new TenantDto
        {
            Name = "primo",
            CampaignTestAccountExtIds = ["a"]
        });

        var loader = CreateLoader(adapter);
        _ = await loader.LoadAsync("primo");
        _ = await loader.LoadAsync("primo");

        Assert.Equal(1, adapter.GetByNameCallCount);
    }

    private static TenantVerificationContextLoader CreateLoader(ITenantDataAdapter adapter)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CampaignAgent:TenantLookupByName"] = "true",
                ["CampaignAgent:TenantContextCacheMinutes"] = "3"
            })
            .Build();
        return new TenantVerificationContextLoader(
            adapter,
            new MemoryCache(new MemoryCacheOptions()),
            config,
            NullLogger<TenantVerificationContextLoader>.Instance);
    }

    private sealed class CountingTenantAdapter : ITenantDataAdapter
    {
        private readonly Func<string, TenantDto?> _getByName;

        public CountingTenantAdapter(Func<string, TenantDto?> getByName) => _getByName = getByName;

        public int GetByNameCallCount { get; private set; }

        public Task<TenantDto?> GetTenantByNameAsync(
            string name,
            bool includeDeleted = false,
            CancellationToken cancellationToken = default,
            JsonSerializerOptions? serializerOptions = null)
        {
            GetByNameCallCount++;
            return Task.FromResult(_getByName(name));
        }

        public Task<TenantDto?> GetTenantAsync(
            string id,
            bool includeDeleted = false,
            CancellationToken cancellationToken = default,
            JsonSerializerOptions? serializerOptions = null) =>
            throw new NotSupportedException();

        public Task<ContinuableList<TenantDto>> GetManyTenantsAsync(
            GetManyTenantsRequest request,
            CancellationToken cancellationToken = default,
            JsonSerializerOptions? serializerOptions = null) =>
            throw new NotSupportedException();

        public Task<ContinuableList<TenantDto>> GetAllTenantsAsync(
            GetAllTenantsRequest request,
            CancellationToken cancellationToken = default,
            JsonSerializerOptions? serializerOptions = null) =>
            throw new NotSupportedException();

        public Task<TenantDto> SaveTenantAsync(
            TenantDto tenantDto,
            CancellationToken cancellationToken = default,
            JsonSerializerOptions? serializerOptions = null) =>
            throw new NotSupportedException();

        public Task<TenantDto?> SoftDeleteTenantAsync(
            string id,
            CancellationToken cancellationToken = default,
            JsonSerializerOptions? serializerOptions = null) =>
            throw new NotSupportedException();
    }
}
