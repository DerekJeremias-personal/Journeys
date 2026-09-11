using Backend.Dto.Structures.Model;
using Journeys.Core.Configuration;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.Core.Services;
using Journeys.Core.Utility;
using Journeys.DTO.Requests;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Journeys.Tests.Services;

public class CampaignServiceDeletePatTests
{
    private static readonly ILogger<CampaignService> Logger = new NopLogger<CampaignService>();
    private static readonly CampaignDefinitionValidator CampaignDefinitionValidator =
        CampaignTestServices.CreateDefinitionValidator(new StubModelAdapter());

    [Fact]
    public async Task DeletePointAccountTypeAsync_calls_adapter_then_invalidates_cache()
    {
        var adapter = new RecordingPatAdapter();
        var cache = new RecordingPatCache();
        var campaignAdapter = new ThrowingCampaignAdapter();
        var sut = new CampaignService(campaignAdapter, adapter, cache, Logger, CampaignDefinitionValidator, CampaignTestServices.CreateValidationOrchestrator());

        await sut.DeletePointAccountTypeAsync("t1", "pat-99");

        Assert.Single(adapter.DeleteByIdCalls);
        Assert.Equal(("t1", "pat-99"), adapter.DeleteByIdCalls[0]);
        Assert.Single(cache.InvalidateOneCalls);
        Assert.Equal(("t1", "pat-99"), cache.InvalidateOneCalls[0]);
    }

    [Fact]
    public async Task DeletePointAccountTypeAsync_throws_when_id_empty()
    {
        var sut = new CampaignService(
            new ThrowingCampaignAdapter(),
            new RecordingPatAdapter(),
            new RecordingPatCache(),
            Logger,
            CampaignDefinitionValidator,
            CampaignTestServices.CreateValidationOrchestrator());

        await Assert.ThrowsAsync<ArgumentException>(() => sut.DeletePointAccountTypeAsync("t1", "  "));
    }

    private sealed class StubModelAdapter : IModelAdapter
    {
        public Task<PagedResultSet<ModelDto>> GetModels(string tenantId, string groupingType, int pageSize = 10, string? continuationToken = null, CancellationToken? token = null) =>
            throw new NotImplementedException();

        public Task RemoveModelAsync(string tenantId, string modelId, string modelType, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<ModelDto?> GetModelAsync(string tenantId, string modelId, string modelType, bool includeChildModels = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<ModelDto?>(null);
    }

    private sealed class NopLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    private sealed class RecordingPatAdapter : IPointAccountTypeAdapter
    {
        public List<(string TenantId, string Id)> DeleteByIdCalls { get; } = new();

        public Task DeletePointAccountTypeAsync(string tenantId, string id)
        {
            DeleteByIdCalls.Add((tenantId, id));
            return Task.CompletedTask;
        }

        public Task DeletePointAccountTypeAsync(string tenantId, PointAccountType acctType) =>
            throw new NotImplementedException();

        public Task<PointAccountType> FetchPointAccountTypeAsync(string tenantId, string entityId) =>
            throw new NotImplementedException();

        public Task<PagedResultSet<PointAccountType>> GetAllPointAccountTypesAsync(string tenantId, int pageSize, string continuationToken = null) =>
            throw new NotImplementedException();

        public Task<PointAccountType> UpsertPointAccountTypeAsync(string tenantId, PointAccountType acctType) =>
            throw new NotImplementedException();
    }

    private sealed class RecordingPatCache : IPointAccountTypeCache
    {
        public List<(string TenantId, string Id)> InvalidateOneCalls { get; } = new();
        public List<string> InvalidateTenantCalls { get; } = new();

        public bool CachePointAccountType(string tenantId, PointAccountType pointAccountType) => throw new NotImplementedException();

        public Task<bool> EnsurePATsLoaded(string tenantId) => throw new NotImplementedException();

        public Task<List<PointAccountType>> GetAllPointAccountTypes(string tenantId) => throw new NotImplementedException();

        public Task<PointAccountType> GetPointAccountTypeAsync(string tenantId, string pointAccountTypeId) =>
            throw new NotImplementedException();

        public Task InvalidatePointAccountTypeAsync(string tenantId, string pointAccountTypeId)
        {
            InvalidateOneCalls.Add((tenantId, pointAccountTypeId));
            return Task.CompletedTask;
        }

        public Task InvalidateTenantPointAccountTypesAsync(string tenantId)
        {
            InvalidateTenantCalls.Add(tenantId);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingCampaignAdapter : ICampaignAdapter
    {
        public Task DeleteCampaignAsync(string tenantId, string campaignId, string status) => throw new NotImplementedException();
        public Task DeleteCampaignAsync(string tenantId, Campaign Campaign) => throw new NotImplementedException();
        public Task<Campaign> FetchCampaignAsync(string tenantId, string campaignId, string status) => throw new NotImplementedException();
        public Task<PagedResultSet<Campaign>> GetArchivedCampaignsByDateRangeAsync(string tenantId, DateTimeOffset? fromDate, DateTimeOffset? toDate, int pageSize, string? continuationToken = null) => throw new NotImplementedException();
        public Task<Campaign> GetDraftCampaignByExtIdAsync(string tenantId, string extCampaignId) => throw new NotImplementedException();
        public Task<Campaign> GetLiveCampaignByExtIdAsync(string tenantId, string extCampaignId) => throw new NotImplementedException();
        public Task<List<Campaign>> GetCampaignVersionsByExtIdAsync(string tenantId, string extCampaignId) => throw new NotImplementedException();
        public Task<PagedResultSet<Campaign>> GetCampaignsByFiltersAsync(string tenantId, GetCampaignsByFilterRequest req) => throw new NotImplementedException();
        public Task<PagedResultSet<Campaign>> GetCampaignsByStatusAsync(string tenantId, string status, int pageSize, string? continuationToken = null) => throw new NotImplementedException();
        public Task<List<Campaign>> GetCampaignsAsync(string tenantId, List<string> ids, string status) => throw new NotImplementedException();
        public Task<PagedResultSet<Campaign>> GetAllCampaignsAsync(string tenantId, int pageSize, string continuationToken)
        {
            throw new NotImplementedException();
        }
        public Task<Campaign> UpsertCampaignAsync(string tenantId, Campaign campaign) => throw new NotImplementedException();
    }
}
