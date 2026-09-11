using Journeys.Core.RulesEngine;
using Journeys.Core.RulesEngine.Rules;

namespace Journeys.API.Mcp;

/// <summary>
/// Machine-readable Tier A rules-engine contract for MCP tool and resource (keep aligned with
/// CampaignJourneyStructureValidator and docs/superpowers/specs/2026-05-07-rules-engine-campaign-property-requirements-matrix.md).
/// </summary>
public static class RulesEngineMcpContractSummary
{
    public const string MatrixVersion = "2026-06-20";

    public static RulesEngineContractSummaryDto Build()
    {
        var valueProviderKinds = RulesEnginePolymorphicCatalog.ValueProviderKinds;

        return new(
            MatrixVersion,
            "rules-engine-campaign-contract",
            "Apply casingContract before authoring ruleJsonElement; use PascalCase keys inside rules (LeftProvider, not leftProvider); lowercase PropertyPath segments from GetModel symbols. Before UpsertCampaign use governance pre-flight; on journey.materialize enum errors consult enumCatalog; on unknown $type errors match field to typeDiscriminatorCatalog.jsonContexts (providerKinds is deprecated alias of valueProviderKinds; use historicalProviderKinds for HistoricalRule historicalValueProvider); call GetRulePatternRecipes when choosing HistoricalRule vs PointBalanceProvider patterns; after success call GetCampaignAssistantContext before ProcessEvent.",
            new TierAErrorShapeDto(
                "journey.validation",
                "[violation=CODE]",
                new[] { "ruleSet", "nodeId", "rulePath", "kind", "field", "outcomeIndex" }),
            RuleKinds: new[]
            {
                RuleKindDiscriminators.AndRule,
                RuleKindDiscriminators.OrRule,
                RuleKindDiscriminators.NotRule,
                RuleKindDiscriminators.SimpleRule,
                RuleKindDiscriminators.NumericPropertyRule,
                RuleKindDiscriminators.StringPropertyRule,
                RuleKindDiscriminators.DatePropertyRule,
                RuleKindDiscriminators.TaxonomicRule,
                RuleKindDiscriminators.HistoricalRule,
                RuleKindDiscriminators.TemporalConstraintRule
            },
            OutcomeKinds: new[]
            {
                OutcomeKindDiscriminators.DepositPointsOutcome,
                OutcomeKindDiscriminators.SpendPointsOutcome,
                OutcomeKindDiscriminators.ExpirePointsOutcome,
                OutcomeKindDiscriminators.TagOutcome,
                OutcomeKindDiscriminators.NotificationOutcome,
                OutcomeKindDiscriminators.WorkflowOutcome,
                OutcomeKindDiscriminators.RuleStateOutcome
            },
            CriticalRows: new CriticalContractRowDto[]
            {
                new(
                    "simple_rule_three_part",
                    "rule",
                    new[] { RuleKindDiscriminators.NumericPropertyRule, RuleKindDiscriminators.StringPropertyRule, RuleKindDiscriminators.DatePropertyRule },
                    new[] { "LeftProvider", "RightProvider", "Evaluator" },
                    new[] { "TIER_A_SIMPLE_RULE_MISSING_LEFT_PROVIDER", "TIER_A_SIMPLE_RULE_MISSING_RIGHT_PROVIDER", "TIER_A_SIMPLE_RULE_MISSING_EVALUATOR" },
                    "Non-taxonomic SimpleRule family: all three required for Evaluate."),
                new(
                    "taxonomic_rule_left_eval",
                    "rule",
                    new[] { RuleKindDiscriminators.TaxonomicRule },
                    new[] { "LeftProvider", "Evaluator" },
                    new[] { "TIER_A_TAXONOMIC_MISSING_LEFT_PROVIDER", "TIER_A_TAXONOMIC_MISSING_EVALUATOR" },
                    "TaxonomicRule: rightProvider not required at runtime."),
                new(
                    "and_or_non_empty_children",
                    "composite",
                    new[] { RuleKindDiscriminators.AndRule, RuleKindDiscriminators.OrRule },
                    new[] { "Children (non-empty array)" },
                    new[] { "TIER_A_COMPOSITE_EMPTY_CHILDREN" },
                    "AndRule/OrRule must have at least one child rule."),
                new(
                    "not_rule_single_child",
                    "composite",
                    new[] { RuleKindDiscriminators.NotRule },
                    new[] { "Children (exactly one rule)" },
                    new[] { "TIER_A_NOT_RULE_CHILD_COUNT" },
                    "NotRule must have exactly one child."),
                new(
                    "temporal_providers",
                    "rule",
                    new[] { RuleKindDiscriminators.TemporalConstraintRule },
                    new[] { "TimeOfOccurrenceProvider", "TemporalEvaluation" },
                    new[] { "TIER_A_TEMPORAL_MISSING_TIME_PROVIDER", "TIER_A_TEMPORAL_MISSING_EVALUATION" },
                    "TemporalConstraintRule: Comparison is a struct and always present."),
                new(
                    "historical_value_provider",
                    "rule",
                    new[] { RuleKindDiscriminators.HistoricalRule },
                    new[] { "HistoricalValueProvider" },
                    new[] { "TIER_A_HISTORICAL_MISSING_VALUE_PROVIDER" },
                    null),
                new(
                    "historical_aggregation_provider",
                    "rule",
                    new[] { RuleKindDiscriminators.HistoricalRule },
                    new[] { "AggregationValueProvider" },
                    Array.Empty<string>(),
                    "Recommend the same SimpleCalculationProvider instance (shared Id) on aggregationValueProvider when the rule accumulates on each processed event."),
                new(
                    "historical_provider_type",
                    "rule",
                    new[] { RuleKindDiscriminators.HistoricalRule },
                    new[] { "historicalValueProvider.$type = SimpleCalculationProvider" },
                    new[] { "TIER_A_UNKNOWN_TYPE_DISCRIMINATOR", "TIER_A_MISSING_TYPE_DISCRIMINATOR" },
                    "Do not invent composed names (PointBalanceHistoricalValueProvider). Use SimpleCalculationProvider with nested PointBalanceProvider as instanceValueProvider or aggregationValueProvider; share provider Id on historicalValueProvider and aggregationValueProvider."),
                new(
                    "deposit_dollar_provider",
                    "outcome",
                    new[] { OutcomeKindDiscriminators.DepositPointsOutcome },
                    new[] { "DollarAmountProvider" },
                    new[] { "TIER_A_DEPOSIT_MISSING_DOLLAR_AMOUNT_PROVIDER" },
                    null),
                new(
                    "spend_withdrawal_provider",
                    "outcome",
                    new[] { OutcomeKindDiscriminators.SpendPointsOutcome },
                    new[] { "WithdrawlAmountProvider" },
                    new[] { "TIER_A_SPEND_MISSING_WITHDRAWAL_AMOUNT_PROVIDER" },
                    "Property name spelling matches engine (withdrawl)."),
                new(
                    "outcome_event_id_provider",
                    "outcome",
                    new[]
                    {
                        OutcomeKindDiscriminators.DepositPointsOutcome,
                        OutcomeKindDiscriminators.SpendPointsOutcome,
                        OutcomeKindDiscriminators.ExpirePointsOutcome,
                        OutcomeKindDiscriminators.TagOutcome
                    },
                    new[] { "EventIdProvider" },
                    new[] { "TIER_A_OUTCOME_MISSING_EVENT_ID_PROVIDER" },
                    null),
                new(
                    "aggregate_value_provider",
                    "provider",
                    new[] { ProviderKindDiscriminators.AggregateValueProvider },
                    new[] { "RowProvider", "RowPropertyProvider", "AggregateType (!= Undefined)" },
                    Array.Empty<string>(),
                    "Validated under journey path context; messages may omit [violation=] prefix for aggregate-only rows."),
                new(
                    "outcome_affected_pat_ids",
                    "outcome",
                    new[]
                    {
                        OutcomeKindDiscriminators.DepositPointsOutcome,
                        OutcomeKindDiscriminators.SpendPointsOutcome,
                        OutcomeKindDiscriminators.ExpirePointsOutcome
                    },
                    new[] { "AffectedPointAccountTypeIds (non-empty GUID[])" },
                    new[]
                    {
                        "TIER_A_DEPOSIT_MISSING_AFFECTED_PAT",
                        "TIER_A_SPEND_MISSING_AFFECTED_PAT",
                        "TIER_A_EXPIRE_MISSING_AFFECTED_PAT",
                        "TIER_A_OUTCOME_PAT_ALIAS_MISUSED"
                    },
                    "PascalCase key required for bind. Values are PAT GUID strings from PointAccountManifest.items[].pointAccountTypeId — not manifest alias labels.")
            },
            ValueProviderKinds: valueProviderKinds,
            HistoricalProviderKinds: RulesEnginePolymorphicCatalog.HistoricalProviderKinds,
            NavigationCriteriaTypes: RulesEnginePolymorphicCatalog.NavigationCriteriaTypes,
            TypeDiscriminatorCatalog: RulesEnginePolymorphicCatalog.BuildTypeDiscriminatorCatalog(),
            ProviderKinds: valueProviderKinds,
            EvaluatorTypes: RulesEnginePolymorphicCatalog.EvaluatorTypes,
            EnumCatalog: RulesEngineEnumCatalog.Build(),
            RuleSemantics: RulesEnginePatternRecipes.BuildRuleSemantics(),
            CasingContract: JsonCasingContract.Build());
    }

