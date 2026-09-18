using Backend.Dto.Structures.Model;
using Backend.Dto.Structures.Model.Attributes;
using Journeys.Core.Configuration;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.RulesEngine.Outcomes;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.Services;
using Journeys.DTO.Models;
using Journeys.DTO.Requests;
using Journeys.DTO.Responses;
using Microsoft.Extensions.Options;

namespace Journeys.Tests.Services;

public class CampaignAssistantContextServiceTests
{
    private const string TenantId = "t1";
    private const string CampaignId = "c1";
    private const string ModelId = "m1";

    [Fact]
    public async Task GetContextAsync_includes_processing_contract()
    {
        var model = CreateOrderModel();
        var sut = CreateSut(model);

        var ctx = await sut.GetContextAsync(TenantId, CampaignId, CampaignStatusStrings.Live, includeSampleTemplate: false);

        Assert.NotNull(ctx);
        var em = Assert.Single(ctx.EventModels);
        Assert.NotNull(em.ProcessingContract);
        Assert.True(em.ProcessingContract!.IsProcessEventEligible);
        Assert.Equal("eventable", em.ProcessingContract.ModelTag);
        Assert.Equal("profileid", em.ProcessingContract.AccountLink.SymbolPath);
        Assert.Equal("orderid", Assert.Single(em.ProcessingContract.NaturalKey.Symbols));
        Assert.Equal("timestamp", em.ProcessingContract.TimeOfOccurrenceSymbol);
        Assert.Equal("w1", em.ProcessingContract.WrapperModelId);
    }

