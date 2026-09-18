"use client";

/**
 * PromotionEditorModal — author the rule + outcomes for one journey-node
 * promotion (a `RuleSet`). Two panels:
 *   1. Rule builder — `<LoyaltyRuleBuilder>` from
 *      `@/components/loyalty/promotions/loyalty-rule-builder`. Loyalty rules
 *      use a Provider/Evaluator/Composite/Taxonomic ontology that does not
 *      map onto the CDP `<UniversalRuleBuilder>`'s profile/events/exclusion
 *      sections; the loyalty editor is colocated here for that reason.
 *   2. Outcome list — add/remove outcomes via `<OutcomeEditorModal>`.
 *
 * Saves the drafted `RuleSet` to the wizard store (the parent caller decides
 * which journey node it belongs to via the `nodeId` prop and `onSave`).
 */

import { useMemo, useState } from "react";
import { Pencil, Plus, Trash2 } from "lucide-react";
import { useQuery } from "@tanstack/react-query";

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
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Alert, AlertDescription } from "@/components/ui/alert";

import type { LoyaltyRule, LoyaltySchema, Outcome, PointAccountType, RuleSet } from "@/lib/campaign-types";
import { asPointAccountTypes } from "@/lib/campaign-types";
import { LoyaltyRuleBuilder } from "@/components/loyalty/promotions";
import { getPointAccountTypes } from "@/services/loyalty/actions";
import { loyaltyKeys } from "@/services/loyalty/query-keys";
import { OutcomeEditorModal, outcomeToFormValues, type OutcomeFormValues } from "./outcome-editor-modal";

export interface PromotionEditorModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  initial?: RuleSet | null;
  onSave: (ruleSet: RuleSet) => void;
  /**
   * Optional schema providing payload-property suggestions for the rule
   * builder's PathValueProvider/TaxonomicRule editors. When omitted the
   * editor falls back to free-text input.
   */
  schema?: LoyaltySchema | null;
  /** Optional schema choices mapped from campaign event types. */
  availableSchemas?: LoyaltySchema[];
}

function outcomesFromRuleSet(rs: RuleSet | null | undefined): Outcome[] {
  if (!rs?.outcomesJsonElement) return [];
  // Wire may deliver a single outcome as a plain object on very old records.
  if (Array.isArray(rs.outcomesJsonElement)) return rs.outcomesJsonElement as Outcome[];
  if (typeof rs.outcomesJsonElement === "object") return [rs.outcomesJsonElement as Outcome];
  return [];
}

function ruleFromRuleSet(rs: RuleSet | null | undefined): LoyaltyRule | null {
  const raw = rs?.ruleJsonElement;
  if (!raw || typeof raw !== "object") return null;
  return raw as LoyaltyRule;
}

const EMPTY_PATS: PointAccountType[] = [];
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

type RuleEditorMode = "builder" | "json";

function hasAdvancedRuleTopology(rule: LoyaltyRule | null): boolean {
  if (!rule) return false;
  if (rule.Kind === "NotRule") return true;
  if (rule.Kind === "AndRule" || rule.Kind === "OrRule") {
    const children = rule.Children ?? [];
    return children.some((child) => child.Kind === "AndRule" || child.Kind === "OrRule" || child.Kind === "NotRule");
  }
  return false;
}

