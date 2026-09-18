/**
 * Serialize the editor's `Condition[]` + composite into the canonical wire
 * `Rule` (PascalCase JsonElement) shape.
 *
 * Wire shapes per `@/lib/campaign-types/loyalty-rules` (sourced from
 * `Elevate.ELP.Core/RulesEngine/Rules/`).
 */

import type {
  AndRule,
  ConstantValueProvider,
  Evaluator,
  HistoricalRule as LoyaltyHistoricalRule,
  LoyaltyRule,
  OrRule,
  PathValueProvider,
  Provider,
  SimpleRule,
  TaxonomicRule,
} from "@/lib/campaign-types";

import {
  EVALUATOR_BY_VALUE,
  defaultHistoricalConfig,
  type Condition,
  type CompositeKind,
  type ConditionSide,
  type HistoricalConditionConfig,
  type UiProviderType,
} from "./types";

// ─────────────────────────────────────────────────────────────────────────────
// Provider serialization
// ─────────────────────────────────────────────────────────────────────────────

function buildPathValueProvider(path: string): PathValueProvider {
  return {
    Kind: "PathValueProvider",
    $type: "PathValueProvider",
    PropertyPath: path,
  };
}

function buildPointBalanceProvider(pointAccountTypeId: string): PathValueProvider {
  // Wire reality (per `loyalty-rules.ts` JSDoc): `PointBalanceProvider` ships
  // `Kind: "PathValueProvider"` (parent's literal) + `$type: "PointBalanceProvider"`.
  return {
    Kind: "PathValueProvider",
    $type: "PointBalanceProvider",
    PointAccountTypeId: pointAccountTypeId,
  };
}

function buildConstantProvider(value: string | number | boolean): ConstantValueProvider {
  return {
    Kind: "ConstantValueProvider",
    $type: "ConstantValueProvider",
    Value: value,
  };
}

