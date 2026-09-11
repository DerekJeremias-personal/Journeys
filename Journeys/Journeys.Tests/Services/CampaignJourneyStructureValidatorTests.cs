using Journeys.Core.Configuration;
using Journeys.Core.Extensions;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine;
using Journeys.Core.RulesEngine.Comparitors;
using Journeys.Core.RulesEngine.Comparitors.Enums;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.RulesEngine.Journey.Enums;
using Journeys.Core.RulesEngine.Outcomes;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Providers.Enums;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.RulesEngine.Rules.Composite;
using Journeys.Core.Services;
using Journeys.DTO.Exceptions;
using Journeys.DTO.Models;
using Journeys.DTO.Requests;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Backend.Dto.Structures.Model;
using Journeys.Core.Utility;
using System.Text.Json;

namespace Journeys.Tests.Services;

public class CampaignJourneyStructureValidatorTests
{
    private static readonly ILogger<CampaignService> Logger = new NopLogger<CampaignService>();
    private static readonly CampaignDefinitionValidator CampaignDefinitionValidator =
        CampaignTestServices.CreateDefinitionValidator(new StubModelAdapter());

    [Fact]
    public void Validate_throws_APIErrorsException_when_aggregate_missing_row_providers()
    {
        var campaign = CreateCampaignWithAggregate(left: new AggregateValueProvider());

        var ex = Assert.Throws<APIErrorsException>(() => CampaignJourneyStructureValidator.Validate(campaign));

        Assert.NotEmpty(ex.Errors);
        Assert.Contains(ex.Errors, kv => kv.Key.StartsWith("journey.validation.", StringComparison.Ordinal));
        Assert.Contains(ex.Errors.Values, v => v.Contains("RowProvider", StringComparison.Ordinal));
        Assert.Contains(ex.Errors.Values, v => v.Contains("RowPropertyProvider", StringComparison.Ordinal));
        Assert.Contains(ex.Errors.Values, v => v.Contains("Undefined", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_throws_when_aggregate_type_undefined_but_providers_set()
    {
        var agg = new AggregateValueProvider
        {
            AggregateType = AggregateType.Undefined,
            RowProvider = new PathValueProvider("event.items"),
            RowPropertyProvider = new PathValueProvider("event.price")
        };
        var campaign = CreateCampaignWithAggregate(left: agg);

        var ex = Assert.Throws<APIErrorsException>(() => CampaignJourneyStructureValidator.Validate(campaign));

        Assert.Contains(ex.Errors.Values, v => v.Contains("Undefined", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_throws_when_deposit_points_outcome_missing_dollar_amount_provider()
    {
        var journey = new JourneyNode(
            "root",
            new List<RuleSet>
            {
                new RuleSet
                {
                    Name = "earn-rs",
                    RuleTree = new NumericPropertyRule
                    {
                        LeftProvider = new PathValueProvider("event.amount"),
                        RightProvider = new ConstantValueProvider(0m),
                        Evaluator = new NumericEvaluation(NumEvalType.GreaterThan)
                    },
                    Outcomes = new List<OutcomeBase>
                    {
                        new DepositPointsOutcome
                        {
                            DollarAmountProvider = null,
                            PointsPerDollar = 1m
                        }
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
            null,
            DateTimeOffset.UtcNow,
            null,
            null,
            journey,
            "t1",
            "c1");

        var ex = Assert.Throws<APIErrorsException>(() => CampaignJourneyStructureValidator.Validate(campaign));

        Assert.Contains(ex.Errors.Values, v => v.Contains("earn-rs", StringComparison.Ordinal));
        Assert.Contains(ex.Errors.Values, v => v.Contains("DollarAmountProvider", StringComparison.Ordinal));
        Assert.Contains(ex.Errors.Values, v => v.Contains("DepositPoints", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_throws_when_deposit_points_outcome_missing_affected_pat_ids()
    {
        var journey = new JourneyNode(
            "root",
            new List<RuleSet>
            {
                new RuleSet
                {
                    Name = "earn-rs",
                    RuleTree = new NumericPropertyRule
                    {
                        LeftProvider = new PathValueProvider("event.amount"),
                        RightProvider = new ConstantValueProvider(0m),
                        Evaluator = new NumericEvaluation(NumEvalType.GreaterThan)
                    },
                    Outcomes = new List<OutcomeBase>
                    {
                        new DepositPointsOutcome
                        {
                            DollarAmountProvider = new ConstantValueProvider(10m),
                            PointsPerDollar = 1m,
                            AffectedPointAccountTypeIds = new List<string>()
                        }
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
            null,
            DateTimeOffset.UtcNow,
            null,
            null,
            journey,
            "t1",
            "c1");

        var ex = Assert.Throws<APIErrorsException>(() => CampaignJourneyStructureValidator.Validate(campaign));

        Assert.Contains(ex.Errors.Values, v =>
            v.Contains("TIER_A_DEPOSIT_MISSING_AFFECTED_PAT", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_throws_when_deposit_points_outcome_missing_event_id_provider()
    {
        var journey = new JourneyNode(
            "root",
            new List<RuleSet>
            {
                new RuleSet
                {
                    Name = "earn-rs",
                    RuleTree = new NumericPropertyRule
                    {
                        LeftProvider = new PathValueProvider("event.amount"),
                        RightProvider = new ConstantValueProvider(0m),
                        Evaluator = new NumericEvaluation(NumEvalType.GreaterThan)
                    },
                    Outcomes = new List<OutcomeBase>
                    {
                        new DepositPointsOutcome
                        {
                            DollarAmountProvider = new ConstantValueProvider(1m),
                            EventIdProvider = null,
                            PointsPerDollar = 1m
                        }
                    }
                }
            },
            "j1", "j1", null, null);

        var campaign = new Campaign("ext", CampaignStatusStrings.Draft, "n", null, DateTimeOffset.UtcNow, null, null, journey, "t1", "c1");

        var ex = Assert.Throws<APIErrorsException>(() => CampaignJourneyStructureValidator.Validate(campaign));

        Assert.Contains(ex.Errors.Values, v => v.Contains("TIER_A_OUTCOME_MISSING_EVENT_ID_PROVIDER", StringComparison.Ordinal));
        Assert.Contains(ex.Errors.Values, v => v.Contains("EventIdProvider", StringComparison.Ordinal));
        Assert.Contains(ex.Errors.Values, v => v.Contains("DepositPoints", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_throws_when_spend_points_outcome_missing_event_id_provider()
    {
        var journey = new JourneyNode(
            "root",
            new List<RuleSet>
            {
                new RuleSet
                {
                    Name = "spend-rs",
                    RuleTree = new NumericPropertyRule
                    {
                        LeftProvider = new PathValueProvider("event.amount"),
                        RightProvider = new ConstantValueProvider(0m),
                        Evaluator = new NumericEvaluation(NumEvalType.GreaterThan)
                    },
                    Outcomes = new List<OutcomeBase>
                    {
                        new SpendPointsOutcome
                        {
                            WithdrawlAmountProvider = new ConstantValueProvider(1m),
                            EventIdProvider = null
                        }
                    }
                }
            },
            "j1", "j1", null, null);

        var campaign = new Campaign("ext", CampaignStatusStrings.Draft, "n", null, DateTimeOffset.UtcNow, null, null, journey, "t1", "c1");

        var ex = Assert.Throws<APIErrorsException>(() => CampaignJourneyStructureValidator.Validate(campaign));

        Assert.Contains(ex.Errors.Values, v => v.Contains("TIER_A_OUTCOME_MISSING_EVENT_ID_PROVIDER", StringComparison.Ordinal));
        Assert.Contains(ex.Errors.Values, v => v.Contains("SpendPoints", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UpsertCampaignAsync_does_not_invoke_adapter_when_deposit_points_missing_dollar_amount_provider()
    {
        var adapter = new RecordingCampaignAdapter();
        var sut = new CampaignService(
            adapter,
            new RecordingPatAdapter(),
            new RecordingPatCache(),
            Logger,
            CampaignDefinitionValidator,
            CampaignTestServices.CreateValidationOrchestrator());

        var journey = new JourneyNode(
            "root",
            new List<RuleSet>
            {
                new RuleSet
                {
                    Name = "earn-rs",
                    RuleTree = new NumericPropertyRule
                    {
                        LeftProvider = new PathValueProvider("event.amount"),
                        RightProvider = new ConstantValueProvider(0m),
                        Evaluator = new NumericEvaluation(NumEvalType.GreaterThan)
                    },
                    Outcomes = new List<OutcomeBase>
                    {
                        new DepositPointsOutcome { DollarAmountProvider = null, PointsPerDollar = 1m }
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
            null,
            DateTimeOffset.UtcNow,
            null,
            null,
            journey,
            "t1",
            "c1");
        var dto = campaign.ToDto()!;
        dto.TenantId = "t1";

        await Assert.ThrowsAsync<APIErrorsException>(() => sut.UpsertCampaignAsync("t1", dto));

        Assert.Equal(0, adapter.UpsertCalls);
    }

    [Fact]
    public async Task UpsertCampaignAsync_does_not_invoke_adapter_when_spend_points_missing_withdrawal_amount_provider()
    {
        var adapter = new RecordingCampaignAdapter();
        var sut = new CampaignService(
            adapter,
            new RecordingPatAdapter(),
            new RecordingPatCache(),
            Logger,
            CampaignDefinitionValidator,
            CampaignTestServices.CreateValidationOrchestrator());

        var journey = new JourneyNode(
            "root",
            new List<RuleSet>
            {
                new RuleSet
                {
                    Name = "burn-rs",
                    RuleTree = new NumericPropertyRule
                    {
                        LeftProvider = new PathValueProvider("event.amount"),
                        RightProvider = new ConstantValueProvider(0m),
                        Evaluator = new NumericEvaluation(NumEvalType.GreaterThan)
                    },
                    Outcomes = new List<OutcomeBase> { new SpendPointsOutcome { WithdrawlAmountProvider = null } }
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
            null,
            DateTimeOffset.UtcNow,
            null,
            null,
            journey,
            "t1",
            "c1");
        var dto = campaign.ToDto()!;
        dto.TenantId = "t1";

        await Assert.ThrowsAsync<APIErrorsException>(() => sut.UpsertCampaignAsync("t1", dto));

        Assert.Equal(0, adapter.UpsertCalls);
    }

    [Fact]
    public void Validate_throws_when_numeric_rule_missing_evaluator()
    {
        var journey = new JourneyNode(
            "root",
            new List<RuleSet>
            {
                new RuleSet
                {
                    Name = "rs",
                    RuleTree = new NumericPropertyRule
                    {
                        LeftProvider = new PathValueProvider("event.amount"),
                        RightProvider = new ConstantValueProvider(0m),
                        Evaluator = null
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
            null,
            DateTimeOffset.UtcNow,
            null,
            null,
            journey,
            "t1",
            "c1");

        var ex = Assert.Throws<APIErrorsException>(() => CampaignJourneyStructureValidator.Validate(campaign));

        Assert.Contains(ex.Errors.Values, v => v.Contains("TIER_A_SIMPLE_RULE_MISSING_EVALUATOR", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_throws_when_not_rule_has_wrong_child_count()
    {
        var journey = new JourneyNode(
            "root",
            new List<RuleSet>
            {
                new RuleSet
                {
                    Name = "rs",
                    RuleTree = new NotRule
                    {
                        Children = new List<RuleBase>
                        {
                            new NumericPropertyRule
                            {
                                LeftProvider = new PathValueProvider("a"),
                                RightProvider = new ConstantValueProvider(0m),
                                Evaluator = new NumericEvaluation(NumEvalType.GreaterThan)
                            },
                            new NumericPropertyRule
                            {
                                LeftProvider = new PathValueProvider("b"),
                                RightProvider = new ConstantValueProvider(0m),
                                Evaluator = new NumericEvaluation(NumEvalType.GreaterThan)
                            }
                        }
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
            null,
            DateTimeOffset.UtcNow,
            null,
            null,
            journey,
            "t1",
            "c1");

        var ex = Assert.Throws<APIErrorsException>(() => CampaignJourneyStructureValidator.Validate(campaign));

        Assert.Contains(ex.Errors.Values, v => v.Contains("TIER_A_NOT_RULE_CHILD_COUNT", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_throws_when_and_rule_has_empty_children()
    {
        var journey = new JourneyNode(
            "root",
            new List<RuleSet>
            {
                new RuleSet
                {
                    Name = "rs",
                    RuleTree = new AndRule { Children = new List<RuleBase>() }
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
            null,
            DateTimeOffset.UtcNow,
            null,
            null,
            journey,
            "t1",
            "c1");

        var ex = Assert.Throws<APIErrorsException>(() => CampaignJourneyStructureValidator.Validate(campaign));

        Assert.Contains(ex.Errors.Values, v => v.Contains("TIER_A_COMPOSITE_EMPTY_CHILDREN", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_throws_when_spend_points_outcome_missing_withdrawal_amount_provider()
    {
        var journey = new JourneyNode(
            "root",
            new List<RuleSet>
            {
                new RuleSet
                {
                    Name = "burn-rs",
                    RuleTree = new NumericPropertyRule
                    {
                        LeftProvider = new PathValueProvider("event.amount"),
                        RightProvider = new ConstantValueProvider(0m),
                        Evaluator = new NumericEvaluation(NumEvalType.GreaterThan)
                    },
                    Outcomes = new List<OutcomeBase>
                    {
                        new SpendPointsOutcome { WithdrawlAmountProvider = null }
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
            null,
            DateTimeOffset.UtcNow,
            null,
            null,
            journey,
            "t1",
            "c1");

        var ex = Assert.Throws<APIErrorsException>(() => CampaignJourneyStructureValidator.Validate(campaign));

        Assert.Contains(ex.Errors.Values, v => v.Contains("TIER_A_SPEND_MISSING_WITHDRAWAL_AMOUNT_PROVIDER", StringComparison.Ordinal));
        Assert.Contains(ex.Errors.Values, v => v.Contains("WithdrawlAmountProvider", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_throws_when_temporal_constraint_missing_time_provider()
    {
        var journey = new JourneyNode(
            "root",
            new List<RuleSet>
            {
                new RuleSet
                {
                    Name = "rs",
                    RuleTree = new TemporalConstraintRule(
                        null!,
                        new TemporalEvaluation(TemporalEvalType.After),
                        TimeSpan.FromHours(1))
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
            null,
            DateTimeOffset.UtcNow,
            null,
            null,
            journey,
            "t1",
            "c1");

        var ex = Assert.Throws<APIErrorsException>(() => CampaignJourneyStructureValidator.Validate(campaign));

        Assert.Contains(ex.Errors.Values, v => v.Contains("TIER_A_TEMPORAL_MISSING_TIME_PROVIDER", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_throws_when_historical_rule_missing_historical_value_provider()
    {
        var journey = new JourneyNode(
            "root",
            new List<RuleSet>
            {
                new RuleSet
                {
                    Name = "rs",
                    RuleTree = new HistoricalRule { HistoricalValueProvider = null! }
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
            null,
            DateTimeOffset.UtcNow,
            null,
            null,
            journey,
            "t1",
            "c1");

        var ex = Assert.Throws<APIErrorsException>(() => CampaignJourneyStructureValidator.Validate(campaign));

        Assert.Contains(ex.Errors.Values, v => v.Contains("TIER_A_HISTORICAL_MISSING_VALUE_PROVIDER", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_succeeds_for_numeric_rule_with_path_and_constant()
    {
        var journey = new JourneyNode(
            "root",
            new List<RuleSet>
            {
                new RuleSet
                {
                    Name = "rs",
                    RuleTree = new NumericPropertyRule
                    {
                        LeftProvider = new PathValueProvider("event.amount"),
                        RightProvider = new ConstantValueProvider(0m),
                        Evaluator = new NumericEvaluation(NumEvalType.GreaterThan)
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
            null,
            DateTimeOffset.UtcNow,
            null,
            null,
            journey,
            "t1",
            "c1");

        CampaignJourneyStructureValidator.Validate(campaign);
    }

    [Fact]
    public void Validate_throws_when_numeric_evaluator_comparison_unassigned()
    {
        using var ruleDoc = JsonDocument.Parse("""
            {
              "Kind": "NumericPropertyRule",
              "LeftProvider": { "$type": "PathValueProvider", "PropertyPath": "event.amount" },
              "RightProvider": { "$type": "ConstantValueProvider", "ConstantValue": 0 },
              "Evaluator": { "$type": "NumericEvaluation" }
            }
            """);
        using var outcomesDoc = JsonDocument.Parse("""
            [
              {
                "Kind": "DepositPointsOutcome",
                "AffectedPointAccountTypeIds": [ "pat-1" ],
                "DollarAmountProvider": { "$type": "PathValueProvider", "PropertyPath": "event.amount" }
              }
            ]
            """);

        var ruleSet = new RuleSet("earn", ruleDoc.RootElement.Clone(), "NumericPropertyRule", outcomesDoc.RootElement.Clone(), null);
        var journey = new JourneyNode("root", new List<RuleSet> { ruleSet }, "j1", "j1", null, null);
        CampaignTestServices.AddRootEntryNavigation(journey);
        var campaign = new Campaign("ext", CampaignStatusStrings.Draft, "n", null, DateTimeOffset.UtcNow, null, null, journey, "t1", "c1");

        CampaignJourneyMaterializer.Materialize(campaign);

        var ex = Assert.Throws<APIErrorsException>(() => CampaignJourneyStructureValidator.Validate(campaign));

        Assert.Contains(ex.Errors.Values, v => v.Contains("JOURNEY_EVAL_UNASSIGNED_COMPARISON", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_passes_when_navigation_uses_legacy_EvalType_alias()
    {
        using var navDoc = JsonDocument.Parse("""
            {
              "Entry": {
                "$type": "SimpleNavigationCriteria",
                "navigationType": "Entry",
                "navConstraint": {
                  "Kind": "NumericPropertyRule",
                  "LeftProvider": { "$type": "ConstantValueProvider", "ConstantValue": 1 },
                  "RightProvider": { "$type": "ConstantValueProvider", "ConstantValue": 0 },
                  "Evaluator": { "$type": "NumericEvaluation", "EvalType": "GreaterThanOrEqual" }
                }
              }
            }
            """);

        var journey = new JourneyNode("root", new List<RuleSet>(), "j1", "j1", navDoc.RootElement.Clone(), null);
        var campaign = new Campaign("ext", CampaignStatusStrings.Draft, "n", null, DateTimeOffset.UtcNow, null, null, journey, "t1", "c1");

        CampaignJourneyMaterializer.Materialize(campaign);
        CampaignJourneyStructureValidator.Validate(campaign);

        var entry = Assert.IsType<SimpleNavigationCriteria>(journey.NavigationCriteria![NavigationType.Entry]);
        var rule = Assert.IsType<NumericPropertyRule>(entry.NavConstraint);
        var numeric = Assert.IsType<NumericEvaluation>(rule.Evaluator);
        Assert.Equal(NumEvalType.GreaterThanOrEqual, numeric.Comparison);
    }

    [Fact]
    public async Task UpsertCampaignAsync_invokes_adapter_when_journey_valid()
    {
        var adapter = new RecordingCampaignAdapter();
        var sut = new CampaignService(
            adapter,
            new RecordingPatAdapter(),
            new RecordingPatCache(),
            Logger,
            CampaignDefinitionValidator,
            CampaignTestServices.CreateValidationOrchestrator());

        var campaign = CreateValidCampaignForUpsert();
        var dto = campaign.ToDto()!;
        dto.TenantId = "t1";

        await sut.UpsertCampaignAsync("t1", dto);

        Assert.Equal(1, adapter.UpsertCalls);
    }

    [Fact]
    public async Task UpsertCampaignAsync_does_not_invoke_adapter_when_aggregate_invalid()
    {
        var adapter = new RecordingCampaignAdapter();
        var sut = new CampaignService(
            adapter,
            new RecordingPatAdapter(),
            new RecordingPatCache(),
            Logger,
            CampaignDefinitionValidator,
            CampaignTestServices.CreateValidationOrchestrator());

        var campaign = CreateCampaignWithAggregate(left: new AggregateValueProvider());
        var dto = campaign.ToDto()!;
        dto.TenantId = "t1";

        await Assert.ThrowsAsync<APIErrorsException>(() => sut.UpsertCampaignAsync("t1", dto));

        Assert.Equal(0, adapter.UpsertCalls);
    }

    private static Campaign CreateCampaignWithAggregate(IValueProvider left)
    {
        var rule = new NumericPropertyRule
        {
            LeftProvider = left,
            RightProvider = new ConstantValueProvider(0m),
            Evaluator = new NumericEvaluation(NumEvalType.GreaterThan)
        };

        var journey = new JourneyNode(
            "root",
            new List<RuleSet>
            {
                new RuleSet { Name = "rs", RuleTree = rule }
            },
            "j1",
            "j1",
            null,
            null);

        return new Campaign(
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
    }

    private static Campaign CreateValidCampaignForUpsert()
    {
        var journey = new JourneyNode(
            "root",
            new List<RuleSet>
            {
                new RuleSet
                {
                    Name = "rs",
                    RuleTree = new NumericPropertyRule
                    {
                        LeftProvider = new PathValueProvider("event.amount"),
                        RightProvider = new ConstantValueProvider(0m),
                        Evaluator = new NumericEvaluation(NumEvalType.GreaterThan)
                    }
                }
            },
            "j1",
            "j1",
            null,
            null);
        CampaignTestServices.AddRootEntryNavigation(journey);

        return new Campaign(
            "ext-valid",
            CampaignStatusStrings.Draft,
            "valid",
            null,
            DateTimeOffset.UtcNow,
            null,
            null,
            journey,
            "t1",
            "c1-valid");
    }

    private sealed class NopLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
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

    private sealed class RecordingCampaignAdapter : ICampaignAdapter
    {
        public int UpsertCalls { get; private set; }

        public Task<Campaign> UpsertCampaignAsync(string tenantId, Campaign campaign)
        {
            UpsertCalls++;
            return Task.FromResult(campaign);
        }

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
            //return new PagedResultSet<Campaign>
            //{
            //    Count = 0,
            //    Entities = new List<Campaign>()
            //};
        }
    }

    private sealed class RecordingPatAdapter : IPointAccountTypeAdapter
    {
        public Task DeletePointAccountTypeAsync(string tenantId, string id) => throw new NotImplementedException();
        public Task DeletePointAccountTypeAsync(string tenantId, PointAccountType acctType) => throw new NotImplementedException();
        public Task<PointAccountType> FetchPointAccountTypeAsync(string tenantId, string entityId) => throw new NotImplementedException();
        public Task<PagedResultSet<PointAccountType>> GetAllPointAccountTypesAsync(string tenantId, int pageSize, string continuationToken = null) => throw new NotImplementedException();
        public Task<PointAccountType> UpsertPointAccountTypeAsync(string tenantId, PointAccountType acctType) => throw new NotImplementedException();
    }

    private sealed class RecordingPatCache : IPointAccountTypeCache
    {
        public bool CachePointAccountType(string tenantId, PointAccountType pointAccountType) => throw new NotImplementedException();
        public Task<bool> EnsurePATsLoaded(string tenantId) => throw new NotImplementedException();
        public Task<List<PointAccountType>> GetAllPointAccountTypes(string tenantId) => throw new NotImplementedException();
        public Task<PointAccountType> GetPointAccountTypeAsync(string tenantId, string pointAccountTypeId) => throw new NotImplementedException();
        public Task InvalidatePointAccountTypeAsync(string tenantId, string pointAccountTypeId) => throw new NotImplementedException();
        public Task InvalidateTenantPointAccountTypesAsync(string tenantId) => throw new NotImplementedException();
    }
}
