"use client";

/**
 * RulePreview — live "IF X AND Y" textual summary of the conditions.
 */

import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import type { PointAccountType } from "@/lib/campaign-types";

import {
  EVALUATOR_BY_VALUE,
  type CompositeKind,
  type Condition,
  type ConditionSide,
  type UiProviderType,
} from "./types";

export interface RulePreviewProps {
  conditions: Condition[];
  composite: CompositeKind;
  pointAccountTypes: Pick<PointAccountType, "id" | "name">[];
}

function formatSide(side: ConditionSide, pats: Pick<PointAccountType, "id" | "name">[]): string {
  switch (side.providerType) {
    case "PointBalanceProvider": {
      const pat = pats.find((p) => p.id === side.pointAccountTypeId);
      return pat ? `Balance(${pat.name})` : "Balance(?)";
    }
    case "PathValueProvider":
      return side.propertyPath || "<property>";
    case "AggregateValueProvider":
      return "Sum(...)";
    case "ProductListProvider":
      return "Products(?)";
    case "ConstantValueProvider":
      if (typeof side.constantValue === "boolean") return side.constantValue ? "true" : "false";
      return JSON.stringify(side.constantValue);
  }
}

function describeProviderType(type: UiProviderType): string {
  switch (type) {
    case "PointBalanceProvider":
      return "point balance";
    case "PathValueProvider":
      return "event property";
    case "AggregateValueProvider":
      return "aggregate";
    case "ProductListProvider":
      return "product list";
    case "ConstantValueProvider":
      return "constant";
  }
}

export function RulePreview({ conditions, composite, pointAccountTypes }: RulePreviewProps) {
  if (conditions.length === 0) return null;
  const compositeWord = composite === "AndRule" ? "AND" : "OR";

  const formatHistoricalThreshold = (condition: Condition): string => {
    const cfg = condition.historicalConfig;
    if (!cfg) return "?";
    if (cfg.thresholdProviderType === "PathValueProvider") return cfg.thresholdPath || "<property>";
    if (cfg.thresholdProviderType === "PointBalanceProvider") {
      const pat = pointAccountTypes.find((p) => p.id === cfg.thresholdPointAccountTypeId);
      return pat ? `Balance(${pat.name})` : "Balance(?)";
    }
    return String(cfg.thresholdValue);
  };

  return (
    <Card>
      <CardHeader className="py-3">
        <CardTitle className="text-xs uppercase tracking-wider text-muted-foreground">Rule preview</CardTitle>
      </CardHeader>
      <CardContent className="space-y-1.5 pb-4 text-sm">
        {conditions.map((condition, index) => {
          const isFirst = index === 0;
          const evMeta = EVALUATOR_BY_VALUE.get(condition.evaluator);
          const isTaxonomic = condition.kind === "taxonomic";
          const selection = condition.productSelection;
          return (
            <div key={condition.id} className="flex flex-wrap items-center gap-1.5">
              {!isFirst && <span className="font-bold text-muted-foreground">{compositeWord}</span>}
              {isFirst && <span className="font-bold text-muted-foreground">IF</span>}
              {isTaxonomic ? (
                <>
                  <code className="rounded bg-muted px-1.5 py-0.5 text-xs">
                    {condition.itemsPropertyPath || "event.items"}
                  </code>
                  <span className="text-xs text-muted-foreground">
                    {selection?.mode === "include" ? "matches products in" : "excludes products in"}
                  </span>
                  <code className="rounded bg-muted px-1.5 py-0.5 text-xs">
                    [{selection?.categories.length ?? 0} categories, {selection?.entities.length ?? 0} SKUs]
                  </code>
                </>
              ) : condition.kind === "historical" ? (
                <>
                  <code className="rounded bg-muted px-1.5 py-0.5 text-xs">
                    {condition.historicalConfig?.aggregateType ?? "Count"}(
                    {condition.historicalConfig?.valuePath ?? "..."})
                  </code>
                  <span className="text-xs text-muted-foreground">over last</span>
                  <code className="rounded bg-muted px-1.5 py-0.5 text-xs">
                    {condition.historicalConfig?.windowDays ?? "?"}d
                  </code>
                  <span className="text-xs text-muted-foreground">where time</span>
                  <code className="rounded bg-muted px-1.5 py-0.5 text-xs">
                    {condition.historicalConfig?.occurrencePath ?? "..."}
                  </code>
                  <span className="text-xs text-muted-foreground">&gt;=</span>
                  <code className="rounded bg-muted px-1.5 py-0.5 text-xs">{formatHistoricalThreshold(condition)}</code>
                </>
              ) : (
                <>
                  <code
                    className="rounded bg-muted px-1.5 py-0.5 text-xs"
                    title={describeProviderType(condition.left.providerType)}
                  >
                    {formatSide(condition.left, pointAccountTypes)}
                  </code>
                  <span className="text-xs text-muted-foreground">{evMeta?.label ?? condition.evaluator}</span>
                  <code
                    className="rounded bg-muted px-1.5 py-0.5 text-xs"
                    title={describeProviderType(condition.right.providerType)}
                  >
                    {formatSide(condition.right, pointAccountTypes)}
                  </code>
                </>
              )}
            </div>
          );
        })}
      </CardContent>
    </Card>
  );
}
