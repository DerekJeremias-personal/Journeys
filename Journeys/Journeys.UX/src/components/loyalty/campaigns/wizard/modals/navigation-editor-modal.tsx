"use client";

/**
 * NavigationEditorModal — author one Entry / Transition / Exit navigation
 * criterion on a journey node.
 *
 * Two halves:
 *   1. **Constraint** — any `LoyaltyRule` (composite or leaf, including
 *      TaxonomicRule with the product selector) authored via the same
 *      `<LoyaltyRuleBuilder>` the PromotionEditorModal uses.
 *   2. **Outcomes** — list of `Outcome` records that ELP awards when this
 *      navigation fires. Backed by `SimpleNavigationCriteria.Outcomes` on the
 *      ELP server, calculated and awarded by
 *      `NavigateAndAwardNavigationOutcomesAsync`. Same wire shape as
 *      promotion outcomes (`Outcome` discriminated union).
 */

import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";

import { ArrowRight, DoorOpen, LogOut, Pencil, Plus, Trash2 } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

import { LoyaltyRuleBuilder } from "@/components/loyalty/promotions";
import { areConditionsValid, parseRule } from "@/components/loyalty/promotions/loyalty-rule-builder";
import { getPointAccountTypes } from "@/services/loyalty/actions";
import { loyaltyKeys } from "@/services/loyalty/query-keys";
import { asPointAccountTypes } from "@/lib/campaign-types";
import type {
  LoyaltyRule,
  LoyaltySchema,
  NavigationInner,
  NavigationType,
  Outcome,
  PointAccountType,
} from "@/lib/campaign-types";

import { OutcomeEditorModal, outcomeToFormValues, type OutcomeFormValues } from "./outcome-editor-modal";

const EMPTY_PATS: Pick<PointAccountType, "id" | "name">[] = [];
const EDITABLE_OUTCOME_KINDS = new Set([
  "DepositPointsOutcome",
  "SpendPointsOutcome",
  "ExpirePointsOutcome",
  "TagOutcome",
]);

function isEditableOutcome(outcome: Outcome): boolean {
  return EDITABLE_OUTCOME_KINDS.has(outcome.Kind);
}

function labelForOutcomeKind(kind: Outcome["Kind"]): string {
  switch (kind) {
    case "DepositPointsOutcome":
      return "Deposit";
    case "SpendPointsOutcome":
      return "Spend";
    case "ExpirePointsOutcome":
      return "Expiration";
    case "TagOutcome":
      return "Tag";
    default:
      return kind;
  }
}

function summarizeOutcomes(outcomes: Outcome[]): string {
  if (outcomes.length === 0) return "No outcomes configured yet.";
  const uniqueKinds = Array.from(new Set(outcomes.map((outcome) => labelForOutcomeKind(outcome.Kind))));
  const preview = uniqueKinds.slice(0, 2).join(" + ");
  const moreKinds = uniqueKinds.length > 2 ? ` +${uniqueKinds.length - 2} more` : "";
  return `${outcomes.length} outcome${outcomes.length === 1 ? "" : "s"} configured: ${preview}${moreKinds}`;
}

const DIRECTION_META: Record<
  NavigationType,
  { title: string; description: string; tone: "success" | "info" | "warning"; icon: typeof DoorOpen }
> = {
  Entry: {
    title: "Entry condition",
    description: "Define when a loyalty account is allowed to enter this journey step.",
    tone: "success",
    icon: DoorOpen,
  },
  Transition: {
    title: "Transition condition",
    description: "Define when a loyalty account moves into this step from its parent.",
    tone: "info",
    icon: ArrowRight,
  },
  Exit: {
    title: "Exit condition",
    description: "Define when a loyalty account should leave this journey step.",
    tone: "warning",
    icon: LogOut,
  },
};

const TONE_CLASSES: Record<"success" | "info" | "warning", string> = {
  success: "border-emerald-500/40 bg-emerald-50/40 dark:bg-emerald-950/20 text-emerald-700 dark:text-emerald-400",
  info: "border-sky-500/40 bg-sky-50/40 dark:bg-sky-950/20 text-sky-700 dark:text-sky-400",
  warning: "border-amber-500/40 bg-amber-50/40 dark:bg-amber-950/20 text-amber-700 dark:text-amber-400",
};

