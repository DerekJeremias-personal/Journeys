"use client";

/**
 * NodeConfigPanel — right-rail editor for the currently selected journey node.
 * Renders three tabs (Promotions / Navigation / Settings) plus the Delete Step
 * action that mutates the wizard store.
 *
 * Promotion + navigation editors live in their own modals, launched via
 * buttons in this panel.
 */

import { startTransition, useEffect, useMemo, useRef, useState } from "react";
import { useFormContext, useWatch } from "react-hook-form";
import { ArrowRight, DoorOpen, LogOut, Pencil, Plus, Trash2 } from "lucide-react";
import { useQuery } from "@tanstack/react-query";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { cn } from "@/lib/utils";
import { getAllSchemas } from "@/services/loyalty/actions";
import { loyaltyKeys } from "@/services/loyalty/query-keys";

import {
  selectJourney,
  selectSelectedNodeId,
  selectSelectedSchema,
  useWizardActions,
  useWizardStore,
} from "../wizard-store";
import type { Journey, LoyaltyRule, LoyaltySchema, Navigation, NavigationInner, NavigationType, RuleSet } from "@/lib/campaign-types";
import { asLoyaltySchemas } from "@/lib/campaign-types";
import { PromotionEditorModal } from "../modals/promotion-editor-modal";
import { NavigationEditorModal } from "../modals/navigation-editor-modal";
import type { CampaignWizardValues } from "../campaign-wizard";

function findNode(node: Journey, id: string): Journey | null {
  if (node.id === id) return node;
  for (const child of node.children ?? []) {
    const hit = findNode(child, id);
    if (hit) return hit;
  }
  return null;
}

function isEventableSchemaNameOrId(schema: { id?: string | null; name?: string | null }, event: string): boolean {
  if (!schema.id && !schema.name) return false;
  const normalized = event.toLowerCase();
  return (schema.id ?? "").toLowerCase() === normalized || (schema.name ?? "").toLowerCase() === normalized;
}

interface NavigationDirectionMeta {
  id: NavigationType;
  label: string;
  description: string;
  icon: typeof DoorOpen;
  /** Tailwind classes for the indicator dot when enabled. */
  enabledDot: string;
  /** Tailwind classes for the icon background when enabled. */
  enabledIcon: string;
}

const NAVIGATION_DIRECTIONS: ReadonlyArray<NavigationDirectionMeta> = [
  {
    id: "Entry",
    label: "Entry",
    description: "When loyalty accounts can enter this step.",
    icon: DoorOpen,
    enabledDot: "bg-emerald-500",
    enabledIcon: "border-emerald-500/40 bg-emerald-50 text-emerald-700 dark:bg-emerald-950/30 dark:text-emerald-400",
  },
  {
    id: "Transition",
    label: "Transition",
    description: "Moving from the parent step into this one.",
    icon: ArrowRight,
    enabledDot: "bg-sky-500",
    enabledIcon: "border-sky-500/40 bg-sky-50 text-sky-700 dark:bg-sky-950/30 dark:text-sky-400",
  },
  {
    id: "Exit",
    label: "Exit",
    description: "When loyalty accounts should leave this step.",
    icon: LogOut,
    enabledDot: "bg-amber-500",
    enabledIcon: "border-amber-500/40 bg-amber-50 text-amber-700 dark:bg-amber-950/30 dark:text-amber-400",
  },
] as const;

interface NavigationDirectionCardProps {
  direction: NavigationDirectionMeta;
  inner: NavigationInner | null;
  onToggle: (on: boolean) => void;
  onEdit: () => void;
}

/** Summary text rendered below the direction label when enabled. */
function summarizeConstraint(constraint: NavigationInner["NavConstraint"] | null | undefined): string {
  if (!constraint || typeof constraint !== "object") return "No conditions";
  const rule = constraint as LoyaltyRule;
  if ((rule.Kind === "AndRule" || rule.Kind === "OrRule") && Array.isArray(rule.Children)) {
    const word = rule.Kind === "AndRule" ? "AND" : "OR";
    return `${rule.Children.length} condition${rule.Children.length === 1 ? "" : "s"} (${word})`;
  }
  if (rule.Kind === "TaxonomicRule") {
    return "Product/category constraint";
  }
  if (
    rule.Kind === "SimpleRule" ||
    rule.Kind === "DatePropertyRule" ||
    rule.Kind === "NumericPropertyRule" ||
    rule.Kind === "StringPropertyRule"
  ) {
    return "1 condition";
  }
  return "Configured";
}

interface PromotionRowProps {
  ruleSet: RuleSet;
  index: number;
  onEdit: () => void;
  onDelete: () => void;
}

