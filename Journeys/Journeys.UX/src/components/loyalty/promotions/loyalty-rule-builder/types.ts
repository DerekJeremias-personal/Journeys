/**
 * Internal `Condition` model consumed by `<LoyaltyRuleBuilder>` UI.
 *
 * The component edits a flat array of conditions composed with a single AND/OR
 * (matching legacy admin-web parity) and serializes to / parses from the
 * canonical `Rule` (PascalCase JsonElement) wire shape from
 * `@/lib/campaign-types/loyalty-rules`.
 *
 * Two condition kinds:
 *   - `standard`   — `Provider {Evaluator} Provider` (SimpleRule)
 *   - `taxonomic`  — `event.items[].sku ∈ products` (TaxonomicRule)
 *
 * Composition with multiple conditions wraps them in `AndRule` / `OrRule`.
 * Single-condition output is the bare leaf rule.
 */

import type { ProductSelection } from "@/components/loyalty/product-selector";
import type { LoyaltyRule } from "@/lib/campaign-types";

// ─────────────────────────────────────────────────────────────────────────────
// Provider type (UI-side, not the wire `ProviderKind`)
// ─────────────────────────────────────────────────────────────────────────────

/**
 * UI-facing provider type. Adds two synthetic types over the wire `ProviderKind`:
 *   - `PointBalanceProvider` — a `PathValueProvider` carrying a `PointAccountTypeId`
 *     (server-side bug per `PointBalanceProvider.cs:16`; we expose it as a first-
 *     class option to match how loyalty admins think about it).
 *   - `ProductListProvider` — server-side concept that hangs off `ProductSelection`.
 */
export type UiProviderType =
  | "PathValueProvider"
  | "ConstantValueProvider"
  | "AggregateValueProvider"
  | "PointBalanceProvider"
  | "ProductListProvider";

// ─────────────────────────────────────────────────────────────────────────────
// Evaluator metadata (UI-side; maps to wire `Evaluator` shapes)
// ─────────────────────────────────────────────────────────────────────────────

export type EvaluatorClass = "numeric" | "string" | "boolean";

export interface EvaluatorOption {
  /** Stable UI key, also used as the `Comparison` literal we emit on the wire. */
  value: string;
  label: string;
  class: EvaluatorClass;
  /** Numeric `Comparison` code per the C# enums. */
  numericCode: number;
  /** Wire `$type` literal of the matching `Evaluator` schema. */
  evaluatorType: "NumericEvaluation" | "StringEvaluation" | "BoolEvaluation";
}

export const EVALUATORS: readonly EvaluatorOption[] = [
  { value: "NumericEquals", label: "equals (=)", class: "numeric", numericCode: 1, evaluatorType: "NumericEvaluation" },
  {
    value: "NumericGreaterThan",
    label: "greater than (>)",
    class: "numeric",
    numericCode: 2,
    evaluatorType: "NumericEvaluation",
  },
  {
    value: "NumericGreaterThanOrEqual",
    label: "greater or equal (\u2265)",
    class: "numeric",
    numericCode: 3,
    evaluatorType: "NumericEvaluation",
  },
  {
    value: "NumericLessThan",
    label: "less than (<)",
    class: "numeric",
    numericCode: 4,
    evaluatorType: "NumericEvaluation",
  },
  {
    value: "NumericLessThanOrEqual",
    label: "less or equal (\u2264)",
    class: "numeric",
    numericCode: 5,
    evaluatorType: "NumericEvaluation",
  },
  { value: "StringEquals", label: "equals (text)", class: "string", numericCode: 1, evaluatorType: "StringEvaluation" },
  {
    value: "StringContains",
    label: "contains",
    class: "string",
    numericCode: 2,
    evaluatorType: "StringEvaluation",
  },
  {
    value: "StringStartsWith",
    label: "starts with",
    class: "string",
    numericCode: 3,
    evaluatorType: "StringEvaluation",
  },
  {
    value: "StringEndsWith",
    label: "ends with",
    class: "string",
    numericCode: 4,
    evaluatorType: "StringEvaluation",
  },
  {
    value: "StringNotEquals",
    label: "not equals (text)",
    class: "string",
    numericCode: 7,
    evaluatorType: "StringEvaluation",
  },
  { value: "BooleanEquals", label: "is", class: "boolean", numericCode: 1, evaluatorType: "BoolEvaluation" },
] as const;

