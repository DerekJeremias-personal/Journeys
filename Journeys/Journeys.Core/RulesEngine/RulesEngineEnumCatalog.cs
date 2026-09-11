using System.Reflection;
using Journeys.Core.RulesEngine.Comparitors.Enums;
using Journeys.Core.RulesEngine.Journey.Enums;
using Journeys.Core.RulesEngine.Providers.Enums;
using Journeys.Core.RulesEngine.Rules;

namespace Journeys.Core.RulesEngine;

/// <summary>
/// Valid enum strings and JSON placement hints for journey rule trees (consumed by GetRulesEngineContractSummary).
/// </summary>
public static class RulesEngineEnumCatalog
{
    private static readonly HashSet<string> ExcludedEnumMembers = new(StringComparer.Ordinal)
    {
        "Unassigned",
        "Undefined"
    };

    public static IReadOnlyList<string> ProviderKinds =>
        typeof(ProviderKindDiscriminators)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)!)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

    public static IReadOnlyList<string> EvaluatorTypes { get; } =
    [
        "NumericEvaluation",
        "DateEvaluation",
        "StringEvaluation",
        "BoolEvaluation"
    ];

    public static IReadOnlyList<RulesEngineEnumCatalogEntry> Build() =>
    [
        Entry(
            nameof(NumEvalType),
            typeof(NumEvalType),
            [
                "evaluator.comparison when evaluator.$type is NumericEvaluation",
                "NumericPropertyRule / SimpleRule family ruleJsonElement"
            ],
            "PascalCase enum member names. Property name is comparison (camelCase). Legacy evalType/EvalType aliases accepted on deserialize. Rule/provider keys elsewhere use PascalCase — see casingContract."),
        Entry(
            nameof(StringEvalType),
            typeof(StringEvalType),
            ["evaluator.comparison when evaluator.$type is StringEvaluation"],
            "PascalCase enum member names. Property name is comparison (camelCase). Rule/provider keys elsewhere use PascalCase — see casingContract."),
        Entry(
            nameof(DateEvalType),
            typeof(DateEvalType),
            ["evaluator.comparison when evaluator.$type is DateEvaluation"],
            "PascalCase enum member names. Property name is comparison (camelCase). Rule/provider keys elsewhere use PascalCase — see casingContract."),
        Entry(
            nameof(TemporalEvalType),
            typeof(TemporalEvalType),
            ["temporalEvaluation.comparison on TemporalConstraintRule (Kind)"],
            "PascalCase enum member names. Property name is comparison (camelCase). Rule/provider keys elsewhere use PascalCase — see casingContract."),
        Entry(
            nameof(AggregateType),
            typeof(AggregateType),
            ["aggregateType on AggregateValueProvider ($type)"],
            "Undefined is invalid for journey saves."),
        Entry(
            nameof(NavigationType),
            typeof(NavigationType),
            [
                "navigationType on SimpleNavigationCriteria ($type)",
                "keys of journey.navigation object (Entry, Exit, Transition)"
            ],
            "PascalCase enum member names.")
    ];

    private static RulesEngineEnumCatalogEntry Entry(
        string id,
        Type enumType,
        IReadOnlyList<string> jsonContexts,
        string? notes) =>
        new(id, EnumValues(enumType), jsonContexts, notes);

    private static IReadOnlyList<string> EnumValues(Type enumType) =>
        Enum.GetNames(enumType)
            .Where(n => !ExcludedEnumMembers.Contains(n))
            .ToList();
}

public sealed record RulesEngineEnumCatalogEntry(
    string Id,
    IReadOnlyList<string> Values,
    IReadOnlyList<string> JsonContexts,
    string? Notes);
