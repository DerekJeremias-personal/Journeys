"use client";

/**
 * OutcomeEditorModal — add or edit a single outcome (Deposit / Spend /
 * Expiration / Tag) for a journey node's promotion. Saves the outcome via
 * `onSave`; the parent Promotion editor merges it into the wizard journey
 * store.
 *
 * Out-of-scope (server-side stubs per `loyalty-rules.ts`):
 *   - NotificationOutcome
 *   - WorkflowOutcome
 *   - RuleStateOutcome
 */

import { useEffect, useMemo, useRef } from "react";
import { useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQuery } from "@tanstack/react-query";
import { z } from "zod";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Form, FormControl, FormDescription, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";
import { Input } from "@/components/ui/input";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Skeleton } from "@/components/ui/skeleton";

import { getPointAccountTypes } from "@/services/loyalty/actions";
import { loyaltyKeys } from "@/services/loyalty/query-keys";
import { asPointAccountTypes, type Outcome } from "@/lib/campaign-types";

// ─────────────────────────────────────────────────────────────────────────────
// Form schema
// ─────────────────────────────────────────────────────────────────────────────

export type OutcomeFormKind = "Deposit" | "Spend" | "Expiration" | "Tag";

const outcomeSchema = z
  .object({
    kind: z.enum(["Deposit", "Spend", "Expiration", "Tag"]),
    eventId: z.string().min(1, "Event id is required"),
    eventType: z.string().min(1, "Event type is required"),
    pointAccountTypeId: z.string().optional(),
    pointsPerDollar: z.number().nonnegative().optional(),
    pointsPerUnit: z.number().nonnegative().optional(),
    withdrawalAmount: z.number().nonnegative().optional(),
    expirationAmount: z.number().nonnegative().optional(),
    expirationPercent: z.number().min(0).max(100).optional(),
    daysUntilExpiration: z.number().int().nonnegative().optional(),
    tagType: z.string().optional(),
    tagName: z.string().optional(),
    tagValue: z.string().optional(),
    tagEffectiveStartDate: z.string().optional(),
    tagEffectiveEndDate: z.string().optional(),
    tagTtlSec: z.number().int().nonnegative().optional(),
  })
  .superRefine((value, ctx) => {
    if (value.kind === "Deposit") {
      if (!value.pointAccountTypeId) {
        ctx.addIssue({ code: "custom", path: ["pointAccountTypeId"], message: "Select a point account type" });
      }
      if (value.pointsPerDollar === undefined) {
        ctx.addIssue({ code: "custom", path: ["pointsPerDollar"], message: "Points per dollar is required" });
      }
    }
    if (value.kind === "Spend") {
      if (!value.pointAccountTypeId) {
        ctx.addIssue({ code: "custom", path: ["pointAccountTypeId"], message: "Select a point account type" });
      }
      if (value.withdrawalAmount === undefined) {
        ctx.addIssue({ code: "custom", path: ["withdrawalAmount"], message: "Withdrawal amount is required" });
      }
    }
    if (value.kind === "Expiration") {
      if (!value.pointAccountTypeId) {
        ctx.addIssue({ code: "custom", path: ["pointAccountTypeId"], message: "Select a point account type" });
      }
      if (value.expirationAmount === undefined && value.expirationPercent === undefined) {
        ctx.addIssue({
          code: "custom",
          path: ["expirationAmount"],
          message: "Provide either an amount or percent to expire",
        });
      }
    }
    if (value.kind === "Tag") {
      if (!value.tagType) ctx.addIssue({ code: "custom", path: ["tagType"], message: "Tag type is required" });
      if (!value.tagName) ctx.addIssue({ code: "custom", path: ["tagName"], message: "Tag name is required" });
      if (!value.tagValue) ctx.addIssue({ code: "custom", path: ["tagValue"], message: "Tag value is required" });
    }
  });

export type OutcomeFormValues = z.infer<typeof outcomeSchema>;

const DEFAULTS: OutcomeFormValues = {
  kind: "Deposit",
  eventId: "",
  eventType: "",
  pointAccountTypeId: "",
  pointsPerDollar: 1,
};

export interface OutcomeEditorModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  initial?: OutcomeFormValues;
  initialOutcome?: Outcome | null;
  onSave: (outcome: Outcome) => void;
}

