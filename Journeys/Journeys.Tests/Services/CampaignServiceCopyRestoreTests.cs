using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.Core.Services;
using Journeys.DTO.Exceptions;
using Journeys.DTO.Requests;
using Microsoft.Extensions.Logging;

namespace Journeys.Tests.Services;

public class CampaignServiceCopyRestoreTests
{
    private static readonly ILogger<CampaignService> Logger = new NopLogger<CampaignService>();

    [Fact]
    public async Task CopyCampaignAsync_repeatedly_upserts_distinct_draft_program_identities()
    {
        var adapter = new RecordingCampaignAdapter
        {
            Source = Campaign("source-id", "live", "Summer", "summer")
        };
        var sut = CreateService(adapter);

        var firstResult = await sut.CopyCampaignAsync("tenant-1", "source-id", "live");
        var secondResult = await sut.CopyCampaignAsync("tenant-1", "source-id", "live");

        Assert.Equal(("tenant-1", "source-id", "live"), adapter.FetchCall);
        Assert.Equal(2, adapter.UpsertedCampaigns.Count);
        var firstCopy = adapter.UpsertedCampaigns[0];
        var secondCopy = adapter.UpsertedCampaigns[1];
        Assert.NotEqual("source-id", firstCopy.Id);
        Assert.NotEqual(firstCopy.Id, secondCopy.Id);
        Assert.Equal("draft", firstCopy.Status);
        Assert.Equal("summer copy", firstCopy.Name);
        Assert.NotEqual("summer", firstCopy.ExtCampaignId);
        Assert.NotEqual("summer", secondCopy.ExtCampaignId);
        Assert.NotEqual(firstCopy.ExtCampaignId, secondCopy.ExtCampaignId);
        Assert.Equal(firstCopy.Id, firstResult.Id);
        Assert.Equal(secondCopy.Id, secondResult.Id);
    }

    [Fact]
    public async Task RestoreArchivedCampaignAsync_throws_when_draft_already_exists()
    {
        var adapter = new RecordingCampaignAdapter
        {
            Source = Campaign("archive-id", "archive", "Summer", "summer"),
            ExistingDraft = Campaign("draft-id", "draft", "Summer", "summer")
        };
        var sut = CreateService(adapter);

        var exception = await Assert.ThrowsAsync<APIErrorsException>(
            () => sut.RestoreArchivedCampaignAsync("tenant-1", "archive-id"));

        Assert.Equal("A draft already exists for this program.", exception.Errors["extCampaignId"]);
        Assert.Null(adapter.Upserted);
    }

    [Fact]
    public async Task RestoreArchivedCampaignAsync_upserts_new_draft_with_same_external_id()
    {
        var adapter = new RecordingCampaignAdapter
        {
            Source = Campaign("archive-id", "archive", "Summer", "summer")
        };
        var sut = CreateService(adapter);

        var result = await sut.RestoreArchivedCampaignAsync("tenant-1", "archive-id");

        Assert.Equal(("tenant-1", "archive-id", "archive"), adapter.FetchCall);
        Assert.Equal(("tenant-1", "summer"), adapter.DraftLookupCall);
        Assert.NotNull(adapter.Upserted);
        Assert.NotEqual("archive-id", adapter.Upserted.Id);
        Assert.Equal("summer", adapter.Upserted.ExtCampaignId);
        Assert.Equal("draft", adapter.Upserted.Status);
        Assert.NotEqual("archive", adapter.Upserted.Status);
        Assert.Equal(adapter.Upserted.Id, result.Id);
    }

    private static CampaignService CreateService(RecordingCampaignAdapter adapter) =>
        new(
            adapter,
            new ThrowingPointAccountTypeAdapter(),
            new PermissivePointAccountTypeCache(),
            Logger,
            CampaignTestServices.CreateDefinitionValidator(),
            CampaignTestServices.CreateValidationOrchestrator());

    private static Campaign Campaign(string id, string status, string name, string extCampaignId) =>
        new(
            extCampaignId,
            status,
            name,
            new List<string> { "evt" },
            DateTimeOffset.Parse("2026-01-01Z"),
            null,
            new List<Segment>(),
            null,
            "tenant-1",
            id);

