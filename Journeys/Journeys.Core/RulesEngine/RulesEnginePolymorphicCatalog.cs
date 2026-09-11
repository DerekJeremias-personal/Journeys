using System.Reflection;
using System.Text.Json.Serialization;
using Journeys.Core.RulesEngine.Comparitors;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Providers.Historical;
using Journeys.Core.RulesEngine.Rules;

namespace Journeys.Core.RulesEngine;

public static class RulesEnginePolymorphicCatalog
{
    public static IReadOnlyList<string> ValueProviderKinds { get; } =
        GetJsonDerivedTypeDiscriminators(typeof(IValueProvider));

    public static IReadOnlyList<string> HistoricalProviderKinds { get; } =
        GetJsonDerivedTypeDiscriminators(typeof(IHistoricalValueProvider));

    public static IReadOnlyList<string> EvaluatorTypes { get; } =
        GetJsonDerivedTypeDiscriminators(typeof(IEvaluatable));

    public static IReadOnlyList<string> NavigationCriteriaTypes { get; } =
        GetJsonDerivedTypeDiscriminators(typeof(INavigationCriteria));

    public static IReadOnlyList<string> RuleKinds { get; } =
        GetPublicConstStrings(typeof(RuleKindDiscriminators));

    public static IReadOnlyList<string> OutcomeKinds { get; } =
        GetPublicConstStrings(typeof(OutcomeKindDiscriminators));

    public static IReadOnlyList<RulesEngineTypeDiscriminatorCatalogEntry> BuildTypeDiscriminatorCatalog() =>
    [
        Entry(nameof(IValueProvider), ValueProviderKinds,
            ["LeftProvider", "RightProvider", "AggregationValueProvider", "InstanceValueProvider",
             "TimeOfOccurrenceProvider", "DollarAmountProvider", "EarnDateProvider",
             "WithdrawlAmountProvider", "EventIdProvider", "EffectiveStartDateProvider",
             "EffectiveEndDateProvider", "RowProvider", "RowPropertyProvider"],
            "Use $type on every nested IValueProvider object. PropertyPath values use lowercase model symbols (event.ordertotal)."),
        Entry(nameof(IHistoricalValueProvider), HistoricalProviderKinds,
            ["HistoricalValueProvider"],
            "Only SimpleCalculationProvider. Do not invent composed names like PointBalanceHistoricalValueProvider."),
        Entry(nameof(IEvaluatable), EvaluatorTypes, ["Evaluator"], null),
        Entry(nameof(INavigationCriteria), NavigationCriteriaTypes,
            ["journey.navigation values"],
            "Each navigation channel value object uses $type."),
        Entry("RuleBase.Kind", RuleKinds,
            ["ruleJsonElement root", "children[]", "navConstraint", "isApplicableConstraint", "temporalConstraint"],
            "Rule objects use Kind, not $type."),
        Entry("OutcomeBase.Kind", OutcomeKinds,
            ["outcomesJsonElement[]", "navigation.outcomes[]"],
            "Outcome objects use Kind, not $type.")
    ];

    public static bool IsAllowed(IReadOnlyList<string> allowlist, string? discriminator) =>
        !string.IsNullOrWhiteSpace(discriminator)
        && allowlist.Any(a => string.Equals(a, discriminator, StringComparison.OrdinalIgnoreCase));

    private static RulesEngineTypeDiscriminatorCatalogEntry Entry(
        string id, IReadOnlyList<string> allowedTypes, IReadOnlyList<string> jsonContexts, string? notes) =>
        new(id, allowedTypes, jsonContexts, notes);

    private static IReadOnlyList<string> GetJsonDerivedTypeDiscriminators(Type interfaceType) =>
        interfaceType
            .GetCustomAttributes<JsonDerivedTypeAttribute>(inherit: false)
            .Select(a => a.TypeDiscriminator?.ToString() ?? a.DerivedType.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

    private static IReadOnlyList<string> GetPublicConstStrings(Type staticClass) =>
        staticClass
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)!)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();
}

public sealed record RulesEngineTypeDiscriminatorCatalogEntry(
    string Id,
    IReadOnlyList<string> AllowedTypes,
    IReadOnlyList<string> JsonContexts,
    string? Notes);