// ─────────────────────────────────────────────────────────────────────────────
// Form → Outcome wire-shape mapping
// ─────────────────────────────────────────────────────────────────────────────

function isoFromDateInput(value: string | undefined): string | null {
  if (!value) return null;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return null;
  return date.toISOString();
}

function dateInputFromIso(value: string | null | undefined): string | undefined {
  if (!value) return undefined;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return undefined;
  return date.toISOString().slice(0, 10);
}

function parseConstantNumber(provider: unknown): number | undefined {
  if (!provider || typeof provider !== "object") return undefined;
  const value = (provider as { Value?: unknown }).Value;
  return typeof value === "number" ? value : undefined;
}

export function outcomeToFormValues(outcome: Outcome): OutcomeFormValues {
  if (outcome.Kind === "DepositPointsOutcome") {
    return {
      kind: "Deposit",
      eventId: outcome.EventId ?? "",
      eventType: outcome.EventType ?? "",
      pointAccountTypeId: outcome.AffectedPointAccountTypeIds?.[0] ?? "",
      pointsPerDollar: outcome.PointsPerDollar,
    };
  }

  if (outcome.Kind === "SpendPointsOutcome") {
    return {
      kind: "Spend",
      eventId: outcome.EventId ?? "",
      eventType: outcome.EventType ?? "",
      pointAccountTypeId: outcome.AffectedPointAccountTypeIds?.[0] ?? "",
      withdrawalAmount: parseConstantNumber(outcome.WithdrawlAmountProvider),
    };
  }

  if (outcome.Kind === "ExpirePointsOutcome") {
    return {
      kind: "Expiration",
      eventId: outcome.EventId ?? "",
      eventType: outcome.EventType ?? "",
      pointAccountTypeId: outcome.AffectedPointAccountTypeIds?.[0] ?? "",
      expirationAmount: outcome.ExpirationAmount ?? undefined,
      expirationPercent: outcome.ExpirationPercent ?? undefined,
    };
  }

  if (outcome.Kind === "TagOutcome") {
    return {
      kind: "Tag",
      eventId: outcome.EventId ?? "",
      eventType: outcome.EventType ?? "",
      tagType: outcome.Type ?? "",
      tagName: outcome.Name ?? "",
      tagValue: outcome.Value ?? "",
      tagEffectiveStartDate: dateInputFromIso(outcome.EffectiveStartDate),
      tagEffectiveEndDate: dateInputFromIso(outcome.EffectiveEndDate),
      tagTtlSec: outcome.TtlSec ?? undefined,
    };
  }

  return DEFAULTS;
}

function formToOutcome(values: OutcomeFormValues, existingId?: string): Outcome {
  const baseId = existingId ?? crypto.randomUUID();
  const eventBase = {
    Id: baseId,
    EventId: values.eventId,
    EventType: values.eventType,
  } as const;

  if (values.kind === "Deposit") {
    return {
      ...eventBase,
      Kind: "DepositPointsOutcome",
      PointSourceAccountId: null,
      PointsPerDollar: values.pointsPerDollar ?? 0,
      AffectedPointAccountTypeIds: values.pointAccountTypeId ? [values.pointAccountTypeId] : [],
    };
  }

  if (values.kind === "Spend") {
    return {
      ...eventBase,
      Kind: "SpendPointsOutcome",
      PointSourceAccountId: null,
      LedgerTypeId: null,
      WithdrawlAmountProvider: {
        Kind: "ConstantValueProvider",
        $type: "ConstantValueProvider",
        Value: values.withdrawalAmount ?? 0,
      },
      AffectedPointAccountTypeIds: values.pointAccountTypeId ? [values.pointAccountTypeId] : [],
    };
  }

  if (values.kind === "Expiration") {
    return {
      ...eventBase,
      Kind: "ExpirePointsOutcome",
      ExpirationAmount: values.expirationAmount ?? null,
      ExpirationPercent: values.expirationPercent ?? null,
      AffectedPointAccountTypeIds: values.pointAccountTypeId ? [values.pointAccountTypeId] : [],
    };
  }

  return {
    ...eventBase,
    Kind: "TagOutcome",
    Type: values.tagType ?? "",
    EntityId: "",
    Name: values.tagName ?? "",
    Value: values.tagValue ?? "",
    EffectiveStartDate: isoFromDateInput(values.tagEffectiveStartDate),
    EffectiveEndDate: isoFromDateInput(values.tagEffectiveEndDate),
    TtlSec: values.tagTtlSec ?? null,
  };
}

