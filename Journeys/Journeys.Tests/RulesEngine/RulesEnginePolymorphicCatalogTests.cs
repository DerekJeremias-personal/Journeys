using System.Reflection;
using System.Text.Json.Serialization;
using Journeys.Core.RulesEngine;
using Journeys.Core.RulesEngine.Comparitors;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Providers.Historical;
using Journeys.Core.RulesEngine.Rules;
using Xunit;

namespace Journeys.Tests.RulesEngine;

public class RulesEnginePolymorphicCatalogTests
{
    [Fact]
    public void ValueProviderKinds_includes_PointBalanceProvider()
    {
        Assert.Contains(nameof(PointBalanceProvider), RulesEnginePolymorphicCatalog.ValueProviderKinds);
    }

    [Fact]
    public void ValueProviderKinds_matches_IValueProvider_JsonDerivedType_discriminators()
    {
        var expected = GetJsonDerivedTypeDiscriminators(typeof(IValueProvider));

        Assert.Equal(expected, RulesEnginePolymorphicCatalog.ValueProviderKinds);
    }

    [Fact]
    public void HistoricalProviderKinds_only_SimpleCalculationProvider()
    {
        Assert.Equal(
            [nameof(SimpleCalculationProvider)],
            RulesEnginePolymorphicCatalog.HistoricalProviderKinds);
    }

    [Fact]
    public void HistoricalProviderKinds_excludes_PointBalanceProvider()
    {
        Assert.DoesNotContain(nameof(PointBalanceProvider), RulesEnginePolymorphicCatalog.HistoricalProviderKinds);
    }

    [Fact]
    public void RuleKinds_includes_HistoricalRule()
    {
        Assert.Contains(RuleKindDiscriminators.HistoricalRule, RulesEnginePolymorphicCatalog.RuleKinds);
    }

    [Fact]
    public void RuleKinds_matches_RuleKindDiscriminators_constants()
    {
        var expected = typeof(RuleKindDiscriminators)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)!)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(expected, RulesEnginePolymorphicCatalog.RuleKinds);
    }

    [Fact]
    public void EvaluatorTypes_matches_IEvaluatable_JsonDerivedType_discriminators()
    {
        var expected = GetJsonDerivedTypeDiscriminators(typeof(IEvaluatable));

        Assert.Equal(expected, RulesEnginePolymorphicCatalog.EvaluatorTypes);
    }

    [Fact]
    public void NavigationCriteriaTypes_matches_INavigationCriteria_JsonDerivedType_discriminators()
    {
        var expected = GetJsonDerivedTypeDiscriminators(typeof(INavigationCriteria));

        Assert.Equal(expected, RulesEnginePolymorphicCatalog.NavigationCriteriaTypes);
    }

    [Fact]
    public void BuildTypeDiscriminatorCatalog_includes_IHistoricalValueProvider_entry()
    {
        var catalog = RulesEnginePolymorphicCatalog.BuildTypeDiscriminatorCatalog();
        var entry = catalog.SingleOrDefault(e => e.Id == nameof(IHistoricalValueProvider));

        Assert.NotNull(entry);
        Assert.Equal(
            [nameof(SimpleCalculationProvider)],
            entry.AllowedTypes);
        Assert.Contains("HistoricalValueProvider", entry.JsonContexts);
    }

    [Fact]
    public void BuildTypeDiscriminatorCatalog_includes_all_expected_ids()
    {
        var ids = RulesEnginePolymorphicCatalog.BuildTypeDiscriminatorCatalog()
            .Select(e => e.Id)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(nameof(IValueProvider), ids);
        Assert.Contains(nameof(IHistoricalValueProvider), ids);
        Assert.Contains(nameof(IEvaluatable), ids);
        Assert.Contains(nameof(INavigationCriteria), ids);
        Assert.Contains("RuleBase.Kind", ids);
        Assert.Contains("OutcomeBase.Kind", ids);
    }

    [Fact]
    public void IsAllowed_matches_case_insensitive()
    {
        var allowlist = RulesEnginePolymorphicCatalog.HistoricalProviderKinds;

        Assert.True(RulesEnginePolymorphicCatalog.IsAllowed(allowlist, "simplecalculationprovider"));
        Assert.True(RulesEnginePolymorphicCatalog.IsAllowed(allowlist, nameof(SimpleCalculationProvider)));
        Assert.False(RulesEnginePolymorphicCatalog.IsAllowed(allowlist, nameof(PointBalanceProvider)));
        Assert.False(RulesEnginePolymorphicCatalog.IsAllowed(allowlist, null));
        Assert.False(RulesEnginePolymorphicCatalog.IsAllowed(allowlist, ""));
        Assert.False(RulesEnginePolymorphicCatalog.IsAllowed(allowlist, "   "));
    }

    [Fact]
    public void BuildTypeDiscriminatorCatalog_IValueProvider_jsonContexts_use_PascalCase()
    {
        var entry = RulesEnginePolymorphicCatalog.BuildTypeDiscriminatorCatalog()
            .Single(e => e.Id == nameof(IValueProvider));
        Assert.Contains("LeftProvider", entry.JsonContexts);
        Assert.Contains("DollarAmountProvider", entry.JsonContexts);
        Assert.DoesNotContain(entry.JsonContexts, c => c == "leftProvider");
        Assert.Contains("lowercase model symbols", entry.Notes ?? "", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> GetJsonDerivedTypeDiscriminators(Type interfaceType) =>
        interfaceType
            .GetCustomAttributes<JsonDerivedTypeAttribute>(inherit: false)
            .Select(a => a.TypeDiscriminator?.ToString() ?? a.DerivedType.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
}