export function PromotionEditorModal({
  open,
  onOpenChange,
  initial,
  onSave,
  schema,
  availableSchemas = [],
}: PromotionEditorModalProps) {
  const [name, setName] = useState<string>(initial?.name ?? "");
  const [outcomes, setOutcomes] = useState<Outcome[]>(outcomesFromRuleSet(initial));
  const [rule, setRule] = useState<LoyaltyRule | null>(ruleFromRuleSet(initial));
  const [schemaId, setSchemaId] = useState<string>(initial?.schemaId ?? schema?.id ?? "");
  const [ruleEditorMode, setRuleEditorMode] = useState<RuleEditorMode>(() =>
    hasAdvancedRuleTopology(ruleFromRuleSet(initial)) ? "json" : "builder"
  );
  const [ruleJsonText, setRuleJsonText] = useState<string>(
    ruleFromRuleSet(initial) ? JSON.stringify(ruleFromRuleSet(initial), null, 2) : ""
  );
  const [jsonRuleError, setJsonRuleError] = useState<string | null>(null);
  const [isOutcomeModalOpen, setIsOutcomeModalOpen] = useState(false);
  const [editingOutcomeIndex, setEditingOutcomeIndex] = useState<number | null>(null);
  const ruleBuilderKey = initial?.id ?? "new";

  // Load point-account types once for the builder + preview. Only fetch when
  // the modal is open to avoid useless network on a wizard that never opens
  // promotions.
  //
  // IMPORTANT: queryFn must unwrap the `ApiResponse` envelope and return the
  // plain `PointAccountType[]` array. The shared queryKey
  // `loyaltyKeys.pointAccountTypes.all` is used by every other consumer
  // (settings, accounts, OutcomeEditorModal, etc.) — they all expect the
  // cached value to be an ARRAY. Stuffing the raw envelope here would crash
  // `.filter` / `.map` calls on neighboring components reading the same cache.
  const patsQuery = useQuery({
    queryKey: loyaltyKeys.pointAccountTypes.all,
    queryFn: async () => {
      const result = await getPointAccountTypes();
      if (!result.success) throw new Error(result.error ?? "Failed to load point account types");
      return asPointAccountTypes(result.data);
    },
    enabled: open,
    staleTime: 60_000,
  });
  const pats = patsQuery.data ?? EMPTY_PATS;
  const schemaOptions = useMemo(
    () =>
      availableSchemas.filter(
        (candidate): candidate is LoyaltySchema & { id: string; name: string } =>
          typeof candidate.id === "string" && candidate.id.length > 0 && typeof candidate.name === "string"
      ),
    [availableSchemas]
  );

  // Re-seeding all happens in the `[initial, open]` effect above. Here we
  // just propagate the user-driven close back to the parent.
  const handleOpenChange = (next: boolean) => {
    onOpenChange(next);
  };

  const handleRuleChange = (nextRule: LoyaltyRule | null) => {
    setRule(nextRule);
  };

  const isValid = useMemo(() => name.trim().length > 0, [name]);

  const handleSave = () => {
    let nextRule = rule;
    if (ruleEditorMode === "json") {
      const trimmed = ruleJsonText.trim();
      if (!trimmed) {
        nextRule = null;
      } else {
        try {
          const parsed = JSON.parse(trimmed) as LoyaltyRule;
          nextRule = parsed;
        } catch {
          setJsonRuleError("Rule JSON is invalid. Fix parsing errors before saving.");
          return;
        }
      }
    }
    const ruleSet: RuleSet = {
      ...(initial ?? {}),
      id: initial?.id ?? crypto.randomUUID(),
      name: name.trim(),
      schemaId: schemaId.trim() || null,
      ruleJsonElement: nextRule ?? undefined,
      rootRuleDiscriminator: nextRule?.Kind ?? initial?.rootRuleDiscriminator ?? null,
      outcomesJsonElement: outcomes,
    };
    setJsonRuleError(null);
    onSave(ruleSet);
    onOpenChange(false);
  };

  const editingOutcome = editingOutcomeIndex !== null ? outcomes[editingOutcomeIndex] : null;
  const editingOutcomeInitial: OutcomeFormValues | undefined = editingOutcome
    ? outcomeToFormValues(editingOutcome)
    : undefined;
  const outcomesSummary = useMemo(() => summarizeOutcomes(outcomes), [outcomes]);

  const openAddOutcome = () => {
    setEditingOutcomeIndex(null);
    setIsOutcomeModalOpen(true);
  };

  const openEditOutcome = (index: number) => {
    setEditingOutcomeIndex(index);
    setIsOutcomeModalOpen(true);
  };

  const handleSaveOutcome = (outcome: Outcome) => {
    if (editingOutcomeIndex === null) {
      setOutcomes((prev) => [...prev, outcome]);
      return;
    }
    setOutcomes((prev) => prev.map((item, index) => (index === editingOutcomeIndex ? outcome : item)));
  };

  return (
    <>
      <Dialog open={open} onOpenChange={handleOpenChange}>
        <DialogContent className="max-h-[95vh] w-[min(95vw,80rem)] max-w-none overflow-y-auto sm:max-w-none">
          <DialogHeader>
            <DialogTitle>{initial ? "Edit promotion" : "Add promotion"}</DialogTitle>
            <DialogDescription>
              Build the rule that triggers this promotion and the outcomes it awards.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-6">
            <div className="space-y-2">
              <Label htmlFor="promotion-name">Promotion name</Label>
              <Input
                id="promotion-name"
                placeholder="Birthday bonus rule"
                value={name}
                onChange={(e) => setName(e.target.value)}
              />
            </div>

            <div className="space-y-2">
              <Label>Event schema</Label>
              {schemaOptions.length === 0 ? (
                <Input
                  value={schemaId}
                  onChange={(event) => setSchemaId(event.target.value)}
                  placeholder="Schema ID (optional)"
                />
              ) : (
                <Select value={schemaId || undefined} onValueChange={setSchemaId}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select schema for this promotion" />
                  </SelectTrigger>
                  <SelectContent>
                    {schemaOptions.map((candidate) => (
                      <SelectItem key={candidate.id} value={candidate.id}>
                        {candidate.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            </div>

            <div className="grid gap-6 xl:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]">
              <section className="space-y-2">
                <header>
                  <h4 className="text-sm font-semibold">Rule</h4>
                  <p className="text-xs text-muted-foreground">{outcomesSummary}</p>
                </header>
                <Tabs
                  value={ruleEditorMode}
                  onValueChange={(value) => setRuleEditorMode(value as RuleEditorMode)}
                  className="space-y-2"
                >
                  <TabsList>
                    <TabsTrigger value="builder">Builder</TabsTrigger>
                    <TabsTrigger value="json">JSON</TabsTrigger>
                  </TabsList>
                </Tabs>
                {hasAdvancedRuleTopology(rule) && ruleEditorMode === "builder" ? (
                  <Alert>
                    <AlertDescription>
                      This rule contains nested/NOT topology. Switch to JSON mode to edit without losing structure.
                    </AlertDescription>
                  </Alert>
                ) : null}
                {ruleEditorMode === "json" ? (
                  <div className="space-y-2">
                    <Textarea
                      className="min-h-[320px] font-mono text-xs"
                      value={ruleJsonText}
                      onChange={(event) => setRuleJsonText(event.target.value)}
                      placeholder='{"Kind":"AndRule","Children":[...]}'
                    />
                    {jsonRuleError ? <p className="text-xs text-destructive">{jsonRuleError}</p> : null}
                  </div>
                ) : (
                  <LoyaltyRuleBuilder
                    key={ruleBuilderKey}
                    value={rule}
                    onChange={handleRuleChange}
                    schema={schema ?? null}
                    pointAccountTypes={pats}
                  />
                )}
              </section>

              <aside className="xl:sticky xl:top-0 xl:self-start">
                <Card className="border-primary/30 bg-primary/5">
                  <CardHeader className="space-y-2">
                    <div className="flex items-center justify-between gap-2">
                      <CardTitle className="text-base">Outcomes</CardTitle>
                      <Badge variant="secondary">{outcomes.length} configured</Badge>
                    </div>
                    <CardDescription>What this promotion awards when its rule matches.</CardDescription>
                    <Button type="button" variant="outline" size="sm" onClick={openAddOutcome}>
                      <Plus className="mr-1.5 h-3.5 w-3.5" />
                      Add outcome
                    </Button>
                  </CardHeader>
                  <CardContent className="space-y-2">
                    {outcomes.length === 0 ? (
                      <p className="text-sm text-muted-foreground">No outcomes configured yet.</p>
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
                                onClick={() => setOutcomes(outcomes.filter((_, i) => i !== index))}
                              >
                                <Trash2 className="h-3.5 w-3.5" />
                                <span className="sr-only">Remove outcome</span>
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
            <Button type="button" disabled={!isValid} onClick={handleSave}>
              Save promotion
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