function summarizeRule(rule: LoyaltyRule | null | undefined): string {
  if (!rule) return "No rule configured";
  if ((rule.Kind === "AndRule" || rule.Kind === "OrRule") && Array.isArray(rule.Children)) {
    const word = rule.Kind === "AndRule" ? "AND" : "OR";
    return `${rule.Children.length} condition${rule.Children.length === 1 ? "" : "s"} (${word})`;
  }
  if (rule.Kind === "TaxonomicRule") return "Product/category constraint";
  if (
    rule.Kind === "SimpleRule" ||
    rule.Kind === "DatePropertyRule" ||
    rule.Kind === "NumericPropertyRule" ||
    rule.Kind === "StringPropertyRule"
  ) {
    return "1 condition";
  }
  return "Configured";
}

function PromotionRow({ ruleSet, index, onEdit, onDelete }: PromotionRowProps) {
  const rule = (ruleSet.ruleJsonElement ?? null) as LoyaltyRule | null;
  const outcomesCount = Array.isArray(ruleSet.outcomesJsonElement) ? ruleSet.outcomesJsonElement.length : 0;

  return (
    <li className="rounded-md border border-border bg-card px-3 py-2.5">
      <div className="flex items-start justify-between gap-2">
        <div className="flex min-w-0 flex-1 items-start gap-2">
          <span className="mt-0.5 inline-flex h-5 w-5 shrink-0 items-center justify-center rounded bg-primary text-[11px] font-medium text-primary-foreground">
            {index + 1}
          </span>
          <div className="min-w-0 flex-1">
            <div className="truncate text-sm font-medium">{ruleSet.name ?? "Untitled promotion"}</div>
            <div className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-[11px] text-muted-foreground">
              <span>
                <span className="text-foreground">Condition:</span> {summarizeRule(rule)}
              </span>
              <span>
                <span className="text-foreground">Outcomes:</span> {outcomesCount}
              </span>
              {ruleSet.schemaId ? (
                <span className="truncate" title={ruleSet.schemaId}>
                  <span className="text-foreground">Schema:</span> {ruleSet.schemaId}
                </span>
              ) : null}
            </div>
          </div>
        </div>
        <div className="flex items-center gap-1">
          <Button type="button" variant="ghost" size="icon" onClick={onEdit} aria-label="Edit promotion">
            <Pencil className="h-3.5 w-3.5" />
          </Button>
          <Button type="button" variant="ghost" size="icon" onClick={onDelete} aria-label="Delete promotion">
            <Trash2 className="h-3.5 w-3.5" />
          </Button>
        </div>
      </div>
    </li>
  );
}

