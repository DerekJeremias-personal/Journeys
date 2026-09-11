using Backend.Dto.Structures.Model;
using Backend.Dto.Structures.Model.Attributes;
using Journeys.Core.Configuration;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine;
using Journeys.Core.RulesEngine.Comparitors;
using Journeys.Core.RulesEngine.Comparitors.Enums;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.Services;
using Journeys.Core.Utility;
using Journeys.DTO.Exceptions;
using Journeys.Core.Interfaces.DataStorage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Journeys.Tests.Services;

public class CampaignTaxonomyRuleValidatorTests
{
    private static readonly ILogger<CampaignTaxonomyRuleValidator> Logger = new NopLogger<CampaignTaxonomyRuleValidator>();

    [Fact]
    public async Task ValidateAsync_succeeds_for_taxonomic_rule_on_collection_path()
    {
        var (eventModel, lineModel) = CreateOrderAndLineModels();
        var adapter = new RoutingModelAdapter((eventModel.ID, "loyalty", eventModel), (lineModel.ID, "loyalty", lineModel));
        var sut = new CampaignTaxonomyRuleValidator(adapter, Options.Create(new CampaignUpsertValidationOptions()), Logger);

        var journey = new JourneyNode(
            "root",
            new List<RuleSet>
            {
                new RuleSet
                {
                    Name = "rs",
                    RuleTree = new TaxonomicRule
                    {
                        LeftProvider = new PathValueProvider("event.items"),
                        RightProvider = new ConstantValueProvider("alwaystrue"),
                        Evaluator = new StringEvaluation(StringEvalType.Equal),
                        TaxonomyId = "tax-root",
                        TaxonomyType = "tt",
                        KeySymbolPath = "sku_id",
                        IncludedTreeNodes = new List<string> { "Alcohol" },
                        IncludedIds = new List<string>(),
                        ExcludedTreeNodes = new List<string>(),
                        ExcludedIds = new List<string>()
                    }
                }
            },
            "j1",
            "j1",
            null,
            null);

        var campaign = new Campaign(
            "ext",
            CampaignStatusStrings.Draft,
            "n",
            new List<string> { eventModel.ID! },
            DateTimeOffset.UtcNow,
            null,
            null,
            journey,
            "t1",
            "c1");

        await sut.ValidateAsync("t1", campaign);
    }

    [Fact]
    public async Task ValidateAsync_throws_when_simple_rule_targets_model_taxonomy_path()
    {
        var (eventModel, lineModel) = CreateOrderAndLineModels();
        var adapter = new RoutingModelAdapter((eventModel.ID, "loyalty", eventModel), (lineModel.ID, "loyalty", lineModel));
        var sut = new CampaignTaxonomyRuleValidator(adapter, Options.Create(new CampaignUpsertValidationOptions()), Logger);

        var journey = new JourneyNode(
            "root",
            new List<RuleSet>
            {
                new RuleSet
                {
                    Name = "rs",
                    RuleTree = new SimpleRule<string>
                    {
                        LeftProvider = new PathValueProvider("event.items.category"),
                        RightProvider = new ConstantValueProvider("x"),
                        Evaluator = new StringEvaluation(StringEvalType.Equal)
                    }
                }
            },
            "j1",
            "j1",
            null,
            null);

        var campaign = new Campaign(
            "ext",
            CampaignStatusStrings.Draft,
            "n",
            new List<string> { eventModel.ID! },
            DateTimeOffset.UtcNow,
            null,
            null,
            journey,
            "t1",
            "c1");

        var ex = await Assert.ThrowsAsync<APIErrorsException>(() => sut.ValidateAsync("t1", campaign));

        Assert.Contains(ex.Errors.Keys, k => k.StartsWith("journey.taxonomy.", StringComparison.Ordinal));
    }

    private static (ModelDto Event, ModelDto Line) CreateOrderAndLineModels()
    {
        var line = new ModelDto
        {
            ID = "line-model-1",
            TenantId = "t1",
            Status = "live",
            Name = "Line",
            ModelType = "loyalty",
            Attributes = new List<ModelAttributeDto>
            {
                new ModelAttributeTaxonomyDto
                {
                    Symbol = "category",
                    DataType = "string",
                    DisplayName = "Category",
                    Status = "live",
                    TaxonomyEntity = new TaxonomyEntityDto
                    {
                        TaxonomyType = "tt",
                        ElementSymbol = "sku_id",
                        TaxonomyId = "tax-root",
                        ModelId = "line-model-1",
                        ModelType = "loyalty"
                    }
                }
            }
        };

        var evt = new ModelDto
        {
            ID = "event-model-1",
            TenantId = "t1",
            Status = "live",
            Name = "Order",
            ModelType = "loyalty",
            Attributes = new List<ModelAttributeDto>
            {
                new ModelAttributeListDto
                {
                    Symbol = "items",
                    DataType = "list",
                    DisplayName = "Items",
                    Status = "live",
                    ModelId = line.ID!,
                    ModelType = "loyalty",
                    ListAttributeDataType = "ModelObject"
                }
            }
        };

        return (evt, line);
    }

    private sealed class RoutingModelAdapter : IModelAdapter
    {
        private readonly Dictionary<(string Id, string Type), ModelDto> _byIdType;

        public RoutingModelAdapter(params (string Id, string Type, ModelDto Model)[] models)
        {
            _byIdType = models.ToDictionary(m => (m.Id, m.Type), m => m.Model);
        }

        public Task<PagedResultSet<ModelDto>> GetModels(string tenantId, string groupingType, int pageSize = 10, string? continuationToken = null, CancellationToken? token = null) =>
            throw new NotImplementedException();

        public Task RemoveModelAsync(string tenantId, string modelId, string modelType, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<ModelDto?> GetModelAsync(string tenantId, string modelId, string modelType, bool includeChildModels = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_byIdType.TryGetValue((modelId, modelType), out var m) ? m : null);
        }
    }

    private sealed class NopLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