function buildSideProvider(side: ConditionSide): Provider {
  switch (side.providerType) {
    case "PathValueProvider":
      return buildPathValueProvider(side.propertyPath);
    case "PointBalanceProvider":
      return buildPointBalanceProvider(side.pointAccountTypeId);
    case "ConstantValueProvider":
      return buildConstantProvider(side.constantValue);
    case "AggregateValueProvider":
      // Editor v1 surfaces aggregate as a typed picker; without nested authoring
      // we ship the discriminator + Sum default. Users author finer details via
      // raw JSON (future enhancement).
      return {
        Kind: "AggregateValueProvider",
        $type: "AggregateValueProvider",
        AggregateType: "Sum",
      };
    case "ProductListProvider":
      // The wire-side `ProductListProvider` is encoded as a `TaxonomicRule`
      // when it appears as a left/right operand within a SimpleRule. The
      // editor uses dedicated taxonomic conditions for this; if a user picks
      // ProductListProvider on a Standard condition we emit a no-op constant.
      return buildConstantProvider("");
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Evaluator serialization
// ─────────────────────────────────────────────────────────────────────────────

function buildEvaluator(evaluatorValue: string): Evaluator {
  const meta = EVALUATOR_BY_VALUE.get(evaluatorValue);
  if (!meta) {
    return { $type: "BoolEvaluation", Comparison: "Equal" };
  }
  return {
    $type: meta.evaluatorType,
    Comparison: meta.numericCode,
  };
}

// ─────────────────────────────────────────────────────────────────────────────
// Rule serialization
// ─────────────────────────────────────────────────────────────────────────────

function buildSimpleRule(condition: Condition): SimpleRule {
  return {
    Kind: "SimpleRule",
    LeftProvider: buildSideProvider(condition.left),
    RightProvider: buildSideProvider(condition.right),
    Evaluator: buildEvaluator(condition.evaluator),
  };
}

function buildTaxonomicRule(condition: Condition): TaxonomicRule {
  const selection = condition.productSelection;
  const includeMode = selection?.mode === "include";
  const categoryPaths = selection?.categories.map((c) => c.categoryPath) ?? [];
  const entityIds = selection?.entities.map((e) => e.id) ?? [];
  return {
    Kind: "TaxonomicRule",
    LeftProvider: {
      Kind: "PathValueProvider",
      $type: "PathValueProvider",
      PropertyPath: condition.itemsPropertyPath || "event.items",
    },
    RightProvider: {
      Kind: "ConstantValueProvider",
      $type: "ConstantValueProvider",
      Value: "alwaystrue",
    },
    Evaluator: { $type: "StringEvaluation", Comparison: 1 },
    IncludedTreeNodes: includeMode ? categoryPaths : [],
    IncludedIds: includeMode ? entityIds : [],
    ExcludedTreeNodes: includeMode ? [] : categoryPaths,
    ExcludedIds: includeMode ? [] : entityIds,
    TaxonomyType: condition.taxonomyType || "TreeRoot",
    TaxonomyId: condition.taxonomyId || (selection?.categories[0]?.catalogId ?? ""),
    KeySymbolPath: condition.keySymbolPath || "sku",
  };
}

function toTimeSpanDays(days: number): string {
  const d = Number.isFinite(days) ? Math.max(1, Math.floor(days)) : 1;
  return `${d}.00:00:00`;
}

function buildHistoricalRuleFromConfig(config: HistoricalConditionConfig): LoyaltyHistoricalRule {
  const temporalConstraint = {
    Kind: "TemporalConstraintRule",
    TimeOfOccurrenceProvider: {
      Kind: "PathValueProvider",
      $type: "PathValueProvider",
      PropertyPath: config.occurrencePath,
    },
    TemporalEvaluation: {
      $type: "TemporalEvaluation",
      ComparisonType: config.temporalComparison,
    },
    Comparison: toTimeSpanDays(config.windowDays),
  };

  const historicalProvider = {
    Kind: "SimpleCalculationProvider",
    $type: "SimpleCalculationProvider",
    AggregateType: config.aggregateType,
    TemporalConstraint: temporalConstraint,
    InstanceValueProvider: {
      Kind: "PathValueProvider",
      $type: "PathValueProvider",
      PropertyPath: config.valuePath,
    },
  };

  const rightProvider =
    config.thresholdProviderType === "PathValueProvider"
      ? buildPathValueProvider(config.thresholdPath)
      : config.thresholdProviderType === "PointBalanceProvider"
        ? buildPointBalanceProvider(config.thresholdPointAccountTypeId)
        : buildConstantProvider(config.thresholdValue);

  return {
    Kind: "HistoricalRule",
    AggregateType: config.aggregateType,
    AggregationValueProvider: historicalProvider as unknown as Provider,
    HistoricalValueProvider: historicalProvider,
    RightProvider: rightProvider,
  };
}

function buildLeafRule(condition: Condition): LoyaltyRule {
  if (condition.kind === "historical") {
    if (condition.historicalRule && condition.historicalRule.Kind === "HistoricalRule" && !condition.historicalEdited) {
      return JSON.parse(JSON.stringify(condition.historicalRule)) as LoyaltyRule;
    }
    if (condition.historicalConfig) {
      return buildHistoricalRuleFromConfig(condition.historicalConfig);
    }
    const defaults = defaultHistoricalConfig();
    const fallback: LoyaltyHistoricalRule = {
      ...buildHistoricalRuleFromConfig(defaults),
    };
    return fallback;
  }
  return condition.kind === "taxonomic" ? buildTaxonomicRule(condition) : buildSimpleRule(condition);
}

/**
 * Serialize editor state to a wire `LoyaltyRule` (or `null` for no-op state).
 * Single-condition state returns the bare leaf; multi-condition state wraps
 * children under `AndRule` / `OrRule`.
 */
export function buildRule(conditions: Condition[], composite: CompositeKind): LoyaltyRule | null {
  if (conditions.length === 0) return null;
  if (conditions.length === 1) {
    const only = conditions[0];
    if (!only) return null;
    return buildLeafRule(only);
  }
  const children = conditions.map(buildLeafRule);
  if (composite === "AndRule") {
    const andRule: AndRule = { Kind: "AndRule", Children: children };
    return andRule;
  }
  const orRule: OrRule = { Kind: "OrRule", Children: children };
  return orRule;
}

// ─────────────────────────────────────────────────────────────────────────────
// Condition validation
// ─────────────────────────────────────────────────────────────────────────────

function isSideValid(side: ConditionSide, evaluatorClass: "numeric" | "string" | "boolean" | undefined): boolean {
  switch (side.providerType) {
    case "PathValueProvider":
      return side.propertyPath.trim() !== "";
    case "PointBalanceProvider":
      return side.pointAccountTypeId.trim() !== "";
    case "ConstantValueProvider":
      if (evaluatorClass === "boolean") return typeof side.constantValue === "boolean";
      return side.constantValue !== "" && side.constantValue !== null && side.constantValue !== undefined;
    case "AggregateValueProvider":
      return true;
    case "ProductListProvider":
      return side.productSelection !== null;
  }
}

export function isConditionValid(condition: Condition): boolean {
  if (condition.kind === "historical") {
    if (condition.historicalConfig) {
      return (
        condition.historicalConfig.valuePath.trim() !== "" &&
        condition.historicalConfig.occurrencePath.trim() !== "" &&
        Number.isFinite(condition.historicalConfig.windowDays) &&
        condition.historicalConfig.windowDays > 0 &&
        (condition.historicalConfig.thresholdProviderType === "ConstantValueProvider"
          ? Number.isFinite(condition.historicalConfig.thresholdValue)
          : condition.historicalConfig.thresholdProviderType === "PathValueProvider"
            ? condition.historicalConfig.thresholdPath.trim() !== ""
            : condition.historicalConfig.thresholdPointAccountTypeId.trim() !== "")
      );
    }
    return Boolean(condition.historicalRule && condition.historicalRule.Kind === "HistoricalRule");
  }
  if (condition.kind === "taxonomic") {
    const selection = condition.productSelection;
    const hasSelection = selection !== null && (selection.categories.length > 0 || selection.entities.length > 0);
    return hasSelection && condition.itemsPropertyPath.trim() !== "" && condition.keySymbolPath.trim() !== "";
  }
  const evMeta = EVALUATOR_BY_VALUE.get(condition.evaluator);
  return isSideValid(condition.left, evMeta?.class) && isSideValid(condition.right, evMeta?.class);
}

export function areConditionsValid(conditions: Condition[]): boolean {
  return conditions.length > 0 && conditions.every(isConditionValid);
}

// ─────────────────────────────────────────────────────────────────────────────
// Provider type narrowing helpers (exported for tests + UI)
// ─────────────────────────────────────────────────────────────────────────────

export const STANDARD_PROVIDER_TYPES: ReadonlyArray<{ value: UiProviderType; label: string }> = [
  { value: "PathValueProvider", label: "Event property" },
  { value: "ConstantValueProvider", label: "Fixed value" },
  { value: "PointBalanceProvider", label: "Point balance" },
  { value: "AggregateValueProvider", label: "Aggregate (Sum/Count)" },
];