function NavigationDirectionCard({ direction, inner, onToggle, onEdit }: NavigationDirectionCardProps) {
  const enabled = inner !== null;
  const Icon = direction.icon;
  const outcomesCount = inner?.Outcomes?.length ?? 0;
  const summary = enabled ? summarizeConstraint(inner?.NavConstraint) : direction.description;

  return (
    <div
      className={cn(
        "rounded-md border bg-card text-card-foreground transition-colors",
        enabled ? "border-border" : "border-dashed border-border bg-muted/20"
      )}
    >
      <div className="flex items-start gap-3 p-3">
        <div className="relative">
          <div
            className={cn(
              "flex h-9 w-9 items-center justify-center rounded-md border",
              enabled ? direction.enabledIcon : "border-border bg-muted text-muted-foreground"
            )}
          >
            <Icon className="h-4 w-4" aria-hidden="true" />
          </div>
          <span
            aria-hidden="true"
            className={cn(
              "absolute -right-1 -top-1 h-2.5 w-2.5 rounded-full border-2 border-background",
              enabled ? direction.enabledDot : "bg-muted-foreground/40"
            )}
          />
        </div>
        <div className="flex-1">
          <div className="flex items-center justify-between gap-2">
            <span className={cn("text-sm font-medium", !enabled && "text-muted-foreground")}>{direction.label}</span>
            <div className="flex items-center gap-2">
              {enabled && (
                <Button type="button" variant="outline" size="sm" onClick={onEdit}>
                  <Pencil className="mr-1 h-3 w-3" />
                  Edit
                </Button>
              )}
              <Switch
                checked={enabled}
                onCheckedChange={onToggle}
                aria-label={`${enabled ? "Disable" : "Enable"} ${direction.label.toLowerCase()} criteria`}
              />
            </div>
          </div>
          <p className="mt-0.5 text-xs text-muted-foreground">{summary}</p>
          {enabled && (
            <div className="mt-2 flex items-center gap-3 text-[11px] text-muted-foreground">
              <span>
                <span className="text-foreground">{outcomesCount}</span> outcome{outcomesCount === 1 ? "" : "s"}
              </span>
              {inner?.Name && (
                <>
                  <span aria-hidden="true">•</span>
                  <span className="truncate" title={inner.Name}>
                    {inner.Name}
                  </span>
                </>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

export function NodeConfigPanel() {
  const form = useFormContext<CampaignWizardValues>();
  const journey = useWizardStore(selectJourney);
  const selectedNodeId = useWizardStore(selectSelectedNodeId);
  const selectedSchema = useWizardStore(selectSelectedSchema);
  const { patchJourneyNode, removeNode } = useWizardActions();
  const campaignEvents = useWatch({ control: form.control, name: "events", defaultValue: [] }) as string[];

  const schemasQuery = useQuery({
    queryKey: loyaltyKeys.schemas.all("loyalty"),
    queryFn: async () => {
      const result = await getAllSchemas();
      if (!result.success) throw new Error(result.error ?? "Failed to load schemas");
      return asLoyaltySchemas(result.data).filter(
        (schema: LoyaltySchema) => schema.tag === "eventable" || schema.name === "LoyaltyAccountDetails"
      );
    },
  });

  const [promotionOpen, setPromotionOpen] = useState(false);
  const [editingRuleSet, setEditingRuleSet] = useState<RuleSet | null>(null);
  const [navEditing, setNavEditing] = useState<NavigationType | null>(null);
  const [confirmDeleteOpen, setConfirmDeleteOpen] = useState(false);

  // Close all open modals whenever the selected node changes so a user can
  // never accidentally author / save data for the wrong node.
  // startTransition wraps all three setters so they batch into a single
  // non-urgent render and avoid the React Compiler "setState in effect" warning.
  const prevSelectedNodeRef = useRef(selectedNodeId);
  useEffect(() => {
    if (prevSelectedNodeRef.current === selectedNodeId) return;
    prevSelectedNodeRef.current = selectedNodeId;
    startTransition(() => {
      setNavEditing(null);
      setPromotionOpen(false);
      setEditingRuleSet(null);
    });
  }, [selectedNodeId]);

  const selectedNode = useMemo(
    () => (selectedNodeId ? findNode(journey, selectedNodeId) : null),
    [journey, selectedNodeId]
  );
  const availableRuleSchemas = useMemo(() => {
    const schemas = schemasQuery.data ?? [];
    if (campaignEvents.length === 0) {
      return selectedSchema && typeof selectedSchema.id === "string" ? [selectedSchema] : [];
    }
    return schemas.filter(
      (schema): schema is typeof schema & { id: string; name: string } =>
        typeof schema.id === "string" &&
        schema.id.length > 0 &&
        typeof schema.name === "string" &&
        campaignEvents.some((event) => isEventableSchemaNameOrId(schema, event))
    );
  }, [schemasQuery.data, campaignEvents, selectedSchema]);

  if (!selectedNode || !selectedNodeId) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Step settings</CardTitle>
          <CardDescription>Select a node on the canvas to edit it.</CardDescription>
        </CardHeader>
      </Card>
    );
  }

  const isRoot = selectedNodeId === journey.id;

  const handleSavePromotion = (ruleSet: RuleSet) => {
    const existing = selectedNode.rules ?? [];
    const idx = existing.findIndex((r) => r.id === ruleSet.id);
    const nextRules: RuleSet[] = idx >= 0 ? existing.map((r, i) => (i === idx ? ruleSet : r)) : [...existing, ruleSet];
    patchJourneyNode(selectedNodeId, { rules: nextRules });
    setEditingRuleSet(null);
  };

  const handleDeletePromotion = (ruleSetId: string | null | undefined) => {
    if (!ruleSetId) return;
    patchJourneyNode(selectedNodeId, {
      rules: (selectedNode.rules ?? []).filter((r) => r.id !== ruleSetId),
    });
  };

  const navigation = (selectedNode.navigation as Navigation | undefined) ?? {};
  const navigationGet = (dir: NavigationType): NavigationInner | null => {
    const value = (navigation as Record<NavigationType, NavigationInner | null | undefined>)[dir];
    return value ?? null;
  };

  const handleNavigationChange = (next: Navigation) => {
    patchJourneyNode(selectedNodeId, { navigation: next });
  };

  const handleSaveNavigation = (direction: NavigationType, inner: NavigationInner) => {
    handleNavigationChange({ ...navigation, [direction]: inner });
  };

  const handleToggleNavigation = (direction: NavigationType, on: boolean) => {
    if (on) {
      // Enable with an empty constraint; user can edit immediately.
      const inner: NavigationInner = {
        Name: "",
        NavigationType: direction,
        NavConstraint: null,
        Outcomes: null,
      };
      handleNavigationChange({ ...navigation, [direction]: inner });
    } else {
      const next = { ...navigation };
      delete (next as Record<NavigationType, unknown>)[direction];
      handleNavigationChange(next);
    }
  };

  const ruleSets = selectedNode.rules ?? [];

  return (
    <>
      <Card>
        <CardHeader>
          <CardTitle>{selectedNode.name ?? "Untitled step"}</CardTitle>
          <CardDescription>{isRoot ? "Campaign entry point" : "Journey step"}</CardDescription>
        </CardHeader>
        <CardContent>
          <Tabs defaultValue="promotions">
            <TabsList className="grid w-full grid-cols-3">
              <TabsTrigger value="promotions">Promotions</TabsTrigger>
              <TabsTrigger value="navigation">Navigation</TabsTrigger>
              <TabsTrigger value="settings">Settings</TabsTrigger>
            </TabsList>

            <TabsContent value="promotions" className="mt-4 space-y-3">
              <div className="flex items-center justify-between">
                <div>
                  <h4 className="text-sm font-semibold">Promotions</h4>
                  <p className="text-xs text-muted-foreground">Rules and outcomes that trigger on this step.</p>
                </div>
                <Button
                  type="button"
                  size="sm"
                  onClick={() => {
                    setEditingRuleSet(null);
                    setPromotionOpen(true);
                  }}
                >
                  <Plus className="mr-1.5 h-3.5 w-3.5" />
                  Add promotion
                </Button>
              </div>
              {ruleSets.length === 0 ? (
                <p className="text-xs text-muted-foreground">No promotions configured.</p>
              ) : (
                <ul className="space-y-2">
                  {ruleSets.map((rs, idx) => (
                    <PromotionRow
                      key={rs.id ?? rs.name ?? `${selectedNodeId}-rule-${idx}`}
                      ruleSet={rs}
                      index={idx}
                      onEdit={() => {
                        setEditingRuleSet(rs);
                        setPromotionOpen(true);
                      }}
                      onDelete={() => handleDeletePromotion(rs.id)}
                    />
                  ))}
                </ul>
              )}
            </TabsContent>

            <TabsContent value="navigation" className="mt-4 space-y-3">
              <div className="space-y-2">
                <h4 className="text-sm font-semibold">Navigation criteria</h4>
                <p className="text-xs text-muted-foreground">
                  Define how loyalty accounts enter, transition into, or exit this step. Toggle a direction to enable
                  it, then click Edit to configure its rule.
                </p>
              </div>
              <div className="space-y-2">
                {NAVIGATION_DIRECTIONS.map((dir) => (
                  <NavigationDirectionCard
                    key={dir.id}
                    direction={dir}
                    inner={navigationGet(dir.id)}
                    onToggle={(on) => handleToggleNavigation(dir.id, on)}
                    onEdit={() => setNavEditing(dir.id)}
                  />
                ))}
              </div>
            </TabsContent>

            <TabsContent value="settings" className="mt-4 space-y-4">
              <div className="space-y-2">
                <Label htmlFor="step-name">Step name</Label>
                <Input
                  id="step-name"
                  value={selectedNode.name ?? ""}
                  onChange={(e) => patchJourneyNode(selectedNodeId, { name: e.target.value })}
                />
              </div>
              {isRoot ? (
                <Alert>
                  <AlertDescription>The root entry step cannot be deleted.</AlertDescription>
                </Alert>
              ) : (
                <Button type="button" variant="destructive" size="sm" onClick={() => setConfirmDeleteOpen(true)}>
                  <Trash2 className="mr-1.5 h-3.5 w-3.5" />
                  Delete step
                </Button>
              )}
            </TabsContent>
          </Tabs>
        </CardContent>
      </Card>

      {promotionOpen && (
        <PromotionEditorModal
          open={promotionOpen}
          onOpenChange={(next) => {
            setPromotionOpen(next);
            if (!next) setEditingRuleSet(null);
          }}
          initial={editingRuleSet}
          onSave={handleSavePromotion}
          schema={selectedSchema}
          availableSchemas={availableRuleSchemas}
        />
      )}
      {navEditing && (
        <NavigationEditorModal
          key={`nav-${selectedNodeId ?? "none"}-${navEditing}`}
          open={Boolean(navEditing)}
          onOpenChange={(open) => !open && setNavEditing(null)}
          direction={navEditing}
          initial={navigationGet(navEditing)}
          schema={selectedSchema}
          onSave={(inner) => handleSaveNavigation(navEditing, inner)}
        />
      )}

      <AlertDialog open={confirmDeleteOpen} onOpenChange={setConfirmDeleteOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete journey step?</AlertDialogTitle>
            <AlertDialogDescription>
              This removes the step from the journey. Its child steps will be reconnected to the parent so they are not
              lost. The change is not saved until you submit the wizard.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                removeNode(selectedNodeId);
                setConfirmDeleteOpen(false);
              }}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