// ─────────────────────────────────────────────────────────────────────────────
// Component
// ─────────────────────────────────────────────────────────────────────────────

export function OutcomeEditorModal({ open, onOpenChange, initial, initialOutcome, onSave }: OutcomeEditorModalProps) {
  const initialValues = useMemo<OutcomeFormValues>(
    () => initial ?? (initialOutcome ? outcomeToFormValues(initialOutcome) : DEFAULTS),
    [initial, initialOutcome]
  );

  const form = useForm<OutcomeFormValues>({
    resolver: zodResolver(outcomeSchema),
    defaultValues: initialValues,
  });
  const wasOpenRef = useRef(false);

  // Re-seed form values only when the dialog transitions from closed to open.
  useEffect(() => {
    if (open && !wasOpenRef.current) {
      form.reset(initialValues);
    }
    wasOpenRef.current = open;
  }, [open, initialValues, form]);

  const handleOpenChange = (next: boolean) => {
    onOpenChange(next);
  };

  const patsQuery = useQuery({
    queryKey: loyaltyKeys.pointAccountTypes.all,
    queryFn: async () => {
      const result = await getPointAccountTypes();
      if (!result.success) throw new Error(result.error ?? "Failed to load point account types");
      return asPointAccountTypes(result.data);
    },
    enabled: open,
  });

  const kind = useWatch({ control: form.control, name: "kind" }) ?? DEFAULTS.kind;

  const handleSubmit = form.handleSubmit((values) => {
    onSave(formToOutcome(values, initialOutcome?.Id));
    onOpenChange(false);
  });

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="max-h-[90vh] max-w-3xl overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{initial ? "Edit outcome" : "Add outcome"}</DialogTitle>
          <DialogDescription>Configure how this rule rewards or affects the loyalty account.</DialogDescription>
        </DialogHeader>

        <Form {...form}>
          <form onSubmit={handleSubmit} className="space-y-4">
            <Alert>
              <AlertDescription>
                Editor currently provides first-class forms for Deposit, Spend, Expiration, and Tag outcomes. Other
                outcome kinds are preserved read-only when present on existing campaigns.
              </AlertDescription>
            </Alert>
            <Tabs value={kind} onValueChange={(v) => form.setValue("kind", v as OutcomeFormKind)}>
              <TabsList className="grid w-full grid-cols-4">
                <TabsTrigger value="Deposit">Deposit</TabsTrigger>
                <TabsTrigger value="Spend">Spend</TabsTrigger>
                <TabsTrigger value="Expiration">Expiration</TabsTrigger>
                <TabsTrigger value="Tag">Tag</TabsTrigger>
              </TabsList>

              <div className="mt-4 grid grid-cols-1 gap-4 md:grid-cols-2">
                <FormField
                  control={form.control}
                  name="eventId"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Event id</FormLabel>
                      <FormControl>
                        <Input placeholder="purchase" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="eventType"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Event type</FormLabel>
                      <FormControl>
                        <Input placeholder="Purchase" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              </div>

              <TabsContent value="Deposit" className="mt-4 space-y-4">
                <PointAccountField form={form} patsQuery={patsQuery} />
                <FormField
                  control={form.control}
                  name="pointsPerDollar"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Points per dollar</FormLabel>
                      <FormControl>
                        <Input
                          type="number"
                          min={0}
                          step={0.01}
                          value={field.value ?? ""}
                          onChange={(e) => field.onChange(e.target.value === "" ? undefined : Number(e.target.value))}
                        />
                      </FormControl>
                      <FormDescription>Multiplier applied to the qualifying dollar amount.</FormDescription>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              </TabsContent>

              <TabsContent value="Spend" className="mt-4 space-y-4">
                <PointAccountField form={form} patsQuery={patsQuery} />
                <FormField
                  control={form.control}
                  name="withdrawalAmount"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Withdrawal amount</FormLabel>
                      <FormControl>
                        <Input
                          type="number"
                          min={0}
                          step={1}
                          value={field.value ?? ""}
                          onChange={(e) => field.onChange(e.target.value === "" ? undefined : Number(e.target.value))}
                        />
                      </FormControl>
                      <FormDescription>Number of points to withdraw from the chosen account.</FormDescription>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              </TabsContent>

              <TabsContent value="Expiration" className="mt-4 space-y-4">
                <PointAccountField form={form} patsQuery={patsQuery} />
                <div className="grid grid-cols-2 gap-4">
                  <FormField
                    control={form.control}
                    name="expirationAmount"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Expiration amount</FormLabel>
                        <FormControl>
                          <Input
                            type="number"
                            min={0}
                            value={field.value ?? ""}
                            onChange={(e) => field.onChange(e.target.value === "" ? undefined : Number(e.target.value))}
                          />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                  <FormField
                    control={form.control}
                    name="expirationPercent"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Expiration percent</FormLabel>
                        <FormControl>
                          <Input
                            type="number"
                            min={0}
                            max={100}
                            value={field.value ?? ""}
                            onChange={(e) => field.onChange(e.target.value === "" ? undefined : Number(e.target.value))}
                          />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                </div>
              </TabsContent>

              <TabsContent value="Tag" className="mt-4 space-y-4">
                <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
                  <FormField
                    control={form.control}
                    name="tagType"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Tag type</FormLabel>
                        <FormControl>
                          <Input placeholder="StatusTier" {...field} value={field.value ?? ""} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                  <FormField
                    control={form.control}
                    name="tagName"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Tag name</FormLabel>
                        <FormControl>
                          <Input placeholder="tier" {...field} value={field.value ?? ""} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                  <FormField
                    control={form.control}
                    name="tagValue"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Tag value</FormLabel>
                        <FormControl>
                          <Input placeholder="Gold" {...field} value={field.value ?? ""} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                </div>
                <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
                  <FormField
                    control={form.control}
                    name="tagEffectiveStartDate"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Effective start (optional)</FormLabel>
                        <FormControl>
                          <Input type="date" {...field} value={field.value ?? ""} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                  <FormField
                    control={form.control}
                    name="tagEffectiveEndDate"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Effective end (optional)</FormLabel>
                        <FormControl>
                          <Input type="date" {...field} value={field.value ?? ""} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                  <FormField
                    control={form.control}
                    name="tagTtlSec"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>TTL (seconds, optional)</FormLabel>
                        <FormControl>
                          <Input
                            type="number"
                            min={0}
                            step={1}
                            value={field.value ?? ""}
                            onChange={(e) => field.onChange(e.target.value === "" ? undefined : Number(e.target.value))}
                          />
                        </FormControl>
                        <FormDescription>Auto-expire the tag after this many seconds.</FormDescription>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                </div>
              </TabsContent>
            </Tabs>

            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
                Cancel
              </Button>
              <Button type="submit">Save outcome</Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Reusable PointAccountType select used by Deposit / Spend / Expiration tabs
// ─────────────────────────────────────────────────────────────────────────────

interface PointAccountFieldProps {
  form: ReturnType<typeof useForm<OutcomeFormValues>>;
  patsQuery: ReturnType<typeof useQuery<Array<{ id?: string | null; name: string }>, Error>>;
}

function PointAccountField({ form, patsQuery }: PointAccountFieldProps) {
  if (patsQuery.isLoading) return <Skeleton className="h-10 w-full" />;
  if (patsQuery.error) {
    return (
      <Alert variant="destructive">
        <AlertDescription>{patsQuery.error.message}</AlertDescription>
      </Alert>
    );
  }
  return (
    <FormField
      control={form.control}
      name="pointAccountTypeId"
      render={({ field }) => (
        <FormItem>
          <FormLabel>Point account type</FormLabel>
          <Select onValueChange={field.onChange} value={field.value ?? ""}>
            <FormControl>
              <SelectTrigger>
                <SelectValue placeholder="Select a point account type" />
              </SelectTrigger>
            </FormControl>
            <SelectContent>
              {(patsQuery.data ?? []).filter(
                (p): p is { id: string; name: string } => typeof p.id === "string" && p.id.length > 0
              ).length === 0 ? (
                <div className="px-2 py-1.5 text-xs text-muted-foreground">No point account types available</div>
              ) : (
                (patsQuery.data ?? [])
                  .filter((p): p is { id: string; name: string } => typeof p.id === "string" && p.id.length > 0)
                  .map((p) => (
                    <SelectItem key={p.id} value={p.id}>
                      {p.name}
                    </SelectItem>
                  ))
              )}
            </SelectContent>
          </Select>
          <FormMessage />
        </FormItem>
      )}
    />
  );
}
