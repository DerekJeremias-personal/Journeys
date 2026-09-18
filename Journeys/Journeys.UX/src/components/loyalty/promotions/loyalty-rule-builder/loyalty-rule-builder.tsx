"use client";

/**
 * LoyaltyRuleBuilder — controlled editor for ELP `Rule` (PascalCase
 * JsonElement) instances.
 *
 * Domain semantics: loyalty rules use a `Provider {Evaluator} Provider` model
 * with a specialized `TaxonomicRule` for product/category matching, composed
 * with `AndRule` / `OrRule`. This is a fundamentally different ontology from
 * the CDP `<UniversalRuleBuilder>` (which models profile filters + event
 * sequences + exclusions); see Phase 09 checkpoint follow-up notes.
 *
 * Authored next to the PromotionEditor since it is consumed only there
 * (campaign wizard journey nodes). If a second loyalty consumer appears,
 * relocate to `components/loyalty/rules/` without behavioural change.
 *
 * Wire shape: the component emits the canonical `Rule` from
 * `@/lib/campaign-types/loyalty-rules` (PascalCase keys, raw JsonElement inner
 * values), the same shape the BFF echoes from ELP. Zero translation in the
 * consumer.
 */

import { useCallback, useMemo, useState } from "react";
import { Plus, Info } from "lucide-react";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";

import type { LoyaltyRule, LoyaltySchema, PointAccountType } from "@/lib/campaign-types";

import { areConditionsValid, buildRule } from "./build-rule";
import { ConditionCard, type SchemaProperty } from "./condition-card";
import { parseRule } from "./parse-rule";
import { RulePreview } from "./rule-preview";
import { emptyCondition, type CompositeKind, type Condition, type ConditionSide } from "./types";

export interface LoyaltyRuleBuilderProps {
  /** Current rule value. `null` / `undefined` starts with a single empty condition. */
  value: LoyaltyRule | null | undefined;
  /** Fires whenever the editor produces a new rule (or `null` if cleared). */
  onChange: (rule: LoyaltyRule | null) => void;
  /** Optional schema providing payload property suggestions. */
  schema?: LoyaltySchema | null;
  /** Optional list of point-account types (avoids the editor refetching). */
  pointAccountTypes?: Pick<PointAccountType, "id" | "name">[];
  className?: string;
}

const EMPTY_PATS: Pick<PointAccountType, "id" | "name">[] = [];

export function LoyaltyRuleBuilder({
  value,
  onChange,
  schema,
  pointAccountTypes = EMPTY_PATS,
  className,
}: LoyaltyRuleBuilderProps) {
  // Initialise state once from the incoming value. Callers that need to swap
  // the active rule should remount via `key={ruleSet?.id}` — keeping the
  // editor purely uncontrolled-after-mount avoids set-state-in-effect lint
  // failures and the React Compiler ref restrictions.
  const [{ conditions, composite, supported }, setEditorState] = useState(() => parseRule(value));

  // Important: do NOT call `onChange` inside the `setEditorState` updater.
  // React invokes updaters during render (StrictMode double-invokes them on
  // dev), and calling a parent's setState from there triggers the
  // "cannot update a component while rendering a different component"
  // warning. Each handler computes the next state from its closure, calls
  // setEditorState with the concrete value, then calls onChange in the
  // event handler scope.
  const applyConditions = useCallback(
    (next: Condition[]) => {
      setEditorState((prev) => ({ ...prev, conditions: next, supported: true }));
      onChange(buildRule(next, composite));
    },
    [composite, onChange]
  );

  const handleSetComposite = useCallback(
    (next: CompositeKind) => {
      setEditorState((prev) => ({ ...prev, composite: next, supported: true }));
      onChange(buildRule(conditions, next));
    },
    [conditions, onChange]
  );

  const schemaProperties: SchemaProperty[] = useMemo(() => {
    if (!schema) return [];
    return (schema.attributes ?? []).flatMap((attr) => {
      const path = attr.symbol ?? attr.name;
      if (!path) return [];
      return [
        {
          path,
          label: attr.displayName ?? path,
          dataType: typeof attr.dataType === "string" ? attr.dataType : "String"
        }
      ];
    });
  }, [schema]);

  const handleUpdateCondition = useCallback(
    (index: number, updates: Partial<Condition>) => {
      applyConditions(conditions.map((c, i) => (i === index ? { ...c, ...updates } : c)));
    },
    [applyConditions, conditions]
  );

  const handleUpdateSide = useCallback(
    (index: number, side: "left" | "right", updates: Partial<ConditionSide>) => {
      applyConditions(conditions.map((c, i) => (i === index ? { ...c, [side]: { ...c[side], ...updates } } : c)));
    },
    [applyConditions, conditions]
  );

  const handleAddCondition = useCallback(() => {
    applyConditions([...conditions, emptyCondition()]);
  }, [applyConditions, conditions]);

  const handleRemoveCondition = useCallback(
    (index: number) => {
      if (conditions.length <= 1) return;
      applyConditions(conditions.filter((_, i) => i !== index));
    },
    [applyConditions, conditions]
  );

  const isValid = areConditionsValid(conditions);

  return (
    <div className={className}>
      <div className="space-y-3">
        {!supported && (
          <Alert>
            <Info className="h-4 w-4" />
            <AlertTitle>Complex rule</AlertTitle>
            <AlertDescription>
              This rule uses nested composites or advanced rule types not editable here. The original rule is preserved
              on save unless you replace it below.
            </AlertDescription>
          </Alert>
        )}

        {conditions.length > 1 && (
          <div className="flex items-center gap-3">
            <Label className="text-xs text-muted-foreground">Combine conditions with:</Label>
            <ToggleGroup
              type="single"
              size="sm"
              value={composite}
              onValueChange={(v) => v && handleSetComposite(v as CompositeKind)}
            >
              <ToggleGroupItem value="AndRule" aria-label="All conditions must be true">
                AND
              </ToggleGroupItem>
              <ToggleGroupItem value="OrRule" aria-label="Any condition can be true">
                OR
              </ToggleGroupItem>
            </ToggleGroup>
            <span className="text-xs text-muted-foreground">
              {composite === "AndRule" ? "All conditions must be true" : "Any condition can be true"}
            </span>
          </div>
        )}

        <div className="space-y-3">
          {conditions.map((condition, index) => (
            <ConditionCard
              key={condition.id}
              condition={condition}
              index={index}
              isOnlyCondition={conditions.length === 1}
              composite={composite}
              schemaProperties={schemaProperties}
              pointAccountTypes={pointAccountTypes}
              onUpdate={(updates) => handleUpdateCondition(index, updates)}
              onUpdateSide={(side, updates) => handleUpdateSide(index, side, updates)}
              onRemove={() => handleRemoveCondition(index)}
            />
          ))}
        </div>

        <Button type="button" variant="outline" className="w-full border-dashed" onClick={handleAddCondition}>
          <Plus className="mr-1.5 h-4 w-4" />
          Add another condition
        </Button>

        <RulePreview conditions={conditions} composite={composite} pointAccountTypes={pointAccountTypes} />

        {!isValid && (
          <p className="text-xs text-muted-foreground">Complete every condition to enable saving the promotion.</p>
        )}
      </div>
    </div>
  );
}
