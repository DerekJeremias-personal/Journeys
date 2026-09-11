using System.Text.Json;
using Journeys.API.Mcp;
using Journeys.Core.RulesEngine;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Providers.Historical;
using Journeys.Core.RulesEngine.Rules;
using Xunit;

namespace Journeys.Tests.Mcp;

public class RulesEngineMcpContractSummaryTests
{
    [Fact]
    public void Build_includes_enum_catalog_with_NumEvalType_values()
    {
        var dto = RulesEngineMcpContractSummary.Build();

        Assert.Equal("2026-06-20", dto.MatrixVersion);
        Assert.NotEmpty(dto.EnumCatalog);

        var numEval = dto.EnumCatalog.Single(e => e.Id == "NumEvalType");
        Assert.Equal(
            ["Equal", "GreaterThan", "GreaterThanOrEqual", "LessThan", "LessThanOrEqual"],
            numEval.Values);
        Assert.Contains(numEval.JsonContexts, c => c.Contains("NumericEvaluation", StringComparison.Ordinal));
        Assert.Contains("comparison", numEval.Notes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_AggregateType_excludes_Undefined()
    {
        var dto = RulesEngineMcpContractSummary.Build();
        var aggregate = dto.EnumCatalog.Single(e => e.Id == "AggregateType");

        Assert.Equal(["Sum", "Average", "Min", "Max", "Count"], aggregate.Values);
        Assert.DoesNotContain("Undefined", aggregate.Values);
    }

    [Fact]
    public void Build_ValueProviderKinds_matches_polymorphic_catalog()
    {
        var dto = RulesEngineMcpContractSummary.Build();

        Assert.Equal(RulesEnginePolymorphicCatalog.ValueProviderKinds, dto.ValueProviderKinds);
        Assert.Contains(nameof(PointBalanceProvider), dto.ValueProviderKinds);
        Assert.Contains(ProviderKindDiscriminators.PathValueProvider, dto.ValueProviderKinds);
        Assert.Contains(ProviderKindDiscriminators.AggregateValueProvider, dto.ValueProviderKinds);
    }

    [Fact]
    public void Build_ProviderKinds_is_alias_of_ValueProviderKinds()
    {
        var dto = RulesEngineMcpContractSummary.Build();

        Assert.Same(dto.ValueProviderKinds, dto.ProviderKinds);
        Assert.Equal(dto.ValueProviderKinds, dto.ProviderKinds);
    }

    [Fact]
    public void Build_HistoricalProviderKinds_only_SimpleCalculationProvider()
    {
        var dto = RulesEngineMcpContractSummary.Build();

        Assert.Equal([nameof(SimpleCalculationProvider)], dto.HistoricalProviderKinds);
        Assert.DoesNotContain(nameof(PointBalanceProvider), dto.HistoricalProviderKinds);
    }

    [Fact]
    public void Build_includes_polymorphic_catalog_fields()
    {
        var dto = RulesEngineMcpContractSummary.Build();

        Assert.Equal(RulesEnginePolymorphicCatalog.NavigationCriteriaTypes, dto.NavigationCriteriaTypes);
        Assert.Equal(RulesEnginePolymorphicCatalog.EvaluatorTypes, dto.EvaluatorTypes);
        Assert.NotEmpty(dto.TypeDiscriminatorCatalog);

        var historicalEntry = dto.TypeDiscriminatorCatalog.Single(e => e.Id == nameof(IHistoricalValueProvider));
        Assert.Equal([nameof(SimpleCalculationProvider)], historicalEntry.AllowedTypes);
        Assert.Contains("HistoricalValueProvider", historicalEntry.JsonContexts);
    }

    [Fact]
    public void Build_includes_historical_provider_type_critical_row()
    {
        var dto = RulesEngineMcpContractSummary.Build();
        var row = dto.CriticalRows.Single(r => r.Id == "historical_provider_type");

        Assert.Equal("rule", row.Facet);
        Assert.Equal([RuleKindDiscriminators.HistoricalRule], row.KindMatchers);
        Assert.Equal(["historicalValueProvider.$type = SimpleCalculationProvider"], row.RequiredJsonProperties);
        Assert.Equal(
            ["TIER_A_UNKNOWN_TYPE_DISCRIMINATOR", "TIER_A_MISSING_TYPE_DISCRIMINATOR"],
            row.TierAViolationCodes);
        Assert.Contains("PointBalanceHistoricalValueProvider", row.Notes);
    }

    [Fact]
    public void Build_includes_ruleSemantics_with_HistoricalRule()
    {
        var dto = RulesEngineMcpContractSummary.Build();

        Assert.NotEmpty(dto.RuleSemantics);
        var historical = dto.RuleSemantics.Single(s => s.Id == "HistoricalRule");
        Assert.Contains("aggregationValueProvider", historical.RecommendedProperties);
        Assert.Contains("historical-spend-threshold", historical.RelatedPatterns);
    }

    [Fact]
    public void Build_includes_historical_aggregation_provider_critical_row()
    {
        var dto = RulesEngineMcpContractSummary.Build();
        var row = dto.CriticalRows.Single(r => r.Id == "historical_aggregation_provider");

        Assert.Equal("rule", row.Facet);
        Assert.Equal(["AggregationValueProvider"], row.RequiredJsonProperties);
        Assert.Contains("SimpleCalculationProvider", row.Notes);
    }

    [Fact]
    public void Build_workflowHint_mentions_typeDiscriminatorCatalog_and_historicalProviderKinds()
    {
        var dto = RulesEngineMcpContractSummary.Build();

        Assert.Contains("typeDiscriminatorCatalog", dto.WorkflowHint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("historicalProviderKinds", dto.WorkflowHint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GetRulePatternRecipes", dto.WorkflowHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_serializes_enumCatalog_with_camelCase_properties()
    {
        var dto = RulesEngineMcpContractSummary.Build();
        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("enumCatalog", out var catalog));
        Assert.Equal(JsonValueKind.Array, catalog.ValueKind);
        Assert.True(catalog.GetArrayLength() >= 6);
        Assert.True(root.TryGetProperty("valueProviderKinds", out _));
        Assert.True(root.TryGetProperty("historicalProviderKinds", out _));
        Assert.True(root.TryGetProperty("navigationCriteriaTypes", out _));
        Assert.True(root.TryGetProperty("typeDiscriminatorCatalog", out var typeCatalog));
        Assert.True(typeCatalog.GetArrayLength() >= 5);
        Assert.True(root.TryGetProperty("providerKinds", out _));
        Assert.True(root.TryGetProperty("evaluatorTypes", out var evaluators));
        Assert.Contains("NumericEvaluation", evaluators.EnumerateArray().Select(e => e.GetString()));
        Assert.True(root.TryGetProperty("casingContract", out var casingContract));
        Assert.Equal(JsonValueKind.Array, casingContract.GetProperty("layers").ValueKind);
    }

    [Fact]
    public void Build_includes_casingContract_with_three_layers()
    {
        var dto = RulesEngineMcpContractSummary.Build();
        Assert.NotNull(dto.CasingContract);
        Assert.Equal(3, dto.CasingContract!.Layers.Count);
        Assert.Contains(dto.CasingContract.AntiPatterns,
            a => a.Contains("AffectedPointAccountTypeIds", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_simple_rule_critical_row_uses_PascalCase_properties()
    {
        var dto = RulesEngineMcpContractSummary.Build();
        var row = dto.CriticalRows.Single(r => r.Id == "simple_rule_three_part");
        Assert.Equal(["LeftProvider", "RightProvider", "Evaluator"], row.RequiredJsonProperties);
    }

    [Fact]
    public void Build_includes_outcome_affected_pat_ids_critical_row()
    {
        var dto = RulesEngineMcpContractSummary.Build();
        var row = dto.CriticalRows.Single(r => r.Id == "outcome_affected_pat_ids");
        Assert.Contains("AffectedPointAccountTypeIds", row.RequiredJsonProperties[0], StringComparison.Ordinal);
        Assert.Contains("TIER_A_DEPOSIT_MISSING_AFFECTED_PAT", row.TierAViolationCodes);
    }

    [Fact]
    public void RulesEngineEnumCatalog_Build_includes_all_P1_enums()
    {
        var catalog = RulesEngineEnumCatalog.Build();
        var ids = catalog.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("NumEvalType", ids);
        Assert.Contains("StringEvalType", ids);
        Assert.Contains("DateEvalType", ids);
        Assert.Contains("TemporalEvalType", ids);
        Assert.Contains("AggregateType", ids);
        Assert.Contains("NavigationType", ids);
    }
}