export const EVALUATOR_BY_VALUE: ReadonlyMap<string, EvaluatorOption> = new Map(EVALUATORS.map((e) => [e.value, e]));

// ─────────────────────────────────────────────────────────────────────────────
// Condition (UI state, one per row)
// ─────────────────────────────────────────────────────────────────────────────

export type ConditionKind = "standard" | "taxonomic" | "historical";

export interface ConditionSide {
  providerType: UiProviderType;
  /** PathValueProvider: dotted path (e.g. `payload.spendTotal`) */
  propertyPath: string;
  /** ConstantValueProvider: literal value */
  constantValue: string | number | boolean;
  /** PointBalanceProvider: PAT id */
  pointAccountTypeId: string;
  /** ProductListProvider: catalog selection */
  productSelection: ProductSelection | null;
}

export interface Condition {
  /** Stable UI id, NOT serialized. */
  id: string;
  kind: ConditionKind;
  /** Standard condition fields. */
  left: ConditionSide;
  right: ConditionSide;
  evaluator: string;
  /** Taxonomic condition fields. */
  productSelection: ProductSelection | null;
  taxonomyId: string;
  taxonomyType: string;
  keySymbolPath: string;
  itemsPropertyPath: string;
  historicalRule: LoyaltyRule | null;
  historicalConfig: HistoricalConditionConfig | null;
  historicalEdited: boolean;
}

export type HistoricalAggregateType = "Sum" | "Count" | "Average" | "Min" | "Max";
export type HistoricalTemporalComparison = "OnOrAfter" | "After" | "OnOrBefore" | "Before";
export type HistoricalThresholdProviderType = "ConstantValueProvider" | "PathValueProvider" | "PointBalanceProvider";

export interface HistoricalConditionConfig {
  aggregateType: HistoricalAggregateType;
  valuePath: string;
  occurrencePath: string;
  windowDays: number;
  thresholdProviderType: HistoricalThresholdProviderType;
  thresholdValue: number;
  thresholdPath: string;
  thresholdPointAccountTypeId: string;
  temporalComparison: HistoricalTemporalComparison;
}

export function defaultHistoricalConfig(): HistoricalConditionConfig {
  return {
    aggregateType: "Count",
    valuePath: "event.price",
    occurrencePath: "event.transactionDate",
    windowDays: 30,
    thresholdProviderType: "ConstantValueProvider",
    thresholdValue: 1,
    thresholdPath: "event.price",
    thresholdPointAccountTypeId: "",
    temporalComparison: "OnOrAfter",
  };
}

export type CompositeKind = "AndRule" | "OrRule";

export interface RuleEditorState {
  conditions: Condition[];
  composite: CompositeKind;
}

// ─────────────────────────────────────────────────────────────────────────────
// Factories
// ─────────────────────────────────────────────────────────────────────────────

export function emptySide(): ConditionSide {
  return {
    providerType: "ConstantValueProvider",
    propertyPath: "",
    constantValue: true,
    pointAccountTypeId: "",
    productSelection: null,
  };
}

export function emptyCondition(kind: ConditionKind = "standard"): Condition {
  return {
    id: typeof crypto !== "undefined" && "randomUUID" in crypto ? crypto.randomUUID() : Math.random().toString(36),
    kind,
    left: emptySide(),
    right: emptySide(),
    evaluator: "BooleanEquals",
    productSelection: null,
    taxonomyId: "",
    taxonomyType: "TreeRoot",
    keySymbolPath: "sku",
    itemsPropertyPath: "event.items",
    historicalRule: null,
    historicalConfig: kind === "historical" ? defaultHistoricalConfig() : null,
    historicalEdited: false,
  };
}

export function emptyState(): RuleEditorState {
  return { conditions: [emptyCondition()], composite: "AndRule" };
}
