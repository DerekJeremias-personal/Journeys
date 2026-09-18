"use client";

/**
 * CampaignDetailsStep — first step of the wizard. Edits the scalar campaign
 * fields (name, ext id, status, dates, events) using the parent RHF instance.
 * Event types are picked via a Combobox-style multi-select powered by the
 * shadcn `<Command>` primitive, sourced from `getAllSchemas`.
 */

import { useEffect, useMemo, useState } from "react";
import { useFieldArray, useFormContext, useWatch } from "react-hook-form";
import { useQuery } from "@tanstack/react-query";
import { AlertTriangle, Check, ChevronsUpDown, X } from "lucide-react";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from "@/components/ui/command";
import { Form, FormControl, FormDescription, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";
import { Input } from "@/components/ui/input";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";

import { getAllSchemas } from "@/services/loyalty/actions";
import { loyaltyKeys } from "@/services/loyalty/query-keys";
import { asLoyaltySchemas, type LoyaltySchema, type LoyaltySegment } from "@/lib/campaign-types";

import { selectSelectedSchema, useWizardActions, useWizardStore } from "../wizard-store";
import type { CampaignWizardValues } from "../campaign-wizard";

function isEventableSchema(s: LoyaltySchema): boolean {
  return s.tag === "eventable" || s.name === "LoyaltyAccountDetails";
}

/** Turn "Summer Bonus 2025!" into "summer-bonus-2025". */
function slugify(input: string): string {
  return input
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-|-$/g, "");
}

const STATUS_BADGE_VARIANTS: Record<string, { label: string; className: string }> = {
  draft: { label: "Draft", className: "bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-400" },
  live: { label: "Live", className: "bg-emerald-100 text-emerald-800 dark:bg-emerald-900/30 dark:text-emerald-400" },
  pause: { label: "Pause", className: "bg-slate-100 text-slate-800 dark:bg-slate-900/30 dark:text-slate-300" },
  archive: { label: "Archive", className: "bg-zinc-100 text-zinc-800 dark:bg-zinc-900/30 dark:text-zinc-300" },
};

/**
 * Resolve a saved `events[]` entry (id OR name) back to a `LoyaltySchema`.
 * Legacy data may store either form; match case-insensitively on both.
 */
function findSchemaForEvent(schemas: LoyaltySchema[], event: string): LoyaltySchema | null {
  const lowered = event.toLowerCase();
  return schemas.find((s) => s.id.toLowerCase() === lowered || s.name.toLowerCase() === lowered) ?? null;
}

function createNewSegmentDraft(): LoyaltySegment {
  return {
    id: null,
    tenantId: null,
    etag: null,
    extSegmentId: "",
    name: "",
    status: "Active",
    type: "File",
    targetFolder: null,
    targetFile: null,
    fileCreateDate: null,
    source: null,
    schedule: null,
  };
}

function createDefaultSegmentSource(): NonNullable<LoyaltySegment["source"]> {
  return {
    name: "Segment source",
    status: "Active",
    type: "File",
    sourceFolder: null,
    sourceFile: null,
    apiUrl: null,
    query: null,
    lastRunDate: null,
    lastRunDuration: null,
  };
}

function createDefaultSegmentSchedule(): NonNullable<LoyaltySegment["schedule"]> {
  return {
    id: null,
    tenantId: null,
    etag: null,
    name: "Segment schedule",
    status: "Active",
    type: "Recurring",
    startDate: new Date().toISOString(),
    endDate: null,
    frequency: null,
    frequencyUnit: null,
    lastRunDate: null,
    lastRunDuration: null,
    nextRunDate: null,
  };
}

