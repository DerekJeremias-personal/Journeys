/**
 * Parse a wire `Rule` (PascalCase JsonElement) into the editor's
 * `Condition[]` + composite state.
 *
 * The editor models a flat list of conditions composed with a single
 * `AndRule` / `OrRule`. Inputs that don't fit (e.g. nested composites,
 * `NotRule`, `HistoricalRule`, `TemporalConstraintRule`,
 * `DatePropertyRule`) are best-effort flattened or surfaced as a single
 * empty condition so the modal stays usable while warning the author that
 * a more complex rule was authored elsewhere.
 */

import type { LoyaltyRule, Provider, SimpleRule, TaxonomicRule } from "@/lib/campaign-types";

import type { ProductSelection, SelectionMode } from "@/components/loyalty/product-selector";
import { categoryNodeId } from "@/components/loyalty/taxonomy-picker/state";

import {
  defaultHistoricalConfig,
  emptyCondition,
  type HistoricalThresholdProviderType,
  emptySide,
  EVALUATORS,
  EVALUATOR_BY_VALUE,
  type CompositeKind,
  type Condition,
  type ConditionSide,
  type RuleEditorState,
  type UiProviderType,
} from "./types";

// ─────────────────────────────────────────────────────────────────────────────
// Provider parsing
// ─────────────────────────────────────────────────────────────────────────────

function inferProviderType(provider: Provider): UiProviderType {
  switch (provider.Kind) {
    case "PathValueProvider":
      // PointBalance ships as PathValueProvider with `$type: "PointBalanceProvider"`
      // OR a `PointAccountTypeId` field; treat either marker as point-balance.
      if (provider.$type === "PointBalanceProvider" || provider.PointAccountTypeId) return "PointBalanceProvider";
      return "PathValueProvider";
    case "ConstantValueProvider":
      return "ConstantValueProvider";
    case "AggregateValueProvider":
      return "AggregateValueProvider";
    default:
      return "PathValueProvider";
  }
}

function parseSide(provider: Provider | null | undefined): ConditionSide {
  if (!provider) return emptySide();
  const providerType = inferProviderType(provider);
  const side = emptySide();
  side.providerType = providerType;
  if (provider.Kind === "PathValueProvider") {
    if (provider.PropertyPath) side.propertyPath = provider.PropertyPath;
    if (provider.PointAccountTypeId) side.pointAccountTypeId = provider.PointAccountTypeId;
    return side;
  }
  if (provider.Kind === "ConstantValueProvider") {
    const v = provider.Value;
    if (typeof v === "string" || typeof v === "number" || typeof v === "boolean") {
      side.constantValue = v;
    }
    return side;
  }
  return side;
}

// ─────────────────────────────────────────────────────────────────────────────
// Evaluator parsing
// ─────────────────────────────────────────────────────────────────────────────

function parseEvaluator(rule: SimpleRule): string {
  const ev = rule.Evaluator;
  if (!ev) return "BooleanEquals";
  // String comparison values (e.g. "Equal" from BoolEvaluation) — exact match first.
  if (typeof ev.Comparison === "string") {
    const direct = EVALUATOR_BY_VALUE.get(ev.Comparison);
    if (direct) return direct.value;
    if (ev.Comparison === "Equal") return "BooleanEquals";
  }
  const numeric = typeof ev.Comparison === "number" ? ev.Comparison : Number(ev.Comparison ?? NaN);
  const evType = ev.$type;
  if (evType === "NumericEvaluation") {
    const found = EVALUATORS.find((e) => e.class === "numeric" && e.numericCode === numeric);
    if (found) return found.value;
    return "NumericEquals";
  }
  if (evType === "StringEvaluation") {
    const found = EVALUATORS.find((e) => e.class === "string" && e.numericCode === numeric);
    if (found) return found.value;
    return "StringEquals";
  }
  if (evType === "BoolEvaluation") return "BooleanEquals";
  // Fallback: inspect numeric code only.
  const fallback = EVALUATORS.find((e) => e.numericCode === numeric);
  return fallback?.value ?? "NumericEquals";
}

// ─────────────────────────────────────────────────────────────────────────────
// Leaf rule → Condition
// ─────────────────────────────────────────────────────────────────────────────

