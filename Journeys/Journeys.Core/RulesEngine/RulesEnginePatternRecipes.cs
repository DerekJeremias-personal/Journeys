using System.Text.Json;
using System.Text.Json.Serialization;
using Journeys.Core.RulesEngine.Rules;

namespace Journeys.Core.RulesEngine;

/// <summary>
/// Pattern recipes (A–F) and rule-kind semantics for campaign agent journey authoring.
/// </summary>
public static class RulesEnginePatternRecipes
{
    public const string SchemaVersion = "2026-06-16";

    public static RulesEnginePatternRecipesDto Build() =>
        new(
            SchemaVersion,
            Patterns: BuildPatterns(),
            RuleSemantics: BuildRuleSemantics());

    public static IReadOnlyList<RuleKindSemanticsDto> BuildRuleSemantics() =>
    [
        new RuleKindSemanticsDto(
            RuleKindDiscriminators.HistoricalRule,
            "Rolling aggregate over processed events (Sum/Count/Avg/Min/Max) compared left >= right when RightProvider present; state keyed CampaignId|RuleId.",
            ["historicalValueProvider", "aggregateType"],
            ["aggregationValueProvider", "rightProvider", "id", "isApplicableConstraint"],
            [
                "historicalValueProvider.$type must be SimpleCalculationProvider",
                "SimpleCalculationProvider requires temporalConstraint and instanceValueProvider for Sum/Count/Avg/Min/Max",
                "Wire the same SimpleCalculationProvider to aggregationValueProvider when accumulating on each event"
            ],
            "left >= right via HistoricalRule; do not add NumericEvaluation on HistoricalRule itself",
            ["historical-spend-threshold", "historical-event-count", "historical-taxonomy-filtered"]),
        new RuleKindSemanticsDto(
            RuleKindDiscriminators.NumericPropertyRule,
            "Compares numeric value from current event (LeftProvider) to RightProvider using NumericEvaluation.",
            ["leftProvider", "rightProvider", "evaluator"],
            ["isApplicableConstraint"],
            ["Use PathValueProvider on event fields for current-event-only checks"],
            "NumericEvaluation.Comparison on left vs right",
            ["temporal-event-gate"]),
        new RuleKindSemanticsDto(
            RuleKindDiscriminators.SimpleRule,
            "Bool comparison for navigation constraints and always-true entry gates.",
            ["leftProvider", "rightProvider", "evaluator"],
            Array.Empty<string>(),
            ["BoolEvaluation with ConstantValueProvider(true) pairs for unconditional Entry navigation"],
            "BoolEvaluation Equal",
            ["tier-navigation-point-balance"]),
        new RuleKindSemanticsDto(
            RuleKindDiscriminators.AndRule,
            "All children must pass; use for composite qualification and tier band bounds.",
            ["children (non-empty)"],
            Array.Empty<string>(),
            ["Prefer shallow trees (2–3 children); place cheap property/taxonomy checks before HistoricalRule legs"],
            "All children evaluate true",
            ["composite-qualification", "tier-navigation-point-balance", "historical-taxonomy-filtered"]),
        new RuleKindSemanticsDto(
            RuleKindDiscriminators.TemporalConstraintRule,
            "Time window for historical aggregates or current-event time gates.",
            ["timeOfOccurrenceProvider", "temporalEvaluation"],
            ["comparison (TimeSpan string, e.g. \"30.00:00:00\")"],
            ["Nest inside SimpleCalculationProvider.temporalConstraint for rolling windows"],
            "TemporalEvaluation After/Before vs Comparison duration",
            ["temporal-event-gate", "historical-spend-threshold", "historical-event-count"]),
        new RuleKindSemanticsDto(
            "PointBalanceProvider",
            "Reads current PAT balance from prior outcomes (deposits/burns/expiry) — not rolling event history.",
            ["pointAccountTypeId"],
            Array.Empty<string>(),
            ["Use on navigation Transition/Entry navConstraint for tier bands; never as historicalValueProvider.$type"],
            "Compared via parent SimpleRule + NumericEvaluation",
            ["tier-navigation-point-balance"])
    ];