export function CampaignDetailsStep({
  lockExtCampaignId = false,
  lockLiveStatus = false,
}: {
  lockExtCampaignId?: boolean;
  lockLiveStatus?: boolean;
}) {
  const form = useFormContext<CampaignWizardValues>();
  const [eventsOpen, setEventsOpen] = useState(false);
  const { setSelectedSchema } = useWizardActions();
  const selectedSchema = useWizardStore(selectSelectedSchema);
  const segmentsFieldArray = useFieldArray({
    control: form.control,
    name: "segments",
  });

  const schemasQuery = useQuery({
    queryKey: loyaltyKeys.schemas.all("loyalty"),
    queryFn: async () => {
      const result = await getAllSchemas();
      if (!result.success) throw new Error(result.error ?? "Failed to load event schemas");
      return asLoyaltySchemas(result.data).filter(isEventableSchema);
    },
  });

  const events = useWatch({ control: form.control, name: "events", defaultValue: [] }) as string[];
  const eventLabels = useMemo(() => {
    const map = new Map<string, string>();
    for (const schema of schemasQuery.data ?? []) {
      map.set(schema.id, schema.name);
      map.set(schema.name, schema.name);
    }
    return map;
  }, [schemasQuery.data]);

  // Identify saved events that no longer resolve to a known schema (legacy
  // data, deleted schemas) so the user can spot + remove them.
  const unmatchedEvents = useMemo(() => {
    const schemas = schemasQuery.data ?? [];
    return events.filter((e) => !findSchemaForEvent(schemas, e));
  }, [events, schemasQuery.data]);

  // Hydrate `selectedSchema` once schemas load AND there's at least one chosen
  // event but no schema cached yet (covers both create-with-template and
  // edit-existing-campaign flows). We pick the *first* resolvable event.
  useEffect(() => {
    const schemas = schemasQuery.data;
    if (!schemas || selectedSchema || events.length === 0) return;
    for (const event of events) {
      const schema = findSchemaForEvent(schemas, event);
      if (schema) {
        setSelectedSchema(schema);
        return;
      }
    }
  }, [schemasQuery.data, events, selectedSchema, setSelectedSchema]);

  const toggleEvent = (schemaId: string) => {
    const next = events.includes(schemaId) ? events.filter((e) => e !== schemaId) : [...events, schemaId];
    form.setValue("events", next, { shouldDirty: true, shouldValidate: true });
    // Cache the picked schema so the rule + navigation editors can render
    // payload-property suggestions. Selecting an additional event keeps the
    // already-cached schema (matches legacy behaviour); deselecting the cached
    // one falls back to whichever event remains, or null if events is empty.
    const schemas = schemasQuery.data ?? [];
    const wasAlreadySelected = events.includes(schemaId);
    if (!wasAlreadySelected) {
      const schema = schemas.find((s) => s.id === schemaId);
      if (schema && !selectedSchema) setSelectedSchema(schema);
    } else if (selectedSchema && (selectedSchema.id === schemaId || selectedSchema.name === schemaId)) {
      const fallback = next.map((e) => findSchemaForEvent(schemas, e)).find((s) => s !== null) ?? null;
      setSelectedSchema(fallback);
    }
  };

  const removeUnmatched = (eventId: string) => {
    form.setValue(
      "events",
      events.filter((e) => e !== eventId),
      { shouldDirty: true, shouldValidate: true }
    );
  };

  // Live preview values (watched so the right-column card updates as the user types).
  const previewName = form.watch("name") ?? "";
  const previewStatus = form.watch("status") ?? "draft";
  const previewStartDate = form.watch("startDate") ?? "";
  const previewEndDate = form.watch("endDate");
  const statusMeta = STATUS_BADGE_VARIANTS[previewStatus] ?? STATUS_BADGE_VARIANTS["draft"]!;

  return (
    <Form {...form}>
      <div className="grid gap-6 lg:grid-cols-2">
        <FormField
          control={form.control}
          name="name"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Campaign name</FormLabel>
              <FormControl>
                <Input
                  placeholder="Summer points promotion"
                  {...field}
                  value={field.value ?? ""}
                  onChange={(e) => {
                    const next = e.target.value;
                    field.onChange(next);
                    // Auto-generate extCampaignId only when the user hasn't set
                    // one OR when it's still tracking a previous slug of the
                    // name. Avoids clobbering a manually-customised slug.
                    // Live new-draft pins the resolved ext — never slugify over it.
                    if (lockExtCampaignId) return;
                    const currentExt = form.getValues("extCampaignId") ?? "";
                    const previousAuto = slugify(field.value ?? "");
                    if (!currentExt || currentExt === previousAuto) {
                      form.setValue("extCampaignId", slugify(next), { shouldDirty: true });
                    }
                  }}
                />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />

        <FormField
          control={form.control}
          name="extCampaignId"
          render={({ field }) => (
            <FormItem>
              <FormLabel>External campaign id</FormLabel>
              <FormControl>
                <Input
                  placeholder="summer-points-2025"
                  {...field}
                  value={field.value ?? ""}
                  readOnly={lockExtCampaignId}
                  disabled={lockExtCampaignId}
                />
              </FormControl>
              <FormDescription>
                {lockExtCampaignId
                  ? "Pinned to the live program identity. Saving creates a new draft with this same id."
                  : "Auto-generated from the name. Override if upstream needs a custom slug."}
              </FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />

        <FormField
          control={form.control}
          name="status"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Status</FormLabel>
              <Select onValueChange={field.onChange} value={field.value}>
                <FormControl>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                </FormControl>
                <SelectContent>
                  {!lockLiveStatus && <SelectItem value="draft">Draft</SelectItem>}
                  <SelectItem value="live">Live</SelectItem>
                  <SelectItem value="pause">Pause</SelectItem>
                  {!lockLiveStatus && <SelectItem value="archive">Archive</SelectItem>}
                </SelectContent>
              </Select>
              {lockLiveStatus ? (
                <FormDescription>Unpublish is Pause. Live cannot become Draft or Archive on this same id.</FormDescription>
              ) : null}
              <FormMessage />
            </FormItem>
          )}
        />

        <Card className="hidden border-dashed lg:block">
          <CardHeader className="py-3">
            <CardTitle className="text-xs uppercase tracking-wider text-muted-foreground">Campaign preview</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            <div className="flex items-center gap-2">
              <span
                className={cn(
                  "inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium",
                  statusMeta.className
                )}
              >
                {statusMeta.label}
              </span>
              <span className="truncate text-sm font-semibold">{previewName.trim() || "Untitled campaign"}</span>
            </div>
            <p className="text-xs text-muted-foreground">
              {previewStartDate ? (
                <>
                  Starts {new Date(previewStartDate).toLocaleDateString()}
                  {previewEndDate ? <> · ends {new Date(previewEndDate).toLocaleDateString()}</> : <> · no end date</>}
                </>
              ) : (
                "Pick a start date to schedule this campaign."
              )}
            </p>
          </CardContent>
        </Card>

        <FormField
          control={form.control}
          name="startDate"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Start date</FormLabel>
              <FormControl>
                <Input
                  type="date"
                  value={field.value ? field.value.slice(0, 10) : ""}
                  onChange={(e) => field.onChange(e.target.value ? new Date(e.target.value).toISOString() : "")}
                />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />

        <FormField
          control={form.control}
          name="endDate"
          render={({ field }) => (
            <FormItem>
              <FormLabel>End date (optional)</FormLabel>
              <FormControl>
                <Input
                  type="date"
                  value={field.value ? field.value.slice(0, 10) : ""}
                  onChange={(e) => field.onChange(e.target.value ? new Date(e.target.value).toISOString() : null)}
                />
              </FormControl>
              <FormDescription>Leave blank for ongoing campaigns.</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />
      </div>

      <FormField
        control={form.control}
        name="events"
        render={() => (
          <FormItem className="mt-6">
            <FormLabel>Event types</FormLabel>
            {schemasQuery.isLoading ? (
              <Skeleton className="h-10 w-full" />
            ) : schemasQuery.error ? (
              <Alert variant="destructive">
                <AlertDescription>{(schemasQuery.error as Error).message}</AlertDescription>
              </Alert>
            ) : (
              <Popover open={eventsOpen} onOpenChange={setEventsOpen}>
                <PopoverTrigger asChild>
                  <Button type="button" variant="outline" className="w-full justify-between">
                    <span className="truncate text-left">
                      {events.length === 0 ? "Pick event types…" : `${events.length} selected`}
                    </span>
                    <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                  </Button>
                </PopoverTrigger>
                <PopoverContent className="w-[--radix-popover-trigger-width] p-0" align="start">
                  <Command>
                    <CommandInput placeholder="Search events…" />
                    <CommandList>
                      <CommandEmpty>No events found.</CommandEmpty>
                      <CommandGroup>
                        {(schemasQuery.data ?? []).map((schema) => {
                          const selected = events.includes(schema.id);
                          return (
                            <CommandItem
                              key={schema.id}
                              value={`${schema.name} ${schema.id}`}
                              onSelect={() => toggleEvent(schema.id)}
                            >
                              <Check className={cn("mr-2 h-4 w-4", selected ? "opacity-100" : "opacity-0")} />
                              <span className="flex-1">{schema.name}</span>
                              <span className="text-xs text-muted-foreground">{schema.modelType}</span>
                            </CommandItem>
                          );
                        })}
                      </CommandGroup>
                    </CommandList>
                  </Command>
                </PopoverContent>
              </Popover>
            )}
            <FormDescription>Pick which event schemas this campaign reacts to.</FormDescription>
            {events.length > 0 && (
              <div className="mt-2 flex flex-wrap gap-1.5">
                {events.map((eventId) => (
                  <Badge key={eventId} variant="secondary" className="gap-1">
                    {eventLabels.get(eventId) ?? eventId}
                    <button
                      type="button"
                      className="rounded-sm hover:bg-muted"
                      onClick={() => toggleEvent(eventId)}
                      aria-label="Remove event"
                    >
                      <X className="h-3 w-3" />
                    </button>
                  </Badge>
                ))}
              </div>
            )}
            <FormMessage />
          </FormItem>
        )}
      />

      <div className="mt-6 space-y-3">
        <div className="flex items-center justify-between">
          <div>
            <h3 className="text-sm font-medium">Segments</h3>
            <p className="text-xs text-muted-foreground">
              Campaign segments are included in save payloads. Add or edit audience segment metadata.
            </p>
          </div>
          <Button type="button" variant="outline" onClick={() => segmentsFieldArray.append(createNewSegmentDraft())}>
            Add segment
          </Button>
        </div>
        {segmentsFieldArray.fields.length === 0 ? (
          <Card>
            <CardContent className="py-6 text-sm text-muted-foreground">
              No segments configured. This campaign will run without segment constraints.
            </CardContent>
          </Card>
        ) : (
          <div className="space-y-3">
            {segmentsFieldArray.fields.map((segment, index) => (
              <Card key={segment.id ?? `segment-${index}`}>
                <CardContent className="space-y-3 pt-6">
                  <div className="grid gap-3 md:grid-cols-2">
                    <FormItem>
                      <FormLabel>Segment name</FormLabel>
                      <FormControl>
                        <Input
                          defaultValue={segment.name ?? ""}
                          {...form.register(`segments.${index}.name` as const)}
                          placeholder="VIP shoppers"
                        />
                      </FormControl>
                    </FormItem>
                    <FormItem>
                      <FormLabel>External segment id</FormLabel>
                      <FormControl>
                        <Input
                          defaultValue={segment.extSegmentId ?? ""}
                          {...form.register(`segments.${index}.extSegmentId` as const)}
                          placeholder="vip-shoppers"
                        />
                      </FormControl>
                    </FormItem>
                    <FormItem>
                      <FormLabel>Status</FormLabel>
                      <FormControl>
                        <Input
                          defaultValue={segment.status ?? ""}
                          {...form.register(`segments.${index}.status` as const)}
                          placeholder="Active"
                        />
                      </FormControl>
                    </FormItem>
                    <FormItem>
                      <FormLabel>Type</FormLabel>
                      <FormControl>
                        <Input
                          defaultValue={segment.type ?? ""}
                          {...form.register(`segments.${index}.type` as const)}
                          placeholder="File"
                        />
                      </FormControl>
                    </FormItem>
                    <FormItem>
                      <FormLabel>Target folder (optional)</FormLabel>
                      <FormControl>
                        <Input
                          defaultValue={segment.targetFolder ?? ""}
                          {...form.register(`segments.${index}.targetFolder` as const)}
                          placeholder="/segments/loyalty"
                        />
                      </FormControl>
                    </FormItem>
                    <FormItem>
                      <FormLabel>Target file (optional)</FormLabel>
                      <FormControl>
                        <Input
                          defaultValue={segment.targetFile ?? ""}
                          {...form.register(`segments.${index}.targetFile` as const)}
                          placeholder="vip.csv"
                        />
                      </FormControl>
                    </FormItem>
                  </div>
                  <div className="flex justify-end">
                    <Button type="button" variant="ghost" onClick={() => segmentsFieldArray.remove(index)}>
                      Remove segment
                    </Button>
                  </div>

                  <details className="rounded-md border border-border p-3">
                    <summary className="cursor-pointer text-sm font-medium">Advanced source + schedule</summary>
                    <div className="mt-3 space-y-4">
                      <div className="space-y-2">
                        <div className="flex items-center justify-between">
                          <h4 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                            Source configuration
                          </h4>
                          {form.watch(`segments.${index}.source` as const) ? (
                            <Button
                              type="button"
                              size="sm"
                              variant="ghost"
                              onClick={() =>
                                form.setValue(`segments.${index}.source` as const, null, { shouldDirty: true })
                              }
                            >
                              Remove source
                            </Button>
                          ) : (
                            <Button
                              type="button"
                              size="sm"
                              variant="outline"
                              onClick={() =>
                                form.setValue(`segments.${index}.source` as const, createDefaultSegmentSource(), {
                                  shouldDirty: true,
                                })
                              }
                            >
                              Add source
                            </Button>
                          )}
                        </div>
                        {form.watch(`segments.${index}.source` as const) && (
                          <div className="grid gap-3 md:grid-cols-2">
                            <FormItem>
                              <FormLabel>Source name</FormLabel>
                              <FormControl>
                                <Input
                                  {...form.register(`segments.${index}.source.name` as const)}
                                  placeholder="VIP feed"
                                />
                              </FormControl>
                            </FormItem>
                            <FormItem>
                              <FormLabel>Source status</FormLabel>
                              <FormControl>
                                <Input
                                  {...form.register(`segments.${index}.source.status` as const)}
                                  placeholder="Active"
                                />
                              </FormControl>
                            </FormItem>
                            <FormItem>
                              <FormLabel>Source type</FormLabel>
                              <FormControl>
                                <Input
                                  {...form.register(`segments.${index}.source.type` as const)}
                                  placeholder="File"
                                />
                              </FormControl>
                            </FormItem>
                            <FormItem>
                              <FormLabel>Source folder</FormLabel>
                              <FormControl>
                                <Input
                                  {...form.register(`segments.${index}.source.sourceFolder` as const)}
                                  placeholder="/imports"
                                />
                              </FormControl>
                            </FormItem>
                            <FormItem>
                              <FormLabel>Source file</FormLabel>
                              <FormControl>
                                <Input
                                  {...form.register(`segments.${index}.source.sourceFile` as const)}
                                  placeholder="vip.csv"
                                />
                              </FormControl>
                            </FormItem>
                            <FormItem>
                              <FormLabel>API URL</FormLabel>
                              <FormControl>
                                <Input
                                  {...form.register(`segments.${index}.source.apiUrl` as const)}
                                  placeholder="https://api.example.com/segments"
                                />
                              </FormControl>
                            </FormItem>
                            <FormItem className="md:col-span-2">
                              <FormLabel>Source query</FormLabel>
                              <FormControl>
                                <Input
                                  {...form.register(`segments.${index}.source.query` as const)}
                                  placeholder="SELECT * FROM..."
                                />
                              </FormControl>
                            </FormItem>
                          </div>
                        )}
                      </div>

                      <div className="space-y-2">
                        <div className="flex items-center justify-between">
                          <h4 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                            Schedule configuration
                          </h4>
                          {form.watch(`segments.${index}.schedule` as const) ? (
                            <Button
                              type="button"
                              size="sm"
                              variant="ghost"
                              onClick={() =>
                                form.setValue(`segments.${index}.schedule` as const, null, { shouldDirty: true })
                              }
                            >
                              Remove schedule
                            </Button>
                          ) : (
                            <Button
                              type="button"
                              size="sm"
                              variant="outline"
                              onClick={() =>
                                form.setValue(`segments.${index}.schedule` as const, createDefaultSegmentSchedule(), {
                                  shouldDirty: true,
                                })
                              }
                            >
                              Add schedule
                            </Button>
                          )}
                        </div>
                        {form.watch(`segments.${index}.schedule` as const) && (
                          <div className="grid gap-3 md:grid-cols-2">
                            <FormItem>
                              <FormLabel>Schedule name</FormLabel>
                              <FormControl>
                                <Input
                                  {...form.register(`segments.${index}.schedule.name` as const)}
                                  placeholder="Nightly sync"
                                />
                              </FormControl>
                            </FormItem>
                            <FormItem>
                              <FormLabel>Schedule status</FormLabel>
                              <FormControl>
                                <Input
                                  {...form.register(`segments.${index}.schedule.status` as const)}
                                  placeholder="Active"
                                />
                              </FormControl>
                            </FormItem>
                            <FormItem>
                              <FormLabel>Schedule type</FormLabel>
                              <FormControl>
                                <Input
                                  {...form.register(`segments.${index}.schedule.type` as const)}
                                  placeholder="Recurring"
                                />
                              </FormControl>
                            </FormItem>
                            <FormItem>
                              <FormLabel>Frequency</FormLabel>
                              <FormControl>
                                <Input
                                  type="number"
                                  {...form.register(`segments.${index}.schedule.frequency` as const, {
                                    setValueAs: (value) => (value === "" ? null : Number(value)),
                                  })}
                                  placeholder="1"
                                />
                              </FormControl>
                            </FormItem>
                            <FormItem>
                              <FormLabel>Frequency unit</FormLabel>
                              <FormControl>
                                <Input
                                  {...form.register(`segments.${index}.schedule.frequencyUnit` as const)}
                                  placeholder="Days"
                                />
                              </FormControl>
                            </FormItem>
                            <FormItem>
                              <FormLabel>Start date (ISO)</FormLabel>
                              <FormControl>
                                <Input
                                  {...form.register(`segments.${index}.schedule.startDate` as const)}
                                  placeholder="2026-05-04T00:00:00.000Z"
                                />
                              </FormControl>
                            </FormItem>
                            <FormItem>
                              <FormLabel>End date (ISO)</FormLabel>
                              <FormControl>
                                <Input
                                  {...form.register(`segments.${index}.schedule.endDate` as const)}
                                  placeholder="2026-06-01T00:00:00.000Z"
                                />
                              </FormControl>
                            </FormItem>
                            <FormItem>
                              <FormLabel>Last run date (ISO)</FormLabel>
                              <FormControl>
                                <Input
                                  {...form.register(`segments.${index}.schedule.lastRunDate` as const)}
                                  placeholder="2026-05-03T08:00:00.000Z"
                                />
                              </FormControl>
                            </FormItem>
                            <FormItem>
                              <FormLabel>Next run date (ISO)</FormLabel>
                              <FormControl>
                                <Input
                                  {...form.register(`segments.${index}.schedule.nextRunDate` as const)}
                                  placeholder="2026-05-04T08:00:00.000Z"
                                />
                              </FormControl>
                            </FormItem>
                          </div>
                        )}
                      </div>
                    </div>
                  </details>
                </CardContent>
              </Card>
            ))}
          </div>
        )}
        <FormDescription>
          Advanced segment source/schedule JSON from existing campaigns is preserved even though this step edits only
          core segment fields.
        </FormDescription>
      </div>

      {unmatchedEvents.length > 0 && (
        <Alert variant="destructive" className="mt-4">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>
            {unmatchedEvents.length} event{unmatchedEvents.length === 1 ? "" : "s"} no longer resolves to a known schema
          </AlertTitle>
          <AlertDescription>
            <p className="mb-2 text-sm">
              These references were saved on the campaign but the underlying schema could not be found (likely deleted
              or renamed). Remove them or restore the schema in the data explorer.
            </p>
            <div className="flex flex-wrap gap-1.5">
              {unmatchedEvents.map((eventId) => (
                <Badge key={eventId} variant="destructive" className="gap-1 font-mono text-[11px]">
                  {eventId}
                  <button
                    type="button"
                    className="rounded-sm hover:bg-destructive-foreground/20"
                    onClick={() => removeUnmatched(eventId)}
                    aria-label={`Remove unmatched event ${eventId}`}
                  >
                    <X className="h-3 w-3" />
                  </button>
                </Badge>
              ))}
            </div>
          </AlertDescription>
        </Alert>
      )}
    </Form>
  );
}