export interface NavigationEditorModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Which navigation slot the user is editing. Determines the title + tone. */
  direction: NavigationType;
  /** Existing inner criterion to edit, or `null` to author a new one. */
  initial?: NavigationInner | null;
  /** Schema providing `payload.<symbol>` suggestions to the rule builder. */
  schema?: LoyaltySchema | null;
  /** Optional point-account-types passthrough so the rule builder doesn't refetch. */
  pointAccountTypes?: Pick<PointAccountType, "id" | "name">[];
  /** Save the authored `NavigationInner`. Caller merges it into the `Navigation` map. */
  onSave: (next: NavigationInner) => void;
}

function ruleFromConstraint(c: NavigationInner["NavConstraint"] | undefined): LoyaltyRule | null {
  if (!c || typeof c !== "object") return null;
  return c as LoyaltyRule;
}

function outcomesFromInner(inner: NavigationInner | null | undefined): Outcome[] {
  if (!inner?.Outcomes) return [];
  if (Array.isArray(inner.Outcomes)) return inner.Outcomes;
  // Defensive: some legacy records may store a single outcome as an object.
  if (typeof inner.Outcomes === "object") return [inner.Outcomes as Outcome];
  return [];
}

export function NavigationEditorModal({
  open,
  onOpenChange,
  direction,
  initial,
  schema,
  pointAccountTypes = EMPTY_PATS,
  onSave,
}: NavigationEditorModalProps) {
  const [name, setName] = useState<string>(initial?.Name ?? "");
  const [rule, setRule] = useState<LoyaltyRule | null>(ruleFromConstraint(initial?.NavConstraint));
  const [outcomes, setOutcomes] = useState<Outcome[]>(outcomesFromInner(initial));
  const [isOutcomeModalOpen, setIsOutcomeModalOpen] = useState(false);
  const [editingOutcomeIndex, setEditingOutcomeIndex] = useState<number | null>(null);

  // Fetch PATs on-demand when the caller doesn't pass them through.
  const patsQuery = useQuery({
    queryKey: loyaltyKeys.pointAccountTypes.all,
    queryFn: async () => {
      const result = await getPointAccountTypes();
      if (!result.success) throw new Error(result.error ?? "Failed to load point account types");
      return asPointAccountTypes(result.data);
    },
    enabled: open && pointAccountTypes.length === 0,
    staleTime: 60_000,
  });
  const resolvedPointAccountTypes = useMemo(
    () => (pointAccountTypes.length > 0 ? pointAccountTypes : (patsQuery.data ?? EMPTY_PATS)),
    [pointAccountTypes, patsQuery.data]
  );

  // Re-seed staging state on each open. Callers also remount via `key` (see
  // node-config-panel) so this is a belt-and-braces guard.
  const handleOpenChange = (next: boolean) => {
    if (next) {
      setName(initial?.Name ?? "");
      setRule(ruleFromConstraint(initial?.NavConstraint));
      setOutcomes(outcomesFromInner(initial));
    }
    onOpenChange(next);
  };

  // A null rule is valid — it means "no constraint (always apply)".
  // A non-null rule is valid only when all its conditions are complete.
  const ruleIsValid = rule === null || areConditionsValid(parseRule(rule).conditions);

  const handleSave = () => {
    if (!ruleIsValid) return;
    const inner: NavigationInner = {
      ...initial,
      Name: name.trim(),
      NavigationType: direction,
      NavConstraint: rule,
      Outcomes: outcomes.length > 0 ? outcomes : null,
    };
    onSave(inner);
    onOpenChange(false);
  };

  const handleSaveOutcome = (outcome: Outcome) => {
    if (editingOutcomeIndex === null) {
      setOutcomes((prev) => [...prev, outcome]);
      return;
    }
    setOutcomes((prev) => prev.map((item, index) => (index === editingOutcomeIndex ? outcome : item)));
  };

  const handleRemoveOutcome = (index: number) => {
    setOutcomes((prev) => prev.filter((_, i) => i !== index));
  };

  const openAddOutcome = () => {
    setEditingOutcomeIndex(null);
    setIsOutcomeModalOpen(true);
  };

  const openEditOutcome = (index: number) => {
    setEditingOutcomeIndex(index);
    setIsOutcomeModalOpen(true);
  };

  const editingOutcome = editingOutcomeIndex !== null ? outcomes[editingOutcomeIndex] : null;
  const editingOutcomeInitial: OutcomeFormValues | undefined = editingOutcome
    ? outcomeToFormValues(editingOutcome)
    : undefined;
  const outcomesSummary = useMemo(() => summarizeOutcomes(outcomes), [outcomes]);

  const meta = DIRECTION_META[direction];
  const Icon = meta.icon;

  return (
    <>
      <Dialog open={open} onOpenChange={handleOpenChange}>
        <DialogContent className="max-h-[95vh] w-[min(95vw,80rem)] max-w-none overflow-y-auto sm:max-w-none">
          <DialogHeader>
            <div className="flex items-start gap-3">
              <span
                className={`flex h-10 w-10 items-center justify-center rounded-md border ${TONE_CLASSES[meta.tone]}`}
                aria-hidden="true"
              >
                <Icon className="h-5 w-5" />
              </span>
              <div className="space-y-1">
                <DialogTitle>{meta.title}</DialogTitle>
                <DialogDescription>{meta.description}</DialogDescription>
              </div>
            </div>
          </DialogHeader>

          <div className="space-y-6">
            <div className="space-y-2">
              <Label htmlFor="navigation-name">Name (optional)</Label>
              <Input
                id="navigation-name"
                placeholder={`e.g. ${
                  direction === "Entry"
                    ? "First qualifying purchase"
                    : direction === "Exit"
                      ? "Reached spend threshold"
                      : "Promoted from previous tier"
                }`}
                value={name}
                onChange={(e) => setName(e.target.value)}
              />
            </div>

            <div className="grid gap-6 xl:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]">
              <section className="space-y-2">
                <header>
                  <h4 className="text-sm font-semibold">Constraint</h4>
                  <p className="text-xs text-muted-foreground">
                    The condition that must be true for this {direction.toLowerCase()} to apply. Leave empty to always
                    allow.
                  </p>
                  <p className="text-xs text-muted-foreground">{outcomesSummary}</p>
                </header>
                <LoyaltyRuleBuilder
                  key={`${direction}-${initial?.Name ?? "new"}`}
                  value={rule}
                  onChange={setRule}
                  schema={schema ?? null}
                  pointAccountTypes={resolvedPointAccountTypes}
                />
              </section>

              <aside className="xl:sticky xl:top-0 xl:self-start">
                <Card className="border-primary/30 bg-primary/5">
                  <CardHeader className="space-y-2">
                    <div className="flex items-center justify-between gap-2">
                      <CardTitle className="text-base">Outcomes</CardTitle>
                      <Badge variant="secondary">{outcomes.length} configured</Badge>
                    </div>
                    <CardDescription>Awarded whenever this {direction.toLowerCase()} navigation fires.</CardDescription>
                    <Button type="button" variant="outline" size="sm" onClick={openAddOutcome}>
                      <Plus className="mr-1.5 h-3.5 w-3.5" />
                      Add outcome
                    </Button>
                  </CardHeader>
                  <CardContent className="space-y-2">
                    {outcomes.length === 0 ? (
                      <p className="text-sm text-muted-foreground">No navigation outcomes configured yet.</p>
                    ) : (
                      <ul className="max-h-[22rem] space-y-2 overflow-y-auto pr-1">
                        {outcomes.map((outcome, index) => (
                          <li
                            key={outcome.Id ?? `${outcome.Kind}-${index}`}
                            className="flex items-center justify-between rounded-md border border-border bg-background px-3 py-2 text-sm"
                          >
                            <div className="flex min-w-0 flex-col">
                              <span className="truncate font-medium">{outcome.Kind}</span>
                              <span className="text-xs text-muted-foreground">
                                Event: {outcome.EventType ?? "—"} ({outcome.EventId ?? "—"})
                              </span>
                            </div>
                            <div className="flex items-center gap-1">
                              <Button
                                type="button"
                                variant="ghost"
                                size="sm"
                                onClick={() => openEditOutcome(index)}
                                disabled={!isEditableOutcome(outcome)}
                              >
                                <Pencil className="h-3.5 w-3.5" />
                                <span className="sr-only">Edit outcome</span>
                              </Button>
                              <Button
                                type="button"
                                variant="ghost"
                                size="sm"
                                onClick={() => handleRemoveOutcome(index)}
                                aria-label="Remove outcome"
                              >
                                <Trash2 className="h-3.5 w-3.5" />
                              </Button>
                            </div>
                          </li>
                        ))}
                      </ul>
                    )}
                  </CardContent>
                </Card>
              </aside>
            </div>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="button" onClick={handleSave} disabled={!ruleIsValid}>
              Save {direction.toLowerCase()}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <OutcomeEditorModal
        open={isOutcomeModalOpen}
        onOpenChange={(next) => {
          setIsOutcomeModalOpen(next);
          if (!next) setEditingOutcomeIndex(null);
        }}
        initial={editingOutcomeInitial}
        initialOutcome={editingOutcome}
        onSave={handleSaveOutcome}
      />
    </>
  );
}