    private static IReadOnlyList<PatternRecipeDto> BuildPatterns() =>
    [
        Pattern(
            "historical-spend-threshold",
            "Rolling spend threshold",
            "Member qualification depends on rolling sum of a numeric event field (or line items) over a time window — e.g. prior-period spend, \"$500 in 30 days\".",
            [
                "NumericPropertyRule on event.ordertotal alone (current order only)",
                "HistoricalRule for tier navigation when tiers use TQP PAT + PointBalanceProvider",
                "PointBalanceProvider as historicalValueProvider.$type"
            ],
            "RuleSet on journey root or tier node — HistoricalRule in ruleJsonElement with outcomes when threshold met.",
            MinimalSkeleton("""
                {
                  "Kind": "HistoricalRule",
                  "Id": "spend-30d",
                  "AggregateType": 1,
                  "HistoricalValueProvider": {
                    "$type": "SimpleCalculationProvider",
                    "Kind": "SimpleCalculationProvider",
                    "Id": "spend-30d",
                    "AggregateType": 1,
                    "TemporalConstraint": {
                      "Kind": "TemporalConstraintRule",
                      "TimeOfOccurrenceProvider": { "$type": "PathValueProvider", "Kind": "PathValueProvider", "PropertyPath": "event.timestamp" },
                      "TemporalEvaluation": { "Comparison": 4 },
                      "Comparison": "30.00:00:00"
                    },
                    "InstanceValueProvider": { "$type": "PathValueProvider", "Kind": "PathValueProvider", "PropertyPath": "event.ordertotal" }
                  },
                  "AggregationValueProvider": { "$type": "SimpleCalculationProvider", "Kind": "SimpleCalculationProvider", "Id": "spend-30d" },
                  "RightProvider": { "$type": "ConstantValueProvider", "Kind": "ConstantValueProvider", "Value": 500 }
                }
                """),
            "historical-spend-threshold-30d",
            ["get_rules_engine_contract_summary", "get_rule_pattern_recipes"]),
        Pattern(
            "tier-navigation-point-balance",
            "Tier navigation via point balance",
            "Tier levels from accumulated qualification points on a NonSpendable TQP PAT; earn outcomes deposit into TQP; navigation uses PointBalanceProvider thresholds.",
            [
                "HistoricalRule on order totals for tier navigation when design uses qual-point deposits",
                "PointBalanceProvider as historicalValueProvider.$type",
                "Invented provider names (PointBalanceHistoricalValueProvider)"
            ],
            "Root Entry: unconditional SimpleRule nav; each tier child: Transition with AndRule + PointBalanceProvider(TQP) bounds; RuleSets with DepositPointsOutcome → TQP + Spendable.",
            MinimalSkeleton("""
                {
                  "navigation": {
                    "Transition": {
                      "$type": "SimpleNavigationCriteria",
                      "NavConstraint": {
                        "Kind": "AndRule",
                        "Children": [
                          {
                            "Kind": "NumericPropertyRule",
                            "LeftProvider": { "$type": "PointBalanceProvider", "Kind": "PointBalanceProvider", "PointAccountTypeId": "<tqp-pat-id>" },
                            "RightProvider": { "$type": "ConstantValueProvider", "Kind": "ConstantValueProvider", "Value": 10000 },
                            "Evaluator": { "$type": "NumericEvaluation", "Comparison": 2 }
                          },
                          {
                            "Kind": "NumericPropertyRule",
                            "LeftProvider": { "$type": "PointBalanceProvider", "Kind": "PointBalanceProvider", "PointAccountTypeId": "<tqp-pat-id>" },
                            "RightProvider": { "$type": "ConstantValueProvider", "Kind": "ConstantValueProvider", "Value": 25000 },
                            "Evaluator": { "$type": "NumericEvaluation", "Comparison": 4 }
                          }
                        ]
                      }
                    }
                  }
                }
                """),
            "tier-system-campaign",
            ["get_rule_pattern_recipes", "get_example_campaign", "get_rules_engine_contract_summary"]),
        Pattern(
            "historical-taxonomy-filtered",
            "Taxonomy-filtered historical aggregate",
            "Rolling spend or quantity in a category (e.g. Electronics) over a window — AndRule with TaxonomicRule on current event + HistoricalRule with row Constraint.",
            [
                "HistoricalRule without TaxonomicRule when category filter is required",
                "Flat NumericPropertyRule when only current line matters"
            ],
            "RuleSet with AndRule: TaxonomicRule (current event) + HistoricalRule (pattern A with AggregateValueProvider.Constraint).",
            MinimalSkeleton("""
                {
                  "Kind": "AndRule",
                  "Children": [
                    { "Kind": "TaxonomicRule", "LeftProvider": { "$type": "PathValueProvider", "PropertyPath": "event.items" }, "Evaluator": { "$type": "StringEvaluation", "Comparison": 1 } },
                    { "Kind": "HistoricalRule", "Id": "cat-spend-30d", "AggregateType": 1, "HistoricalValueProvider": { "$type": "SimpleCalculationProvider", "Id": "cat-spend-30d" } }
                  ]
                }
                """),
            "historical-by-category-campaign",
            ["get_example_campaign", "get_rules_engine_contract_summary"]),
        Pattern(
            "historical-event-count",
            "Historical event frequency",
            "Qualification based on count of events in a rolling window — e.g. \"3+ orders in 90 days\".",
            [
                "AggregateValueProvider line-item sum when intent is event count",
                "NumericPropertyRule counting current event only"
            ],
            "RuleSet on root — HistoricalRule with AggregateType Count, instanceValueProvider ConstantValueProvider(1).",
            MinimalSkeleton("""
                {
                  "Kind": "HistoricalRule",
                  "Id": "orders-90d",
                  "AggregateType": 5,
                  "HistoricalValueProvider": {
                    "$type": "SimpleCalculationProvider",
                    "Kind": "SimpleCalculationProvider",
                    "Id": "orders-90d",
                    "AggregateType": 5,
                    "TemporalConstraint": {
                      "Kind": "TemporalConstraintRule",
                      "TimeOfOccurrenceProvider": { "$type": "PathValueProvider", "PropertyPath": "event.timestamp" },
                      "TemporalEvaluation": { "Comparison": 4 },
                      "Comparison": "90.00:00:00"
                    },
                    "InstanceValueProvider": { "$type": "ConstantValueProvider", "Kind": "ConstantValueProvider", "Value": 1 }
                  },
                  "AggregationValueProvider": { "$type": "SimpleCalculationProvider", "Kind": "SimpleCalculationProvider", "Id": "orders-90d" },
                  "RightProvider": { "$type": "ConstantValueProvider", "Kind": "ConstantValueProvider", "Value": 3 }
                }
                """),
            "historical-count-90d",
            ["get_rule_pattern_recipes", "get_rules_engine_contract_summary"]),
        Pattern(
            "temporal-event-gate",
            "Temporal event gating",
            "Time-based qualification: rolling windows inside SimpleCalculationProvider, or current-event DatePropertyRule / TemporalConstraintRule as IsApplicableConstraint.",
            [
                "Standalone TemporalConstraintRule without parent rule context",
                "HistoricalRule when the ask is PAT balance not event time"
            ],
            "TemporalConstraintRule nested in SimpleCalculationProvider for rolling windows; DatePropertyRule on event.timestamp for current-event-only gates.",
            MinimalSkeleton("""
                {
                  "Kind": "TemporalConstraintRule",
                  "TimeOfOccurrenceProvider": { "$type": "PathValueProvider", "PropertyPath": "event.timestamp" },
                  "TemporalEvaluation": { "Comparison": 4 },
                  "Comparison": "30.00:00:00"
                }
                """),
            null,
            ["get_rules_engine_contract_summary"]),
        Pattern(
            "composite-qualification",
            "Composite qualification (And/Or)",
            "Multiple signals must pass together — e.g. category + rolling spend, or property + historical count.",
            [
                "Deeply nested AndRule trees (>3 levels) without outcomes",
                "Mixing HistoricalRule and PointBalanceProvider in one leg without clear business mapping"
            ],
            "AndRule with 2–3 children from patterns A/D + property/taxonomy rules; outcomes on RuleSet when composite passes.",
            MinimalSkeleton("""
                {
                  "Kind": "AndRule",
                  "Children": [
                    { "Kind": "NumericPropertyRule", "LeftProvider": { "$type": "PathValueProvider", "PropertyPath": "event.ordertotal" }, "RightProvider": { "$type": "ConstantValueProvider", "Value": 0 }, "Evaluator": { "$type": "NumericEvaluation", "Comparison": 1 } },
                    { "Kind": "HistoricalRule", "Id": "spend-30d", "AggregateType": 1, "HistoricalValueProvider": { "$type": "SimpleCalculationProvider", "Id": "spend-30d" }, "RightProvider": { "$type": "ConstantValueProvider", "Value": 500 } }
                  ]
                }
                """),
            "historical-by-category-campaign",
            ["get_rule_pattern_recipes", "get_example_campaign"])
    ];