    private sealed class RecordingCampaignAdapter : ICampaignAdapter
    {
        public Campaign? Source { get; init; }
        public Campaign? ExistingDraft { get; init; }
        public Campaign? Upserted { get; private set; }
        public List<Campaign> UpsertedCampaigns { get; } = new();
        public (string TenantId, string CampaignId, string Status)? FetchCall { get; private set; }
        public (string TenantId, string ExtCampaignId)? DraftLookupCall { get; private set; }

        public Task<Campaign> FetchCampaignAsync(string tenantId, string campaignId, string status)
        {
            FetchCall = (tenantId, campaignId, status);
            return Task.FromResult(Source!);
        }

        public Task<Campaign> GetDraftCampaignByExtIdAsync(string tenantId, string extCampaignId)
        {
            DraftLookupCall = (tenantId, extCampaignId);
            return Task.FromResult(ExistingDraft!);
        }

        public Task<Campaign> UpsertCampaignAsync(string tenantId, Campaign campaign)
        {
            Upserted = campaign;
            UpsertedCampaigns.Add(campaign);
            return Task.FromResult(campaign);
        }

        public Task DeleteCampaignAsync(string tenantId, string campaignId, string status) => throw new NotImplementedException();
        public Task DeleteCampaignAsync(string tenantId, Campaign Campaign) => throw new NotImplementedException();
        public Task<PagedResultSet<Campaign>> GetCampaignsByFiltersAsync(string tenantId, GetCampaignsByFilterRequest req) => throw new NotImplementedException();
        public Task<List<Campaign>> GetCampaignsAsync(string tenantId, List<string> ids, string status) => throw new NotImplementedException();
        public Task<PagedResultSet<Campaign>> GetCampaignsByStatusAsync(string tenantId, string status, int pageSize, string? continuationToken = null) => throw new NotImplementedException();
        public Task<PagedResultSet<Campaign>> GetAllCampaignsAsync(string tenantId, int pageSize, string continuationToken = null) => throw new NotImplementedException();
        public Task<List<Campaign>> GetCampaignVersionsByExtIdAsync(string tenantId, string extCampaignId) => throw new NotImplementedException();
        public Task<PagedResultSet<Campaign>> GetArchivedCampaignsByDateRangeAsync(string tenantId, DateTimeOffset? fromDate, DateTimeOffset? toDate, int pageSize, string? continuationToken = null) => throw new NotImplementedException();
        public Task<Campaign> GetLiveCampaignByExtIdAsync(string tenantId, string extCampaignId) => throw new NotImplementedException();
    }

    private sealed class ThrowingPointAccountTypeAdapter : IPointAccountTypeAdapter
    {
        public Task<PointAccountType> FetchPointAccountTypeAsync(string tenantId, string entityId) => throw new NotImplementedException();
        public Task<PagedResultSet<PointAccountType>> GetAllPointAccountTypesAsync(string tenantId, int pageSize, string continuationToken = null) => throw new NotImplementedException();
        public Task<PointAccountType> UpsertPointAccountTypeAsync(string tenantId, PointAccountType acctType) => throw new NotImplementedException();
        public Task DeletePointAccountTypeAsync(string tenantId, PointAccountType acctType) => throw new NotImplementedException();
        public Task DeletePointAccountTypeAsync(string tenantId, string id) => throw new NotImplementedException();
    }

    private sealed class PermissivePointAccountTypeCache : IPointAccountTypeCache
    {
        public bool CachePointAccountType(string tenantId, PointAccountType pointAccountType) => true;
        public Task<bool> EnsurePATsLoaded(string tenantId) => Task.FromResult(true);
        public Task<List<PointAccountType>> GetAllPointAccountTypes(string tenantId) => Task.FromResult(new List<PointAccountType>());
        public Task<PointAccountType> GetPointAccountTypeAsync(string tenantId, string pointAccountTypeId) => throw new NotImplementedException();
        public Task InvalidatePointAccountTypeAsync(string tenantId, string pointAccountTypeId) => Task.CompletedTask;
        public Task InvalidateTenantPointAccountTypesAsync(string tenantId) => Task.CompletedTask;
    }

    private sealed class NopLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