function parseTaxonomicRule(rule: TaxonomicRule): Condition {
  const condition = emptyCondition("taxonomic");
  const includedTreeNodes = rule.IncludedTreeNodes ?? [];
  const includedIds = rule.IncludedIds ?? [];
  const excludedTreeNodes = rule.ExcludedTreeNodes ?? [];
  const excludedIds = rule.ExcludedIds ?? [];
  const hasIncludes = includedTreeNodes.length > 0 || includedIds.length > 0;
  const mode: SelectionMode = hasIncludes ? "include" : "exclude";
  const categoryPaths = mode === "include" ? includedTreeNodes : excludedTreeNodes;
  const entityIds = mode === "include" ? includedIds : excludedIds;
  // Use the canonical id format (`category-${catalogId}-${path}`) that
  // `buildCategoryTree` emits — otherwise the picker can't match the
  // saved selection back to the tree it loads from the API.
  const catalogId = rule.TaxonomyId || "";
  const selection: ProductSelection = {
    mode,
    categories: categoryPaths.map((path) => ({
      id: categoryNodeId(catalogId || "category", path),
      name: path.split(".").pop() ?? path,
      categoryPath: path,
      catalogId,
    })),
    entities: entityIds.map((id) => ({ id, name: id })),
  };
  condition.productSelection = selection;
  condition.taxonomyId = rule.TaxonomyId;
  condition.taxonomyType = rule.TaxonomyType || "TreeRoot";
  condition.keySymbolPath = rule.KeySymbolPath || "sku";
  const left = rule.LeftProvider;
  if (left && left.Kind === "PathValueProvider" && left.PropertyPath) {
    condition.itemsPropertyPath = left.PropertyPath;
  }
  return condition;
}

function parseLeafRule(rule: LoyaltyRule): Condition {
  if (rule.Kind === "HistoricalRule") {
    const condition = emptyCondition("historical");
    // Preserve historical payload as-is so round-trips remain lossless.
    condition.historicalRule = JSON.parse(JSON.stringify(rule)) as LoyaltyRule;
    condition.historicalConfig = parseHistoricalConfig(rule);
    return condition;
  }

  if (rule.Kind === "TaxonomicRule") return parseTaxonomicRule(rule);
  // SimpleRule + its Date/Numeric/StringPropertyRule specializations all share
  // the LeftProvider/RightProvider/Evaluator triple.
  if (
    rule.Kind === "SimpleRule" ||
    rule.Kind === "DatePropertyRule" ||
    rule.Kind === "NumericPropertyRule" ||
    rule.Kind === "StringPropertyRule"
  ) {
    const condition = emptyCondition("standard");
    condition.left = parseSide(rule.LeftProvider ?? null);
    condition.right = parseSide(rule.RightProvider ?? null);
    condition.evaluator = parseEvaluator(rule as SimpleRule);
    return condition;
  }
  // Unsupported leaf shapes (TemporalConstraintRule) → empty
  // condition; the modal preserves the original rule intact via `onChange`
  // not firing for non-edits (controlled by the caller).
  return emptyCondition("standard");
}

function parseHistoricalConfig(rule: Extract<LoyaltyRule, { Kind: "HistoricalRule" }>) {
  const defaults = defaultHistoricalConfig();

  const provider =
    (rule.HistoricalValueProvider as Record<string, unknown> | undefined) ??
    (rule.AggregationValueProvider as Record<string, unknown> | undefined);
  const instance = (provider?.["InstanceValueProvider"] ?? null) as Record<string, unknown> | null;
  const temporal = (provider?.["TemporalConstraint"] ?? null) as Record<string, unknown> | null;

  const directValuePath =
    instance?.["Kind"] === "PathValueProvider" ? (instance["PropertyPath"] as string | undefined) : undefined;
  const rowPropertyPath =
    instance?.["Kind"] === "AggregateValueProvider"
      ? (((instance["RowPropertyProvider"] as Record<string, unknown> | undefined)?.["PropertyPath"] as
          | string
          | undefined) ?? undefined)
      : undefined;
  const valuePath = directValuePath ?? rowPropertyPath ?? defaults.valuePath;

  const occurrencePath =
    ((temporal?.["TimeOfOccurrenceProvider"] as Record<string, unknown> | undefined)?.["PropertyPath"] as
      | string
      | undefined) ?? defaults.occurrencePath;

  const thresholdRaw = (rule.RightProvider as Record<string, unknown> | undefined)?.["Value"];
  const thresholdNum =
    typeof thresholdRaw === "number" ? thresholdRaw : typeof thresholdRaw === "string" ? Number(thresholdRaw) : NaN;
  const right = (rule.RightProvider as Record<string, unknown> | undefined) ?? {};
  const thresholdValue = Number.isFinite(thresholdNum) ? thresholdNum : defaults.thresholdValue;
  const thresholdProviderType: HistoricalThresholdProviderType =
    right["Kind"] === "PathValueProvider" && right["$type"] === "PointBalanceProvider"
      ? "PointBalanceProvider"
      : right["Kind"] === "PathValueProvider"
        ? "PathValueProvider"
        : "ConstantValueProvider";
  const thresholdPath =
    thresholdProviderType === "PathValueProvider"
      ? ((right["PropertyPath"] as string | undefined) ?? defaults.thresholdPath)
      : defaults.thresholdPath;
  const thresholdPointAccountTypeId =
    thresholdProviderType === "PointBalanceProvider"
      ? ((right["PointAccountTypeId"] as string | undefined) ?? defaults.thresholdPointAccountTypeId)
      : defaults.thresholdPointAccountTypeId;

  const aggregateType = parseAggregateType(rule.AggregateType ?? provider?.["AggregateType"], defaults.aggregateType);

  const temporalEval = (temporal?.["TemporalEvaluation"] as Record<string, unknown> | undefined) ?? {};
  const temporalComparison = parseTemporalComparison(
    temporalEval["ComparisonType"] ?? temporalEval["Comparison"],
    defaults.temporalComparison
  );

  const windowDays = parseWindowDays(temporal?.["Comparison"], defaults.windowDays);

  return {
    aggregateType,
    valuePath,
    occurrencePath,
    windowDays,
    thresholdProviderType,
    thresholdValue,
    thresholdPath,
    thresholdPointAccountTypeId,
    temporalComparison,
  };
}

