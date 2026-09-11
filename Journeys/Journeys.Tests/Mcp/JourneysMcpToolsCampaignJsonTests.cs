using System.Text.Json;
using Journeys.API.Mcp;
using Journeys.Core.Interfaces.Services;
using Journeys.DTO.Models;
using Journeys.DTO.Requests;
using Journeys.DTO.Responses;
using Journeys.CampaignAgent.Remediation;
using Xunit;

namespace Journeys.Tests.Mcp;

public class JourneysMcpToolsCampaignJsonTests
{
    private const string EventsObjectCampaignJson = """
        {
          "name": "order earn campaign",
          "status": "Draft",
          "startDate": "2025-07-14T00:00:00+00:00",
          "events": [
            { "id": "a6edbbc5-bf43-4c57-b2f1-e015b9efaf03", "modelType": "loyalty" }
          ]
        }
        """;

    [Fact]
    public async Task ValidateCampaign_returns_structured_error_when_events_not_string_array()
    {
        var sut = CreateSut();

        var result = await sut.ValidateCampaign("primo", EventsObjectCampaignJson);

        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("campaignJson", out var message));
        Assert.Contains("System.String", message.GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("events", message.GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpsertCampaign_returns_structured_error_when_events_not_string_array()
    {
        var sut = CreateSut();

        var result = await sut.UpsertCampaign("primo", EventsObjectCampaignJson);

        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("campaignJson", out _));
    }

    [Fact]
    public async Task ValidateCampaign_returns_structured_error_for_missing_tenantId()
    {
        var sut = CreateSut();

        var result = await sut.ValidateCampaign("", EventsObjectCampaignJson);

        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.GetProperty("errors").TryGetProperty("tenantId", out _));
    }

    [Fact]
    public void Remediation_maps_campaignJson_deserialize_error()
    {
        var failure = new BackendToolFailure
        {
            ToolName = "validate_campaign",
            ParseStatus = BackendToolFailureParseStatus.Success,
            ValidationErrors = new Dictionary<string, string>
            {
                ["campaignJson"] =
                    "JsonException: The JSON value could not be converted to System.String. Path: $.events[0]"
            }
        };

        var payload = JourneysToolRemediationCatalog.Build("validate_campaign", failure);

        Assert.NotNull(payload);
        Assert.Contains("campaign_json_parse", payload!.MatchedRules);
        Assert.Contains(payload.Hints, h => h.Field == "campaignJson");
        Assert.True(payload.RetryRecommended);
    }

    private static JourneysMcpTools CreateSut(ICampaignService? campaign = null) =>
        new(
            campaign ?? new StubCampaignService(),
            null!,
            null!,
            null!,
            null!,
            null!);

    private sealed class StubCampaignService : ICampaignService
    {
        public Task<CampaignValidationResultDto> ValidateCampaignAsync(
            string tenantId,
            CampaignDto campaign,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CampaignValidationResultDto { IsValid = true });

        public Task<CampaignDto> UpsertCampaignAsync(string tenantId, CampaignDto campaign) =>
            Task.FromResult(campaign);

        public Task<CampaignDto> FetchCampaignAsync(string tenantId, string campaignId, string status) =>
            throw new NotImplementedException();

        public Task<PagedResultSetResponse<CampaignDto>> GetCampaignsByFiltersAsync(string tenantId, GetCampaignsByFilterRequest req) =>
            throw new NotImplementedException();

        public Task<List<CampaignDto>> GetManyCampaignsAsync(string tenantId, List<string> ids, string? status = null) =>
            throw new NotImplementedException();

        public Task<PagedResultSetResponse<CampaignDto>> GetCampaignsByStatusAsync(string tenantId, string status, int pageSize, string? continuationToken = null) =>
            throw new NotImplementedException();

        public Task<PagedResultSetResponse<CampaignDto>> GetAllCampaignsAsync(string tenantId, int pageSize, string continutationToken = null) =>
            throw new NotImplementedException();

        public Task DeleteCampaignAsync(string tenantId, string campaignId, string status) =>
            throw new NotImplementedException();

        public Task DeleteCampaignAsync(string tenantId, CampaignDto campaign) =>
            throw new NotImplementedException();

        public Task<PointAccountTypeDto> FetchPointAccountType(string tenantId, string id) =>
            throw new NotImplementedException();

        public Task<PagedResultSetResponse<PointAccountTypeDto>> GetAllPointAccountTypesAsync(string tenantId, int pageSize, string continutationToken = null) =>
            throw new NotImplementedException();

        public Task<PointAccountTypeDto> UpsertPointAccountTypeAsync(string tenantId, PointAccountTypeDto pat) =>
            throw new NotImplementedException();

        public Task DeletePointAccountTypeAsync(string tenantId, string pointAccountTypeId) =>
            throw new NotImplementedException();

        public Task<CampaignStatisticsDto> GetCampaignStatsAsync(string tenantId, string campaignId) =>
            throw new NotImplementedException();

        public Task<List<CampaignDto>> GetCampaignVersionsByExtIdAsync(string tenantId, string extCampaignId) =>
            throw new NotImplementedException();

        public Task<PagedResultSetResponse<CampaignDto>> GetArchivedCampaignsByDateRangeAsync(
            string tenantId, DateTimeOffset? fromDate, DateTimeOffset? toDate, int pageSize, string? continuationToken = null) =>
            throw new NotImplementedException();

        public Task<CampaignDto> GetLiveCampaignByExtIdAsync(string tenantId, string extCampaignId) =>
            throw new NotImplementedException();

        public Task<CampaignDto> GetDraftCampaignByExtIdAsync(string tenantId, string extCampaignId) =>
            throw new NotImplementedException();
    }
}
