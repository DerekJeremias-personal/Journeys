using Journeys.Core.Configuration;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.Services;
using Journeys.Core.Utility;
using Journeys.DTO.Exceptions;
using Backend.Dto.Structures.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Journeys.Tests.Services;

public class CampaignDefinitionValidatorTests
{
    private static readonly CampaignDefinitionValidator Sut =
        CampaignTestServices.CreateDefinitionValidator(new StubModelAdapter());

    [Fact]
    public async Task ValidateAsync_throws_APIErrorsException_with_journey_materialize_when_outcomes_json_mismatches_model()
    {
        const string ruleJson = """
            {
              "Kind": "SimpleRule",
              "LeftProvider": { "$type": "PathValueProvider", "PropertyPath": "event.isloyaltymember" },
              "RightProvider": { "$type": "ConstantValueProvider", "Value": true },
              "Evaluator": { "$type": "BoolEvaluation", "Comparison": "Equal" }
            }
            """;

        const string outcomesJson = """
            [
              {
                "Kind": "TagOutcome",
                "Type": "account",
                "EntityId": "e",
                "Name": "engagement_reward",
                "Value": "active_member",
                "EffectiveStartDate": { "$type": "ConstantValueProvider", "Value": "2024-12-28T00:00:00Z" }
              }
            ]
            """;

        using var ruleDoc = JsonDocument.Parse(ruleJson);
        using var outcomesDoc = JsonDocument.Parse(outcomesJson);
        var ruleSet = new RuleSet(
            "rs",
            ruleDoc.RootElement.Clone(),
            "SimpleRule",
            outcomesDoc.RootElement.Clone(),
            null);

        var journey = new JourneyNode("root", new List<RuleSet> { ruleSet }, "j1", "j1", null, null);
        CampaignTestServices.AddRootEntryNavigation(journey);
        var campaign = new Campaign(
            "ext",
            CampaignStatusStrings.Draft,
            "n",
            null,
            DateTimeOffset.UtcNow,
            null,
            null,
            journey,
            "t1",
            "c1");

        var ex = await Assert.ThrowsAsync<APIErrorsException>(() => Sut.ValidateAsync("t1", campaign, default));

        Assert.True(ex.Errors.ContainsKey("journey.materialize"), $"Expected journey.materialize key, got: {string.Join(',', ex.Errors.Keys)}");
        Assert.Contains("EffectiveStartDateProvider", ex.Errors["journey.materialize"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_throws_journey_validation_before_materialize_for_invented_provider_type()
    {
        const string ruleJson = """
            {
              "Kind": "HistoricalRule",
              "HistoricalValueProvider": {
                "$type": "PointBalanceHistoricalValueProvider",
                "PointAccountTypeId": "pat-tier"
              }
            }
            """;

        using var ruleDoc = JsonDocument.Parse(ruleJson);
        using var outcomesDoc = JsonDocument.Parse("[]");
        var ruleSet = new RuleSet(
            "TierQual",
            ruleDoc.RootElement.Clone(),
            "HistoricalRule",
            outcomesDoc.RootElement.Clone(),
            null);

        var journey = new JourneyNode("root", new List<RuleSet> { ruleSet }, "j1", "j1", null, null);
        var campaign = new Campaign(
            "ext",
            CampaignStatusStrings.Draft,
            "n",
            null,
            DateTimeOffset.UtcNow,
            null,
            null,
            journey,
            "t1",
            "c1");

        var ex = await Assert.ThrowsAsync<APIErrorsException>(() => Sut.ValidateAsync("t1", campaign, default));

        Assert.Contains(ex.Errors.Keys, k => k.StartsWith("journey.validation.", StringComparison.Ordinal));
        Assert.DoesNotContain(ex.Errors.Keys, k => k.StartsWith("journey.materialize", StringComparison.Ordinal));
        var message = ex.Errors.Values.First();
        Assert.Contains("TIER_A_UNKNOWN_TYPE_DISCRIMINATOR", message, StringComparison.Ordinal);
        Assert.Contains("PointBalanceHistoricalValueProvider", message, StringComparison.Ordinal);
        Assert.Contains("allowed=SimpleCalculationProvider", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAsync_succeeds_for_valid_in_memory_journey()
    {
        var journey = new JourneyNode(
            "root",
            new List<RuleSet>
            {
                new RuleSet
                {
                    Name = "rs",
                    RuleTree = new Journeys.Core.RulesEngine.Rules.NumericPropertyRule
                    {
                        LeftProvider = new Journeys.Core.RulesEngine.Providers.PathValueProvider("event.amount"),
                        RightProvider = new Journeys.Core.RulesEngine.Providers.ConstantValueProvider(0m),
                        Evaluator = new Journeys.Core.RulesEngine.Comparitors.NumericEvaluation(
                            Journeys.Core.RulesEngine.Comparitors.Enums.NumEvalType.GreaterThan)
                    }
                }
            },
            "j1",
            "j1",
            null,
            null);
        CampaignTestServices.AddRootEntryNavigation(journey);

        var campaign = new Campaign(
            "ext",
            CampaignStatusStrings.Draft,
            "n",
            null,
            DateTimeOffset.UtcNow,
            null,
            null,
            journey,
            "t1",
            "c1");

        await Sut.ValidateAsync("t1", campaign, default);
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
}