    [Fact]
    public async Task GetContextAsync_warns_when_model_not_eventable()
    {
        var model = CreateOrderModel();
        model.Tag = null;
        var sut = CreateSut(model);

        var ctx = await sut.GetContextAsync(TenantId, CampaignId, CampaignStatusStrings.Live, includeSampleTemplate: false);

        Assert.NotNull(ctx);
        Assert.Contains(
            ctx.Warnings,
            w => w.Contains("eventable", StringComparison.OrdinalIgnoreCase)
                 && w.Contains("ProcessEvent", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(ctx.CampaignShell);
        Assert.Contains(ModelId, ctx.CampaignShell!.IneligibleEventModelIds);
    }

    [Fact]
    public async Task GetContextAsync_sample_scaffold_root_shape()
    {
        var model = CreateOrderModel();
        var sut = CreateSut(model);

        var ctx = await sut.GetContextAsync(TenantId, CampaignId, CampaignStatusStrings.Live, includeSampleTemplate: true);

        Assert.NotNull(ctx);
        Assert.NotNull(ctx.SampleScaffold);
        Assert.NotNull(ctx.SampleScaffold!.JsonTemplate);
        Assert.DoesNotContain("\"event\":", ctx.SampleScaffold.JsonTemplate!, StringComparison.Ordinal);
        Assert.Contains("orderid", ctx.SampleScaffold.JsonTemplate!, StringComparison.Ordinal);
        Assert.Contains("profileid", ctx.SampleScaffold.JsonTemplate!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetContextAsync_includes_shell_pat_journey_digests()
    {
        const string patId = "pat-1";
        var journey = new JourneyNode(
            "root",
            new List<RuleSet>
            {
                new RuleSet
                {
                    Name = "rs1",
                    Outcomes = new List<OutcomeBase>
                    {
                        new DepositPointsOutcome
                        {
                            AffectedPointAccountTypeIds = new List<string> { patId }
                        }
                    }
                }
            });

        var campaign = new Campaign(
            extCampaignId: "ext1",
            status: CampaignStatusStrings.Live,
            name: "Test Campaign",
            events: [ModelId],
            startDate: DateTimeOffset.UtcNow,
            endDate: null,
            segments: null,
            journey: journey,
            tenantId: TenantId,
            id: CampaignId);

        var model = CreateOrderModel();
        var patDto = new PointAccountTypeDto
        {
            Id = patId,
            Name = "Spendable",
            Status = "Live",
            LedgerType = "Standard",
            IsSpendable = true
        };
        var campaignService = new StubCampaignService((_, id) =>
            id == patId ? Task.FromResult(patDto) : throw new InvalidOperationException($"Unexpected PAT {id}"));

        var sut = CreateSut(model, campaign, campaignService);

        var ctx = await sut.GetContextAsync(TenantId, CampaignId, CampaignStatusStrings.Live, includeSampleTemplate: false);

        Assert.NotNull(ctx);
        Assert.NotNull(ctx.CampaignShell);
        Assert.Equal(CampaignId, ctx.CampaignShell!.CampaignId);
        Assert.NotNull(ctx.Journey);
        Assert.NotNull(ctx.PointAccountManifest);
        Assert.Contains(ctx.PointAccountManifest!.Items, i => string.Equals(i.Id, patId, StringComparison.OrdinalIgnoreCase));
        var patRef = Assert.Single(ctx.Journey!.ReferencedPointAccountTypes);
        Assert.Equal(patId, patRef.PointAccountTypeId);
        Assert.True(patRef.InManifest);
    }

    [Fact]
    public async Task GetContextAsync_field_roles()
    {
        var model = CreateOrderModel();
        var sut = CreateSut(model);

        var ctx = await sut.GetContextAsync(TenantId, CampaignId, CampaignStatusStrings.Live, includeSampleTemplate: true);

        Assert.NotNull(ctx);
        Assert.NotNull(ctx.SampleScaffold);
        var roles = ctx.SampleScaffold!.FieldRoles;
        Assert.Equal("accountLink", roles["profileid"]);
        Assert.Equal("naturalKey", roles["orderid"]);
        Assert.Equal("attribute", roles["timestamp"]);
        Assert.Equal("profileid", ctx.SampleScaffold.AccountLinkSymbolPath);
        Assert.Equal("orderid", Assert.Single(ctx.SampleScaffold.NaturalKeySymbols));
    }

    private static CampaignAssistantContextService CreateSut(
        ModelDto model,
        Campaign? campaign = null,
        ICampaignService? campaignService = null)
    {
        campaign ??= new Campaign(
            extCampaignId: "ext1",
            status: CampaignStatusStrings.Live,
            name: "Test Campaign",
            events: [ModelId],
            startDate: DateTimeOffset.UtcNow,
            endDate: null,
            segments: null,
            journey: null,
            tenantId: TenantId,
            id: CampaignId);

        var campaignAdapter = new StubCampaignAdapter(campaign);
        var modelAdapter = new StubModelAdapter((_, modelId, modelType, _, _) =>
            modelId == ModelId && modelType == "loyalty" ? Task.FromResult<ModelDto?>(model) : Task.FromResult<ModelDto?>(null));
        campaignService ??= new StubCampaignService();
        var options = Options.Create(new CampaignUpsertValidationOptions
        {
            EventModelTypeCandidates = ["loyalty"]
        });

        return new CampaignAssistantContextService(campaignAdapter, modelAdapter, campaignService, options);
    }

    private static ModelDto CreateOrderModel() => new()
    {
        ID = ModelId,
        Name = "order",
        ModelType = "loyalty",
        Tag = "eventable",
        Attributes =
        [
            new ModelAttributePrimitiveDto { Symbol = "orderid", DataType = "string", DisplayName = "Order id", Status = "Live" },
            new ModelAttributePrimitiveDto { Symbol = "profileid", DataType = "string", DisplayName = "Profile", Status = "Live" },
            new ModelAttributePrimitiveDto { Symbol = "timestamp", DataType = "datetime", DisplayName = "Ts", Status = "Live" }
        ],
        ModelMetaData = new Dictionary<string, string>
        {
            ["Wrapper"] = "w1",
            ["NaturalKeySymbols"] = "[\"orderid\"]",
            ["AccountXIdSymbol"] = "profileid",
            ["TimeOfOccurrence"] = "timestamp"
        }
    };

    private sealed class StubCampaignAdapter(Campaign campaign) : ICampaignAdapter
    {
        public Task<Campaign> FetchCampaignAsync(string tenantId, string campaignId, string status) =>
            Task.FromResult(campaign);

        public Task<PagedResultSet<Campaign>> GetCampaignsByFiltersAsync(string tenantId, GetCampaignsByFilterRequest req) =>
            throw new NotImplementedException();

        public Task<List<Campaign>> GetCampaignsAsync(string tenantId, List<string> ids, string status) =>
            throw new NotImplementedException();

        public Task<PagedResultSet<Campaign>> GetCampaignsByStatusAsync(string tenantId, string status, int pageSize, string? continuationToken = null) =>
            throw new NotImplementedException();

        public Task<PagedResultSet<Campaign>> GetAllCampaignsAsync(string tenantId, int pageSize, string continuationToken = null!) =>
            throw new NotImplementedException();

        public Task<Campaign> UpsertCampaignAsync(string tenantId, Campaign campaign) =>
            throw new NotImplementedException();

        public Task DeleteCampaignAsync(string tenantId, string campaignId, string status) =>
            throw new NotImplementedException();

        public Task DeleteCampaignAsync(string tenantId, Campaign Campaign) =>
            throw new NotImplementedException();

        public Task<List<Campaign>> GetCampaignVersionsByExtIdAsync(string tenantId, string extCampaignId) =>
            throw new NotImplementedException();

        public Task<PagedResultSet<Campaign>> GetArchivedCampaignsByDateRangeAsync(string tenantId, DateTimeOffset? fromDate, DateTimeOffset? toDate, int pageSize, string? continuationToken = null) =>
            throw new NotImplementedException();

        public Task<Campaign> GetLiveCampaignByExtIdAsync(string tenantId, string extCampaignId) =>
            throw new NotImplementedException();

        public Task<Campaign> GetDraftCampaignByExtIdAsync(string tenantId, string extCampaignId) =>
            throw new NotImplementedException();
    }

    private sealed class StubCampaignService(
        Func<string, string, Task<PointAccountTypeDto>>? fetchPointAccountType = null) : ICampaignService
    {
        public Task<PointAccountTypeDto> FetchPointAccountType(string tenantId, string id) =>
            fetchPointAccountType != null
                ? fetchPointAccountType(tenantId, id)
                : throw new NotImplementedException();

        public Task<CampaignDto> FetchCampaignAsync(string tenantId, string campaignId, string status) =>
            throw new NotImplementedException();

        public Task<PagedResultSetResponse<CampaignDto>> GetCampaignsByFiltersAsync(string tenantId, GetCampaignsByFilterRequest req) =>
            throw new NotImplementedException();

        public Task<List<CampaignDto>> GetManyCampaignsAsync(string tenantId, List<string> ids, string status = null!) =>
            throw new NotImplementedException();

        public Task<PagedResultSetResponse<CampaignDto>> GetCampaignsByStatusAsync(string tenantId, string status, int pageSize, string? continuationToken = null) =>
            throw new NotImplementedException();

        public Task<PagedResultSetResponse<CampaignDto>> GetAllCampaignsAsync(string tenantId, int pageSize, string continutationToken = null!) =>
            throw new NotImplementedException();

        public Task<CampaignDto> UpsertCampaignAsync(string tenantId, CampaignDto campaign) =>
            throw new NotImplementedException();

        public Task<CampaignDto> CopyCampaignAsync(
            string tenantId,
            string campaignId,
            string status,
            string? name = null,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<CampaignDto> RestoreArchivedCampaignAsync(
            string tenantId,
            string campaignId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<CampaignValidationResultDto> ValidateCampaignAsync(
            string tenantId,
            CampaignDto campaign,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task DeleteCampaignAsync(string tenantId, string campaignId, string status) =>
            throw new NotImplementedException();

        public Task DeleteCampaignAsync(string tenantId, CampaignDto campaign) =>
            throw new NotImplementedException();

        public Task<PagedResultSetResponse<PointAccountTypeDto>> GetAllPointAccountTypesAsync(string tenantId, int pageSize, string continutationToken = null!) =>
            throw new NotImplementedException();

        public Task<PointAccountTypeDto> UpsertPointAccountTypeAsync(string tenantId, PointAccountTypeDto pat) =>
            throw new NotImplementedException();

        public Task DeletePointAccountTypeAsync(string tenantId, string pointAccountTypeId) =>
            throw new NotImplementedException();

        public Task<CampaignStatisticsDto> GetCampaignStatsAsync(string tenantId, string campaignId) =>
            throw new NotImplementedException();

        public Task<List<CampaignDto>> GetCampaignVersionsByExtIdAsync(string tenantId, string extCampaignId) =>
            throw new NotImplementedException();

        public Task<PagedResultSetResponse<CampaignDto>> GetArchivedCampaignsByDateRangeAsync(string tenantId, DateTimeOffset? fromDate, DateTimeOffset? toDate, int pageSize, string? continuationToken = null) =>
            throw new NotImplementedException();

        public Task<CampaignDto> GetLiveCampaignByExtIdAsync(string tenantId, string extCampaignId) =>
            throw new NotImplementedException();

        public Task<CampaignDto> GetDraftCampaignByExtIdAsync(string tenantId, string extCampaignId) =>
            throw new NotImplementedException();
    }

    private sealed class StubModelAdapter(
        Func<string, string, string, bool, CancellationToken, Task<ModelDto?>> getModel) : IModelAdapter
    {
        public Task<PagedResultSet<ModelDto>> GetModels(string tenantId, string groupingType, int pageSize = 10, string? continuationToken = null, CancellationToken? token = null) =>
            throw new NotImplementedException();

        public Task RemoveModelAsync(string tenantId, string modelId, string modelType, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<ModelDto?> GetModelAsync(string tenantId, string modelId, string modelType, bool includeChildModels = false, CancellationToken cancellationToken = default) =>
            getModel(tenantId, modelId, modelType, includeChildModels, cancellationToken);
    }
}
