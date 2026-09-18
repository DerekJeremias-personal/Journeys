import { describe, expect, it } from "vitest";

import { LoyaltyRuleSchema, type LoyaltyRule } from "@/lib/campaign-types";

import { areConditionsValid, buildRule, isConditionValid } from "../build-rule";
import { parseRule } from "../parse-rule";
import { emptyCondition, type Condition } from "../types";
import { buildCategoryTree, findCategoryNode } from "@/components/loyalty/taxonomy-picker/state";

// ─────────────────────────────────────────────────────────────────────────────
// parseRule
// ─────────────────────────────────────────────────────────────────────────────

describe("parseRule", () => {
  it("returns a single empty condition when input is null", () => {
    const state = parseRule(null);
    expect(state.conditions).toHaveLength(1);
    expect(state.composite).toBe("AndRule");
    expect(state.supported).toBe(true);
  });

  it("parses a SimpleRule into a single standard condition", () => {
    const rule: LoyaltyRule = {
      Kind: "SimpleRule",
      LeftProvider: { Kind: "PathValueProvider", $type: "PathValueProvider", PropertyPath: "payload.spendTotal" },
      RightProvider: { Kind: "ConstantValueProvider", $type: "ConstantValueProvider", Value: 100 },
      Evaluator: { $type: "NumericEvaluation", Comparison: 2 }, // GreaterThan
    };
    const state = parseRule(rule);
    expect(state.conditions).toHaveLength(1);
    expect(state.conditions[0]?.kind).toBe("standard");
    expect(state.conditions[0]?.left.providerType).toBe("PathValueProvider");
    expect(state.conditions[0]?.left.propertyPath).toBe("payload.spendTotal");
    expect(state.conditions[0]?.right.providerType).toBe("ConstantValueProvider");
    expect(state.conditions[0]?.right.constantValue).toBe(100);
    expect(state.conditions[0]?.evaluator).toBe("NumericGreaterThan");
  });

  it("parses an AndRule into multiple standard conditions", () => {
    const rule: LoyaltyRule = {
      Kind: "AndRule",
      Children: [
        {
          Kind: "SimpleRule",
          LeftProvider: { Kind: "PathValueProvider", $type: "PathValueProvider", PropertyPath: "payload.a" },
          RightProvider: { Kind: "ConstantValueProvider", $type: "ConstantValueProvider", Value: 1 },
          Evaluator: { $type: "NumericEvaluation", Comparison: 1 },
        },
        {
          Kind: "SimpleRule",
          LeftProvider: { Kind: "PathValueProvider", $type: "PathValueProvider", PropertyPath: "payload.b" },
          RightProvider: { Kind: "ConstantValueProvider", $type: "ConstantValueProvider", Value: "x" },
          Evaluator: { $type: "StringEvaluation", Comparison: 1 },
        },
      ],
    };
    const state = parseRule(rule);
    expect(state.composite).toBe("AndRule");
    expect(state.conditions).toHaveLength(2);
    expect(state.supported).toBe(true);
    expect(state.conditions[0]?.evaluator).toBe("NumericEquals");
    expect(state.conditions[1]?.evaluator).toBe("StringEquals");
  });

  it("parses PointBalanceProvider via $type marker", () => {
    const rule: LoyaltyRule = {
      Kind: "SimpleRule",
      LeftProvider: { Kind: "PathValueProvider", $type: "PointBalanceProvider", PointAccountTypeId: "pat-1" },
      RightProvider: { Kind: "ConstantValueProvider", $type: "ConstantValueProvider", Value: 0 },
      Evaluator: { $type: "NumericEvaluation", Comparison: 2 },
    };
    const state = parseRule(rule);
    expect(state.conditions[0]?.left.providerType).toBe("PointBalanceProvider");
    expect(state.conditions[0]?.left.pointAccountTypeId).toBe("pat-1");
  });

  it("parses TaxonomicRule into a taxonomic condition with include selection", () => {
    const rule: LoyaltyRule = {
      Kind: "TaxonomicRule",
      LeftProvider: { Kind: "PathValueProvider", $type: "PathValueProvider", PropertyPath: "event.items" },
      RightProvider: { Kind: "ConstantValueProvider", $type: "ConstantValueProvider", Value: "alwaystrue" },
      Evaluator: { $type: "StringEvaluation", Comparison: 1 },
      IncludedTreeNodes: ["root.dairy"],
      IncludedIds: ["sku-100"],
      ExcludedTreeNodes: [],
      ExcludedIds: [],
      TaxonomyType: "TreeRoot",
      TaxonomyId: "catalog-1",
      KeySymbolPath: "sku",
    };
    const state = parseRule(rule);
    expect(state.conditions[0]?.kind).toBe("taxonomic");
    expect(state.conditions[0]?.productSelection?.mode).toBe("include");
    expect(state.conditions[0]?.productSelection?.categories).toHaveLength(1);
    expect(state.conditions[0]?.productSelection?.entities).toHaveLength(1);
    expect(state.conditions[0]?.taxonomyId).toBe("catalog-1");
    expect(state.conditions[0]?.itemsPropertyPath).toBe("event.items");
  });

  it("flags supported=false for NotRule wrappers", () => {
    const rule: LoyaltyRule = {
      Kind: "NotRule",
      Children: [
        {
          Kind: "SimpleRule",
          LeftProvider: { Kind: "PathValueProvider", $type: "PathValueProvider", PropertyPath: "x" },
          RightProvider: { Kind: "ConstantValueProvider", $type: "ConstantValueProvider", Value: 1 },
          Evaluator: { $type: "NumericEvaluation", Comparison: 1 },
        },
      ],
    };
    const state = parseRule(rule);
    expect(state.supported).toBe(false);
  });

  it("parses HistoricalRule as supported and preserves payload", () => {
    const rule: LoyaltyRule = {
      Kind: "HistoricalRule",
      Id: "historical-1",
      AggregateType: "Sum",
      HistoricalValueProvider: {
        Kind: "SimpleCalculationProvider",
        $type: "SimpleCalculationProvider",
        AggregateType: "Sum",
        InstanceValueProvider: {
          Kind: "PathValueProvider",
          $type: "PathValueProvider",
          PropertyPath: "event.items[].price",
        },
        TemporalConstraint: {
          Kind: "TemporalConstraintRule",
          TimeOfOccurrenceProvider: {
            Kind: "PathValueProvider",
            $type: "PathValueProvider",
            PropertyPath: "event.transactionDate",
          },
          TemporalEvaluation: {
            $type: "TemporalEvaluation",
            ComparisonType: "OnOrAfter",
          },
          Comparison: "30.00:00:00",
        },
      },
      RightProvider: {
        Kind: "ConstantValueProvider",
        $type: "ConstantValueProvider",
        Value: 500,
      },
    };
    const state = parseRule(rule);
    expect(state.supported).toBe(true);
    expect(state.conditions).toHaveLength(1);
    expect(state.conditions[0]?.kind).toBe("historical");
    expect(state.conditions[0]?.historicalRule).toEqual(rule);
    expect(state.conditions[0]?.historicalConfig?.aggregateType).toBe("Sum");
    expect(state.conditions[0]?.historicalConfig?.windowDays).toBe(30);
    expect(state.conditions[0]?.historicalConfig?.thresholdProviderType).toBe("ConstantValueProvider");
    expect(state.conditions[0]?.historicalConfig?.thresholdValue).toBe(500);
    expect(state.conditions[0]?.historicalEdited).toBe(false);
  });

  it("parses ELP-style numeric enum values for aggregate and temporal comparison", () => {
    const rule: LoyaltyRule = {
      Kind: "HistoricalRule",
      Id: "historical-enum-1",
      AggregateType: 1,
      HistoricalValueProvider: {
        Kind: "SimpleCalculationProvider",
        $type: "SimpleCalculationProvider",
        TemporalConstraint: {
          Kind: "TemporalConstraintRule",
          TimeOfOccurrenceProvider: {
            Kind: "PathValueProvider",
            $type: "PathValueProvider",
            PropertyPath: "event.timestamp",
          },
          TemporalEvaluation: {
            Comparison: 4,
          },
          Comparison: "30.00:00:00",
        },
      },
      RightProvider: {
        Kind: "ConstantValueProvider",
        $type: "ConstantValueProvider",
        Value: 500,
      },
    };
    const state = parseRule(rule);
    expect(state.conditions[0]?.historicalConfig?.aggregateType).toBe("Sum");
    expect(state.conditions[0]?.historicalConfig?.temporalComparison).toBe("After");
    expect(state.conditions[0]?.historicalConfig?.windowDays).toBe(30);
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// buildRule
// ─────────────────────────────────────────────────────────────────────────────

describe("buildRule", () => {
  it("returns null for empty conditions", () => {
    expect(buildRule([], "AndRule")).toBeNull();
  });

  it("builds a SimpleRule for a single standard condition", () => {
    const condition: Condition = {
      ...emptyCondition("standard"),
      left: {
        providerType: "PathValueProvider",
        propertyPath: "payload.x",
        constantValue: "",
        pointAccountTypeId: "",
        productSelection: null,
      },
      right: {
        providerType: "ConstantValueProvider",
        propertyPath: "",
        constantValue: 5,
        pointAccountTypeId: "",
        productSelection: null,
      },
      evaluator: "NumericEquals",
    };
    const rule = buildRule([condition], "AndRule");
    expect(rule?.Kind).toBe("SimpleRule");
    if (rule?.Kind !== "SimpleRule") throw new Error("expected SimpleRule");
    expect(rule.LeftProvider?.Kind).toBe("PathValueProvider");
    expect(rule.RightProvider?.Kind).toBe("ConstantValueProvider");
    expect(rule.Evaluator?.$type).toBe("NumericEvaluation");
  });

  it("emits PointBalanceProvider with $type marker for PointBalance side", () => {
    const condition: Condition = {
      ...emptyCondition("standard"),
      left: { ...emptyCondition().left, providerType: "PointBalanceProvider", pointAccountTypeId: "pat-1" },
      right: { ...emptyCondition().right, providerType: "ConstantValueProvider", constantValue: 100 },
      evaluator: "NumericGreaterThanOrEqual",
    };
    const rule = buildRule([condition], "AndRule");
    if (rule?.Kind !== "SimpleRule") throw new Error("expected SimpleRule");
    const left = rule.LeftProvider;
    if (left?.Kind !== "PathValueProvider") throw new Error("expected PathValueProvider variant");
    expect(left.$type).toBe("PointBalanceProvider");
    expect(left.PointAccountTypeId).toBe("pat-1");
  });

  it("wraps multiple conditions in AndRule / OrRule", () => {
    const a = emptyCondition("standard");
    a.left.providerType = "PathValueProvider";
    a.left.propertyPath = "payload.a";
    a.right.providerType = "ConstantValueProvider";
    a.right.constantValue = 1;
    a.evaluator = "NumericEquals";
    const b: Condition = JSON.parse(JSON.stringify(a)) as Condition;
    b.id = "id-b";
    b.right.constantValue = 2;
    const andRule = buildRule([a, b], "AndRule");
    expect(andRule?.Kind).toBe("AndRule");
    if (andRule?.Kind !== "AndRule") throw new Error("expected AndRule");
    expect(andRule.Children).toHaveLength(2);
    const orRule = buildRule([a, b], "OrRule");
    expect(orRule?.Kind).toBe("OrRule");
  });

  it("emits a TaxonomicRule with include lists when mode=include", () => {
    const condition: Condition = {
      ...emptyCondition("taxonomic"),
      productSelection: {
        mode: "include",
        categories: [{ id: "cat-1", name: "Dairy", categoryPath: "root.dairy", catalogId: "cat-catalog" }],
        entities: [{ id: "sku-1", name: "Milk" }],
      },
      taxonomyId: "cat-catalog",
      taxonomyType: "TreeRoot",
      keySymbolPath: "sku",
      itemsPropertyPath: "event.items",
    };
    const rule = buildRule([condition], "AndRule");
    if (rule?.Kind !== "TaxonomicRule") throw new Error("expected TaxonomicRule");
    expect(rule.IncludedTreeNodes).toEqual(["root.dairy"]);
    expect(rule.IncludedIds).toEqual(["sku-1"]);
    expect(rule.ExcludedTreeNodes).toEqual([]);
    expect(rule.ExcludedIds).toEqual([]);
  });

  it("validated output passes the canonical RuleSchema", () => {
    const condition: Condition = {
      ...emptyCondition("standard"),
      left: { ...emptyCondition().left, providerType: "PathValueProvider", propertyPath: "payload.x" },
      right: { ...emptyCondition().right, providerType: "ConstantValueProvider", constantValue: 5 },
      evaluator: "NumericGreaterThan",
    };
    const rule = buildRule([condition], "AndRule");
    expect(rule).not.toBeNull();
    const parsed = LoyaltyRuleSchema.safeParse(rule);
    if (!parsed.success) {
      throw new Error(
        `RuleSchema parse failed: ${JSON.stringify(parsed.error.issues, null, 2)}\nrule: ${JSON.stringify(rule, null, 2)}`
      );
    }
    expect(parsed.success).toBe(true);
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// Round-trip parse → build
// ─────────────────────────────────────────────────────────────────────────────

describe("parseRule + buildRule round-trip", () => {
  it("preserves a SimpleRule", () => {
    const original: LoyaltyRule = {
      Kind: "SimpleRule",
      LeftProvider: { Kind: "PathValueProvider", $type: "PathValueProvider", PropertyPath: "payload.spendTotal" },
      RightProvider: { Kind: "ConstantValueProvider", $type: "ConstantValueProvider", Value: 50 },
      Evaluator: { $type: "NumericEvaluation", Comparison: 2 },
    };
    const state = parseRule(original);
    const rebuilt = buildRule(state.conditions, state.composite);
    expect(rebuilt?.Kind).toBe("SimpleRule");
    if (rebuilt?.Kind !== "SimpleRule") throw new Error("expected SimpleRule");
    const rl = rebuilt.LeftProvider;
    if (rl?.Kind !== "PathValueProvider") throw new Error("left should be PathValueProvider");
    expect(rl.PropertyPath).toBe("payload.spendTotal");
    expect(rebuilt.Evaluator?.$type).toBe("NumericEvaluation");
  });

  it("preserves an AndRule with two leaves", () => {
    const original: LoyaltyRule = {
      Kind: "AndRule",
      Children: [
        {
          Kind: "SimpleRule",
          LeftProvider: { Kind: "PathValueProvider", $type: "PathValueProvider", PropertyPath: "payload.a" },
          RightProvider: { Kind: "ConstantValueProvider", $type: "ConstantValueProvider", Value: 1 },
          Evaluator: { $type: "NumericEvaluation", Comparison: 1 },
        },
        {
          Kind: "SimpleRule",
          LeftProvider: { Kind: "PathValueProvider", $type: "PointBalanceProvider", PointAccountTypeId: "pat-1" },
          RightProvider: { Kind: "ConstantValueProvider", $type: "ConstantValueProvider", Value: 100 },
          Evaluator: { $type: "NumericEvaluation", Comparison: 2 },
        },
      ],
    };
    const state = parseRule(original);
    expect(state.conditions).toHaveLength(2);
    const rebuilt = buildRule(state.conditions, state.composite);
    expect(rebuilt?.Kind).toBe("AndRule");
    if (rebuilt?.Kind !== "AndRule") throw new Error("expected AndRule");
    expect(rebuilt.Children).toHaveLength(2);
    const second = rebuilt.Children[1];
    if (second?.Kind !== "SimpleRule") throw new Error("expected SimpleRule");
    const left = second.LeftProvider;
    if (left?.Kind !== "PathValueProvider") throw new Error("expected PathValueProvider variant");
    expect(left.$type).toBe("PointBalanceProvider");
    expect(left.PointAccountTypeId).toBe("pat-1");
  });

  it("preserves a TaxonomicRule include selection", () => {
    const original: LoyaltyRule = {
      Kind: "TaxonomicRule",
      LeftProvider: { Kind: "PathValueProvider", $type: "PathValueProvider", PropertyPath: "event.items" },
      RightProvider: { Kind: "ConstantValueProvider", $type: "ConstantValueProvider", Value: "alwaystrue" },
      Evaluator: { $type: "StringEvaluation", Comparison: 1 },
      IncludedTreeNodes: ["root.dairy", "root.snacks"],
      IncludedIds: ["sku-100"],
      ExcludedTreeNodes: [],
      ExcludedIds: [],
      TaxonomyType: "TreeRoot",
      TaxonomyId: "catalog-1",
      KeySymbolPath: "sku",
    };
    const state = parseRule(original);
    const rebuilt = buildRule(state.conditions, state.composite);
    if (rebuilt?.Kind !== "TaxonomicRule") throw new Error("expected TaxonomicRule");
    expect(rebuilt.IncludedTreeNodes).toEqual(["root.dairy", "root.snacks"]);
    expect(rebuilt.IncludedIds).toEqual(["sku-100"]);
    expect(rebuilt.TaxonomyId).toBe("catalog-1");
  });

  it("preserves HistoricalRule payload across parse -> build", () => {
    const original: LoyaltyRule = {
      Kind: "HistoricalRule",
      Id: "hist-1",
      AggregateType: "Count",
      HistoricalValueProvider: {
        Kind: "SimpleCalculationProvider",
        $type: "SimpleCalculationProvider",
        AggregateType: "Count",
        TemporalConstraint: {
          Kind: "TemporalConstraintRule",
          TimeOfOccurrenceProvider: {
            Kind: "PathValueProvider",
            $type: "PathValueProvider",
            PropertyPath: "event.transactionDate",
          },
          TemporalEvaluation: { $type: "TemporalEvaluation", ComparisonType: "OnOrAfter" },
          Comparison: "90.00:00:00",
        },
      },
      RightProvider: { Kind: "ConstantValueProvider", $type: "ConstantValueProvider", Value: 1 },
    };
    const state = parseRule(original);
    const rebuilt = buildRule(state.conditions, state.composite);
    expect(rebuilt).toEqual(original);
  });

  it("rebuilds HistoricalRule from edited historical config", () => {
    const original: LoyaltyRule = {
      Kind: "HistoricalRule",
      Id: "hist-edit-1",
      AggregateType: "Count",
      HistoricalValueProvider: { Kind: "SimpleCalculationProvider", $type: "SimpleCalculationProvider" },
      RightProvider: { Kind: "ConstantValueProvider", $type: "ConstantValueProvider", Value: 1 },
    };
    const state = parseRule(original);
    const cond = state.conditions[0];
    if (!cond) throw new Error("missing condition");
    cond.historicalEdited = true;
    cond.historicalConfig = {
      aggregateType: "Sum",
      valuePath: "event.total",
      occurrencePath: "event.transactionDate",
      windowDays: 45,
      thresholdProviderType: "ConstantValueProvider",
      thresholdValue: 250,
      thresholdPath: "event.threshold",
      thresholdPointAccountTypeId: "",
      temporalComparison: "OnOrAfter",
    };
    const rebuilt = buildRule(state.conditions, state.composite);
    if (rebuilt?.Kind !== "HistoricalRule") throw new Error("expected HistoricalRule");
    expect(rebuilt.AggregateType).toBe("Sum");
    expect((rebuilt.RightProvider as { Value?: number })?.Value).toBe(250);
    const provider = rebuilt.HistoricalValueProvider as {
      TemporalConstraint?: { Comparison?: string };
      InstanceValueProvider?: { PropertyPath?: string };
    };
    expect(provider.InstanceValueProvider?.PropertyPath).toBe("event.total");
    expect(provider.TemporalConstraint?.Comparison).toBe("45.00:00:00");
  });

  it("rebuilds HistoricalRule with path-based threshold provider", () => {
    const c = emptyCondition("historical");
    c.historicalEdited = true;
    c.historicalConfig = {
      aggregateType: "Count",
      valuePath: "event.price",
      occurrencePath: "event.transactionDate",
      windowDays: 14,
      thresholdProviderType: "PathValueProvider",
      thresholdValue: 0,
      thresholdPath: "event.dynamicThreshold",
      thresholdPointAccountTypeId: "",
      temporalComparison: "OnOrAfter",
    };
    const rebuilt = buildRule([c], "AndRule");
    if (rebuilt?.Kind !== "HistoricalRule") throw new Error("expected HistoricalRule");
    const right = rebuilt.RightProvider as { Kind?: string; PropertyPath?: string };
    expect(right.Kind).toBe("PathValueProvider");
    expect(right.PropertyPath).toBe("event.dynamicThreshold");
  });

  it("rebuilds HistoricalRule with point-balance threshold provider", () => {
    const c = emptyCondition("historical");
    c.historicalEdited = true;
    c.historicalConfig = {
      aggregateType: "Count",
      valuePath: "event.price",
      occurrencePath: "event.transactionDate",
      windowDays: 14,
      thresholdProviderType: "PointBalanceProvider",
      thresholdValue: 0,
      thresholdPath: "",
      thresholdPointAccountTypeId: "pat-threshold-1",
      temporalComparison: "OnOrAfter",
    };
    const rebuilt = buildRule([c], "AndRule");
    if (rebuilt?.Kind !== "HistoricalRule") throw new Error("expected HistoricalRule");
    const right = rebuilt.RightProvider as { Kind?: string; $type?: string; PointAccountTypeId?: string };
    expect(right.Kind).toBe("PathValueProvider");
    expect(right.$type).toBe("PointBalanceProvider");
    expect(right.PointAccountTypeId).toBe("pat-threshold-1");
  });

  it("emits category ids that line up with buildCategoryTree (so the picker preselects on edit)", () => {
    // Mirrors the live edit-existing flow: parseRule produces selection ids,
    // the picker subsequently fetches categories and runs them through
    // buildCategoryTree. Both id formats must match so the saved selection
    // can be located via findCategoryNode and rendered as checked.
    const rule: LoyaltyRule = {
      Kind: "TaxonomicRule",
      LeftProvider: { Kind: "PathValueProvider", $type: "PathValueProvider", PropertyPath: "event.items" },
      RightProvider: { Kind: "ConstantValueProvider", $type: "ConstantValueProvider", Value: "alwaystrue" },
      Evaluator: { $type: "StringEvaluation", Comparison: 1 },
      IncludedTreeNodes: ["root.dairy", "root.dairy.cheese"],
      IncludedIds: [],
      ExcludedTreeNodes: [],
      ExcludedIds: [],
      TaxonomyType: "TreeRoot",
      TaxonomyId: "catalog-1",
      KeySymbolPath: "sku",
    };
    const state = parseRule(rule);
    const selection = state.conditions[0]?.productSelection;
    if (!selection) throw new Error("expected productSelection");

    const tree = buildCategoryTree(["root.dairy.cheese", "root.dairy.milk"], "catalog-1");

    for (const cat of selection.categories) {
      const node = findCategoryNode(tree, cat.id);
      // We only assert ids that exist in the fetched tree (root.dairy and
      // root.dairy.cheese — root.dairy is created as a parent of cheese).
      if (cat.categoryPath === "root.dairy" || cat.categoryPath === "root.dairy.cheese") {
        expect(node).not.toBeNull();
        expect(node?.category).toBe(cat.categoryPath);
        expect(cat.catalogId).toBe("catalog-1");
      }
    }
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// isConditionValid
// ─────────────────────────────────────────────────────────────────────────────

describe("isConditionValid", () => {
  it("rejects standard condition with empty PathValueProvider", () => {
    const c = emptyCondition("standard");
    c.left.providerType = "PathValueProvider";
    c.left.propertyPath = "";
    c.right.providerType = "ConstantValueProvider";
    c.right.constantValue = 1;
    expect(isConditionValid(c)).toBe(false);
  });

  it("accepts standard condition with all sides populated", () => {
    const c = emptyCondition("standard");
    c.left.providerType = "PointBalanceProvider";
    c.left.pointAccountTypeId = "pat-1";
    c.right.providerType = "ConstantValueProvider";
    c.right.constantValue = 100;
    c.evaluator = "NumericGreaterThanOrEqual";
    expect(isConditionValid(c)).toBe(true);
  });

  it("rejects taxonomic condition with no selection", () => {
    const c = emptyCondition("taxonomic");
    c.productSelection = null;
    expect(isConditionValid(c)).toBe(false);
  });

  it("accepts taxonomic condition with categories selected", () => {
    const c = emptyCondition("taxonomic");
    c.productSelection = {
      mode: "include",
      categories: [{ id: "c1", name: "Dairy", categoryPath: "root.dairy", catalogId: "cat" }],
      entities: [],
    };
    expect(isConditionValid(c)).toBe(true);
  });

  it("areConditionsValid is true only when all conditions are valid", () => {
    const a = emptyCondition("standard");
    a.left.providerType = "PointBalanceProvider";
    a.left.pointAccountTypeId = "pat-1";
    a.right.providerType = "ConstantValueProvider";
    a.right.constantValue = 0;
    a.evaluator = "NumericGreaterThan";
    // b is configured to be incomplete: numeric evaluator with empty path
    const b = emptyCondition("standard");
    b.left.providerType = "PathValueProvider";
    b.left.propertyPath = "";
    b.evaluator = "NumericEquals";
    expect(areConditionsValid([a, b])).toBe(false);
    expect(areConditionsValid([a])).toBe(true);
  });

  it("accepts historical condition when preserved payload exists", () => {
    const c = emptyCondition("historical");
    c.historicalRule = {
      Kind: "HistoricalRule",
      AggregateType: "Count",
      HistoricalValueProvider: { Kind: "SimpleCalculationProvider", $type: "SimpleCalculationProvider" },
      RightProvider: { Kind: "ConstantValueProvider", $type: "ConstantValueProvider", Value: 1 },
    };
    expect(isConditionValid(c)).toBe(true);
  });

  it("rejects edited historical condition when threshold provider input is incomplete", () => {
    const c = emptyCondition("historical");
    c.historicalEdited = true;
    c.historicalConfig = {
      aggregateType: "Count",
      valuePath: "event.price",
      occurrencePath: "event.transactionDate",
      windowDays: 30,
      thresholdProviderType: "PathValueProvider",
      thresholdValue: 0,
      thresholdPath: "",
      thresholdPointAccountTypeId: "",
      temporalComparison: "OnOrAfter",
    };
    expect(isConditionValid(c)).toBe(false);
  });
});
