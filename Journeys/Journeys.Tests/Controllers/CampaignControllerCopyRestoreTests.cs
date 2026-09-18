using Journeys.API.Controllers;
using Journeys.Core.Interfaces.Services;
using Journeys.DTO.Exceptions;
using Journeys.DTO.Models;
using Journeys.DTO.Requests;
using Journeys.DTO.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Journeys.Tests.Controllers;

public class CampaignControllerCopyRestoreTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task CopyCampaignAsync_missing_or_blank_status_returns_bad_request(string? status)
    {
        var controller = CreateController(new FakeCampaignService());

        var result = await controller.CopyCampaignAsync("tenant-1", "campaign-1", status!);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task CopyCampaignAsync_invalid_status_returns_bad_request_with_errors()
    {
        var controller = CreateController(new FakeCampaignService());

        var result = await controller.CopyCampaignAsync("tenant-1", "campaign-1", "nope");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("status", GetErrors(badRequest).Keys);
    }

    [Fact]
    public async Task CopyCampaignAsync_missing_source_returns_not_found()
    {
        var service = new FakeCampaignService
        {
            CopyResult = null
        };
        var controller = CreateController(service);

        var result = await controller.CopyCampaignAsync("tenant-1", "campaign-1", "live");

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task CopyCampaignAsync_success_returns_ok()
    {
        var copy = new CampaignDto { Id = "copy-1", Status = "draft" };
        var service = new FakeCampaignService
        {
            CopyResult = copy
        };
        var controller = CreateController(service);

        var result = await controller.CopyCampaignAsync("tenant-1", "campaign-1", "LIVE");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(copy, ok.Value);
    }

    [Fact]
    public async Task RestoreArchivedCampaignAsync_non_archive_status_returns_bad_request()
    {
        var controller = CreateController(new FakeCampaignService());

        var result = await controller.RestoreArchivedCampaignAsync("tenant-1", "campaign-1", "draft");

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task RestoreArchivedCampaignAsync_existing_draft_returns_bad_request_with_errors()
    {
        var service = new FakeCampaignService
        {
            RestoreException = new APIErrorsException(new Dictionary<string, string>
            {
                ["extCampaignId"] = "A draft already exists for this program."
            })
        };
        var controller = CreateController(service);

        var result = await controller.RestoreArchivedCampaignAsync("tenant-1", "campaign-1", "ARCHIVE");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(
            "A draft already exists for this program.",
            GetErrors(badRequest)["extCampaignId"]);
    }

    private static CampaignController CreateController(ICampaignService campaignService) =>
        new(
            campaignService,
            new UnusedCampaignAssistantContextService(),
            new NopLogger<CampaignController>());

    private static IDictionary<string, string> GetErrors(BadRequestObjectResult result)
    {
        var errors = result.Value?.GetType().GetProperty("errors")?.GetValue(result.Value);
        return Assert.IsAssignableFrom<IDictionary<string, string>>(errors);
    }

    private sealed class FakeCampaignService : ICampaignService
    {
        public CampaignDto? CopyResult { get; init; } = new() { Id = "copy-1", Status = "draft" };
        public CampaignDto? RestoreResult { get; init; } = new() { Id = "draft-1", Status = "draft" };
        public APIErrorsException? RestoreException { get; init; }

        public Task<CampaignDto> CopyCampaignAsync(
            string tenantId,
            string campaignId,
            string status,
            string? name = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CopyResult!);

        public Task<CampaignDto> RestoreArchivedCampaignAsync(
            string tenantId,
            string campaignId,
            CancellationToken cancellationToken = default)
        {
            if (RestoreException is not null)
                throw RestoreException;

            return Task.FromResult(RestoreResult!);
        }

        public Task<CampaignDto> FetchCampaignAsync(string tenantId, string campaignId, string status) => throw new NotImplementedException();
        public Task<PagedResultSetResponse<CampaignDto>> GetCampaignsByFiltersAsync(string tenantId, GetCampaignsByFilterRequest req) => throw new NotImplementedException();
        public Task<List<CampaignDto>> GetManyCampaignsAsync(string tenantId, List<string> ids, string status = null!) => throw new NotImplementedException();
        public Task<PagedResultSetResponse<CampaignDto>> GetCampaignsByStatusAsync(string tenantId, string status, int pageSize, string? continuationToken = null) => throw new NotImplementedException();
        public Task<PagedResultSetResponse<CampaignDto>> GetAllCampaignsAsync(string tenantId, int pageSize, string continutationToken = null!) => throw new NotImplementedException();
        public Task<CampaignDto> UpsertCampaignAsync(string tenantId, CampaignDto campaign) => throw new NotImplementedException();
        public Task<CampaignValidationResultDto> ValidateCampaignAsync(string tenantId, CampaignDto campaign, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteCampaignAsync(string tenantId, string campaignId, string status) => throw new NotImplementedException();
        public Task DeleteCampaignAsync(string tenantId, CampaignDto campaign) => throw new NotImplementedException();
        public Task<PointAccountTypeDto> FetchPointAccountType(string tenantId, string id) => throw new NotImplementedException();
        public Task<PagedResultSetResponse<PointAccountTypeDto>> GetAllPointAccountTypesAsync(string tenantId, int pageSize, string continutationToken = null!) => throw new NotImplementedException();
        public Task<PointAccountTypeDto> UpsertPointAccountTypeAsync(string tenantId, PointAccountTypeDto pat) => throw new NotImplementedException();
        public Task DeletePointAccountTypeAsync(string tenantId, string pointAccountTypeId) => throw new NotImplementedException();
        public Task<CampaignStatisticsDto> GetCampaignStatsAsync(string tenantId, string campaignId) => throw new NotImplementedException();
        public Task<List<CampaignDto>> GetCampaignVersionsByExtIdAsync(string tenantId, string extCampaignId) => throw new NotImplementedException();
        public Task<PagedResultSetResponse<CampaignDto>> GetArchivedCampaignsByDateRangeAsync(string tenantId, DateTimeOffset? fromDate, DateTimeOffset? toDate, int pageSize, string? continuationToken = null) => throw new NotImplementedException();
        public Task<CampaignDto> GetLiveCampaignByExtIdAsync(string tenantId, string extCampaignId) => throw new NotImplementedException();
        public Task<CampaignDto> GetDraftCampaignByExtIdAsync(string tenantId, string extCampaignId) => throw new NotImplementedException();
    }

    private sealed class UnusedCampaignAssistantContextService : ICampaignAssistantContextService
    {
        public Task<CampaignAssistantContextDto?> GetContextAsync(
            string tenantId,
            string campaignId,
            string? status,
            bool includeSampleTemplate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CampaignAssistantContextDto?>(null);
    }

    private sealed class NopLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }
}