function parseWindowDays(raw: unknown, fallback: number): number {
  if (typeof raw !== "string") return fallback;
  const dot = raw.match(/^(\d+)\.00:00:00$/);
  if (dot) return Number(dot[1]);
  const spaced = raw.match(/^(\d+)\s+day(s)?$/i);
  if (spaced) return Number(spaced[1]);
  return fallback;
}

function parseAggregateType(raw: unknown, fallback: "Sum" | "Count" | "Average" | "Min" | "Max") {
  if (typeof raw === "number") {
    if (raw === 1) return "Sum";
    if (raw === 2) return "Average";
    if (raw === 3) return "Min";
    if (raw === 4) return "Max";
    if (raw === 5) return "Count";
    return fallback;
  }
  const str = String(raw ?? "").trim();
  if (str === "Sum" || str === "Count" || str === "Average" || str === "Min" || str === "Max") return str;
  if (str === "1") return "Sum";
  if (str === "2") return "Average";
  if (str === "3") return "Min";
  if (str === "4") return "Max";
  if (str === "5") return "Count";
  return fallback;
}

function parseTemporalComparison(raw: unknown, fallback: "OnOrAfter" | "After" | "OnOrBefore" | "Before") {
  if (typeof raw === "number") {
    if (raw === 1) return "OnOrBefore";
    if (raw === 2) return "Before";
    if (raw === 3) return "OnOrAfter";
    if (raw === 4) return "After";
    return fallback;
  }
  const str = String(raw ?? "").trim();
  if (str === "OnOrAfter" || str === "After" || str === "OnOrBefore" || str === "Before") return str;
  if (str === "1") return "OnOrBefore";
  if (str === "2") return "Before";
  if (str === "3") return "OnOrAfter";
  if (str === "4") return "After";
  return fallback;
}

// ─────────────────────────────────────────────────────────────────────────────
// Public entry
// ─────────────────────────────────────────────────────────────────────────────

/**
 * Parse a wire rule into editor state. `null`/`undefined` → fresh single
 * empty condition.
 *
 * Returns `supported = false` when the rule contains unsupported shapes
 * (`NotRule`, nested composites, `TemporalConstraintRule`).
 * The UI uses this to render a "complex rule preserved as-is" notice and
 * disable destructive editing.
 */
export function parseRule(rule: LoyaltyRule | null | undefined): RuleEditorState & { supported: boolean } {
  if (!rule) {
    return { conditions: [emptyCondition()], composite: "AndRule", supported: true };
  }
  if (rule.Kind === "AndRule" || rule.Kind === "OrRule") {
    const composite: CompositeKind = rule.Kind;
    const children = rule.Children ?? [];
    if (children.length === 0) {
      return { conditions: [emptyCondition()], composite, supported: true };
    }
    const supported = children.every(isFlatLeaf);
    if (!supported) {
      return { conditions: [emptyCondition()], composite, supported: false };
    }
    return { conditions: children.map(parseLeafRule), composite, supported: true };
  }
  if (rule.Kind === "NotRule" || rule.Kind === "TemporalConstraintRule") {
    return { conditions: [emptyCondition()], composite: "AndRule", supported: false };
  }
  return { conditions: [parseLeafRule(rule)], composite: "AndRule", supported: true };
}

function isFlatLeaf(rule: LoyaltyRule): boolean {
  return (
    rule.Kind === "SimpleRule" ||
    rule.Kind === "DatePropertyRule" ||
    rule.Kind === "NumericPropertyRule" ||
    rule.Kind === "StringPropertyRule" ||
    rule.Kind === "TaxonomicRule" ||
    rule.Kind === "HistoricalRule"
  );
}