    public sealed record TierAErrorShapeDto(
        string JourneyValidationKeyPrefix,
        string ViolationTokenFormat,
        IReadOnlyList<string> CommonMessageFields);

    public sealed record CriticalContractRowDto(
        string Id,
        string Facet,
        IReadOnlyList<string> KindMatchers,
        IReadOnlyList<string> RequiredJsonProperties,
        IReadOnlyList<string> TierAViolationCodes,
        string? Notes);

    public sealed record RulesEngineContractSummaryDto(
        string MatrixVersion,
        string Artifact,
        string WorkflowHint,
        TierAErrorShapeDto TierAErrorShape,
        IReadOnlyList<string> RuleKinds,
        IReadOnlyList<string> OutcomeKinds,
        IReadOnlyList<CriticalContractRowDto> CriticalRows,
        IReadOnlyList<string> ValueProviderKinds,
        IReadOnlyList<string> HistoricalProviderKinds,
        IReadOnlyList<string> NavigationCriteriaTypes,
        IReadOnlyList<RulesEngineTypeDiscriminatorCatalogEntry> TypeDiscriminatorCatalog,
        IReadOnlyList<string> ProviderKinds,
        IReadOnlyList<string> EvaluatorTypes,
        IReadOnlyList<RulesEngineEnumCatalogEntry> EnumCatalog,
        IReadOnlyList<RulesEnginePatternRecipes.RuleKindSemanticsDto> RuleSemantics,
        JsonCasingContractDto CasingContract);
}
