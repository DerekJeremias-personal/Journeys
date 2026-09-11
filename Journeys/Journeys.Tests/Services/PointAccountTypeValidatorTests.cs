using Backend.Dto.Structures.Model;
using Journeys.Core.Configuration;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.Core.Services;
using Journeys.Core.Utility;
using Journeys.DTO.Exceptions;
using Journeys.DTO.Models;
using Journeys.DTO.Requests;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Journeys.Tests.Services;

public class PointAccountTypeValidatorTests
{
    private static PointAccountTypeDto ValidPat() => new()
    {
        Id = "pat-1",
        Name = "user_spendable",
        Status = "Active",
        LedgerType = "Spendable",
        IsSpendable = true
    };

    [Fact]
    public void Validate_succeeds_for_valid_pat()
    {
        PointAccountTypeValidator.Validate(ValidPat());
    }

    [Fact]
    public void Validate_throws_when_ledger_type_missing()
    {
        var pat = ValidPat();
        pat.LedgerType = null;

        var ex = Assert.Throws<APIErrorsException>(() => PointAccountTypeValidator.Validate(pat));

        Assert.Contains(ex.Errors.Values, v => v.Contains("PAT_MISSING_LEDGER_TYPE", StringComparison.Ordinal));
        Assert.Contains(ex.Errors.Values, v => v.Contains("LedgerType", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_throws_when_ledger_type_invalid()
    {
        var pat = ValidPat();
        pat.LedgerType = "TierQualification";

        var ex = Assert.Throws<APIErrorsException>(() => PointAccountTypeValidator.Validate(pat));

        Assert.Contains(ex.Errors.Values, v => v.Contains("PAT_INVALID_LEDGER_TYPE", StringComparison.Ordinal));
        Assert.Contains(ex.Errors.Values, v => v.Contains("TierQualification", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_accepts_ledger_type_case_insensitive()
    {
        var pat = ValidPat();
        pat.LedgerType = "spendable";

        PointAccountTypeValidator.Validate(pat);
    }

    [Fact]
    public void Validate_throws_when_name_missing()
    {
        var pat = ValidPat();
        pat.Name = "  ";

        var ex = Assert.Throws<APIErrorsException>(() => PointAccountTypeValidator.Validate(pat));

        Assert.Contains(ex.Errors.Values, v => v.Contains("PAT_MISSING_NAME", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_throws_when_status_missing()
    {
        var pat = ValidPat();
        pat.Status = null;

        var ex = Assert.Throws<APIErrorsException>(() => PointAccountTypeValidator.Validate(pat));

        Assert.Contains(ex.Errors.Values, v => v.Contains("PAT_MISSING_STATUS", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_succeeds_for_valid_tier_qual_pat()
    {
        var pat = ValidPat();
        pat.LedgerType = "NonSpendable";
        pat.IsSpendable = false;
        pat.PointsLifespanDays = 365;
        PointAccountTypeValidator.Validate(pat);
    }

    [Fact]
    public void Validate_throws_spendable_ledger_mismatch()
    {
        var pat = ValidPat();
        pat.LedgerType = "NonSpendable";
        pat.IsSpendable = true;

        var ex = Assert.Throws<APIErrorsException>(() => PointAccountTypeValidator.Validate(pat));
        Assert.Contains(ex.Errors.Values, v => v.Contains("PAT_SPENDABLE_LEDGER_MISMATCH", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_throws_nonspendable_ledger_mismatch()
    {
        var pat = ValidPat();
        pat.IsSpendable = false;

        var ex = Assert.Throws<APIErrorsException>(() => PointAccountTypeValidator.Validate(pat));
        Assert.Contains(ex.Errors.Values, v => v.Contains("PAT_NONSPENDABLE_LEDGER_MISMATCH", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_throws_escrow_must_not_be_spendable()
    {
        var pat = ValidPat();
        pat.LedgerType = "Escrow";
        pat.IsSpendable = true;

        var ex = Assert.Throws<APIErrorsException>(() => PointAccountTypeValidator.Validate(pat));
        Assert.Contains(ex.Errors.Values, v => v.Contains("PAT_ESCROW_MUST_NOT_BE_SPENDABLE", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_throws_expires_to_requires_lifespan()
    {
        var pat = ValidPat();
        pat.ExpiresToPointAccountTypeId = "dest-pat-id";
        pat.PointsLifespanDays = null;
        pat.PointsLifespanEndDate = null;

        var ex = Assert.Throws<APIErrorsException>(() => PointAccountTypeValidator.Validate(pat));
        Assert.Contains(ex.Errors.Values, v => v.Contains("PAT_EXPIRES_TO_REQUIRES_LIFESPAN", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_throws_expires_to_self()
    {
        var pat = ValidPat();
        pat.ExpiresToPointAccountTypeId = pat.Id;
        pat.PointsLifespanDays = 30;

        var ex = Assert.Throws<APIErrorsException>(() => PointAccountTypeValidator.Validate(pat));
        Assert.Contains(ex.Errors.Values, v => v.Contains("PAT_EXPIRES_TO_SELF", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_throws_conflicting_lifespan()
    {
        var pat = ValidPat();
        pat.PointsLifespanDays = 365;
        pat.PointsLifespanEndDate = DateTimeOffset.UtcNow.AddYears(1);

        var ex = Assert.Throws<APIErrorsException>(() => PointAccountTypeValidator.Validate(pat));
        Assert.Contains(ex.Errors.Values, v => v.Contains("PAT_CONFLICTING_LIFESPAN", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_throws_expires_to_not_pat_id_when_ledger_name()
    {
        var pat = ValidPat();
        pat.ExpiresToPointAccountTypeId = "Expired";
        pat.PointsLifespanDays = 365;

        var ex = Assert.Throws<APIErrorsException>(() => PointAccountTypeValidator.Validate(pat));
        Assert.Contains(ex.Errors.Values, v => v.Contains("PAT_EXPIRES_TO_NOT_PAT_ID", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_throws_invalid_rounding_option()
    {
        var pat = ValidPat();
        pat.RoundingOptionString = "NotARealMode";

        var ex = Assert.Throws<APIErrorsException>(() => PointAccountTypeValidator.Validate(pat));
        Assert.Contains(ex.Errors.Values, v => v.Contains("PAT_INVALID_ROUNDING_OPTION", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_throws_invalid_rounding_places()
    {
        var pat = ValidPat();
        pat.RoundingDecimalPlaces = -1;

        var ex = Assert.Throws<APIErrorsException>(() => PointAccountTypeValidator.Validate(pat));
        Assert.Contains(ex.Errors.Values, v => v.Contains("PAT_INVALID_ROUNDING_PLACES", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_succeeds_for_valid_escrow_cascade_pat()
    {
        var pat = ValidPat();
        pat.LedgerType = "Escrow";
        pat.IsSpendable = false;
        pat.PointsLifespanDays = 30;
        pat.ExpiresToPointAccountTypeId = "spendable-pat-guid";

        PointAccountTypeValidator.Validate(pat);
    }

    [Fact]
    public void Validate_skips_spendable_ledger_cross_check_when_is_spendable_null()
    {
        var pat = ValidPat();
        pat.IsSpendable = null;
        pat.LedgerType = "NonSpendable";

        PointAccountTypeValidator.Validate(pat);
    }
}

public class CampaignServiceUpsertPointAccountTypeTests
{
    private static readonly ILogger<CampaignService> Logger = new NopLogger<CampaignService>();
    private static readonly CampaignDefinitionValidator CampaignDefinitionValidator =
        CampaignTestServices.CreateDefinitionValidator(new StubModelAdapter());

    [Fact]
    public async Task UpsertPointAccountTypeAsync_throws_when_ledger_type_missing_and_does_not_call_adapter()
    {
        var adapter = new RecordingPatAdapter();
        var sut = new CampaignService(
            new ThrowingCampaignAdapter(),
            adapter,
            new RecordingPatCache(),
            Logger,
            CampaignDefinitionValidator,
            CampaignTestServices.CreateValidationOrchestrator());

        var pat = new PointAccountTypeDto
        {
            Id = "pat-1",
            Name = "Spendable",
            Status = "Active"
        };

        var ex = await Assert.ThrowsAsync<APIErrorsException>(() =>
            sut.UpsertPointAccountTypeAsync("t1", pat));

        Assert.Contains(ex.Errors.Values, v => v.Contains("PAT_MISSING_LEDGER_TYPE", StringComparison.Ordinal));
        Assert.Empty(adapter.UpsertCalls);
    }

    [Fact]
    public async Task UpsertPointAccountTypeAsync_calls_adapter_when_valid()
    {
        var adapter = new RecordingPatAdapter();
        var sut = new CampaignService(
            new ThrowingCampaignAdapter(),
            adapter,
            new RecordingPatCache(),
            Logger,
            CampaignDefinitionValidator,
            CampaignTestServices.CreateValidationOrchestrator());

        var pat = new PointAccountTypeDto
        {
            Id = "pat-1",
            Name = "Spendable",
            Status = "Active",
            LedgerType = "Spendable",
            IsSpendable = true,
            TenantId = "t1"
        };

        var result = await sut.UpsertPointAccountTypeAsync("t1", pat);

        Assert.Single(adapter.UpsertCalls);
        Assert.Equal("pat-1", result.Id);
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
        public List<(string TenantId, PointAccountType Pat)> UpsertCalls { get; } = new();

        public Task<PointAccountType> UpsertPointAccountTypeAsync(string tenantId, PointAccountType acctType)
        {
            UpsertCalls.Add((tenantId, acctType));
            return Task.FromResult(acctType);
        }

        public Task DeletePointAccountTypeAsync(string tenantId, string id) => throw new NotImplementedException();
        public Task DeletePointAccountTypeAsync(string tenantId, PointAccountType acctType) => throw new NotImplementedException();
        public Task<PointAccountType> FetchPointAccountTypeAsync(string tenantId, string entityId) => throw new NotImplementedException();
        public Task<PagedResultSet<PointAccountType>> GetAllPointAccountTypesAsync(string tenantId, int pageSize, string continuationToken = null) => throw new NotImplementedException();
    }

    private sealed class RecordingPatCache : IPointAccountTypeCache
    {
        public bool CachePointAccountType(string tenantId, PointAccountType pointAccountType) => throw new NotImplementedException();
        public Task InvalidatePointAccountTypeAsync(string tenantId, string pointAccountTypeId) => Task.CompletedTask;
        public Task InvalidateTenantPointAccountTypesAsync(string tenantId) => Task.CompletedTask;
        public Task<PointAccountType> GetPointAccountTypeAsync(string tenantId, string pointAccountTypeId) => throw new NotImplementedException();
        public Task<bool> EnsurePATsLoaded(string tenantId) => throw new NotImplementedException();
        public Task<List<PointAccountType>> GetAllPointAccountTypes(string tenantId) => throw new NotImplementedException();
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
        public Task<PagedResultSet<Campaign>> GetAllCampaignsAsync(string tenantId, int pageSize, string continuationToken) => throw new NotImplementedException();
        public Task<Campaign> UpsertCampaignAsync(string tenantId, Campaign campaign) => throw new NotImplementedException();
    }
}