    private static PatternRecipeDto Pattern(
        string id,
        string title,
        string whenToUse,
        IReadOnlyList<string> antiPatterns,
        string journeyPlacement,
        JsonElement minimalSkeleton,
        string? relatedExampleCampaignId,
        IReadOnlyList<string> relatedTools) =>
        new(id, title, whenToUse, antiPatterns, journeyPlacement, minimalSkeleton, relatedExampleCampaignId, relatedTools);

    private static JsonElement MinimalSkeleton(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();

    public sealed record RulesEnginePatternRecipesDto(
        string SchemaVersion,
        IReadOnlyList<PatternRecipeDto> Patterns,
        IReadOnlyList<RuleKindSemanticsDto> RuleSemantics);

    public sealed record PatternRecipeDto(
        string Id,
        string Title,
        string WhenToUse,
        IReadOnlyList<string> AntiPatterns,
        string JourneyPlacement,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        JsonElement MinimalSkeleton,
        string? RelatedExampleCampaignId,
        IReadOnlyList<string> RelatedTools);

    public sealed record RuleKindSemanticsDto(
        string Id,
        string Evaluates,
        IReadOnlyList<string> RequiredProperties,
        IReadOnlyList<string> RecommendedProperties,
        IReadOnlyList<string> NestedRequirements,
        string ComparisonSemantics,
        IReadOnlyList<string> RelatedPatterns);
}
