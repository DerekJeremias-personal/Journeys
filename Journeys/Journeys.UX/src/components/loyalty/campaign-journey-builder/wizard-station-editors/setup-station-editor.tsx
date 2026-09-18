"use client";

import { useEffect, useMemo, useState } from "react";
import { useFormContext, useWatch } from "react-hook-form";
import { useQuery } from "@tanstack/react-query";
import { Check, ChevronsUpDown, X } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from "@/components/ui/command";
import { FormControl, FormDescription, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";
import { Input } from "@/components/ui/input";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";
import { asLoyaltySchemas, type LoyaltySchema } from "@/lib/campaign-types";
import { getAllSchemas } from "@/services/loyalty/actions";
import { loyaltyKeys } from "@/services/loyalty/query-keys";

import type { CampaignWizardValues } from "@/components/loyalty/campaigns/wizard/campaign-wizard";

import { selectSelectedSchema, useJourneyBuilderActions, useJourneyBuilderStore } from "../journey-builder-store";

function isEventableSchema(schema: LoyaltySchema): boolean {
  return schema.tag === "eventable" || schema.name === "LoyaltyAccountDetails";
}

function slugify(input: string): string {
  return input
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-|-$/g, "");
}

function findSchemaForEvent(schemas: LoyaltySchema[], event: string): LoyaltySchema | null {
  const lowered = event.toLowerCase();
  return schemas.find((schema) => schema.id.toLowerCase() === lowered || schema.name.toLowerCase() === lowered) ?? null;
}

export function SetupStationEditor() {
  const form = useFormContext<CampaignWizardValues>();
  const [eventsOpen, setEventsOpen] = useState(false);
  const actions = useJourneyBuilderActions();
  const selectedSchema = useJourneyBuilderStore(selectSelectedSchema);

  const schemasQuery = useQuery({
    queryKey: loyaltyKeys.schemas.all("loyalty"),
    queryFn: async () => {
      const result = await getAllSchemas();
      if (!result.success) {
        throw new Error(result.error ?? "Failed to load event schemas");
      }
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

  useEffect(() => {
    const schemas = schemasQuery.data;
    if (!schemas || selectedSchema || events.length === 0) {
      return;
    }
    for (const event of events) {
      const schema = findSchemaForEvent(schemas, event);
      if (schema) {
        actions.setSelectedSchema(schema);
        return;
      }
    }
  }, [actions, events, schemasQuery.data, selectedSchema]);

  const toggleEvent = (schemaId: string) => {
    const next = events.includes(schemaId) ? events.filter((event) => event !== schemaId) : [...events, schemaId];
    form.setValue("events", next, { shouldDirty: true, shouldValidate: true });

    const schemas = schemasQuery.data ?? [];
    const wasAlreadySelected = events.includes(schemaId);
    if (!wasAlreadySelected) {
      const schema = schemas.find((entry) => entry.id === schemaId);
      if (schema && !selectedSchema) {
        actions.setSelectedSchema(schema);
      }
    } else if (selectedSchema && (selectedSchema.id === schemaId || selectedSchema.name === schemaId)) {
      const fallback = next.map((event) => findSchemaForEvent(schemas, event)).find((entry) => entry !== null) ?? null;
      actions.setSelectedSchema(fallback);
    }
  };

  return (
    <div className="grid gap-4 md:grid-cols-2">
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
                onChange={(event) => {
                  const next = event.target.value;
                  field.onChange(next);
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
              <Input placeholder="summer-points-2025" {...field} value={field.value ?? ""} />
            </FormControl>
            <FormDescription>Auto-generated from the name unless you override it.</FormDescription>
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
                <SelectItem value="draft">Draft</SelectItem>
                <SelectItem value="live">Live</SelectItem>
                <SelectItem value="pause">Pause</SelectItem>
                <SelectItem value="archive">Archive</SelectItem>
              </SelectContent>
            </Select>
            <FormMessage />
          </FormItem>
        )}
      />

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
                onChange={(event) =>
                  field.onChange(event.target.value ? new Date(event.target.value).toISOString() : "")
                }
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
                onChange={(event) =>
                  field.onChange(event.target.value ? new Date(event.target.value).toISOString() : null)
                }
              />
            </FormControl>
            <FormDescription>Leave blank for ongoing campaigns.</FormDescription>
            <FormMessage />
          </FormItem>
        )}
      />

      <FormField
        control={form.control}
        name="events"
        render={() => (
          <FormItem className="md:col-span-2">
            <FormLabel>Event types</FormLabel>
            {schemasQuery.isLoading ? (
              <Skeleton className="h-10 w-full" />
            ) : (
              <Popover open={eventsOpen} onOpenChange={setEventsOpen}>
                <PopoverTrigger asChild>
                  <Button type="button" variant="outline" className="w-full justify-between">
                    <span className="truncate text-left">
                      {events.length === 0 ? "Pick event types…" : `${String(events.length)} selected`}
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
            <FormDescription>Event schemas drive downstream criteria editors.</FormDescription>
            {events.length > 0 ? (
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
            ) : null}
            <FormMessage />
          </FormItem>
        )}
      />
    </div>
  );
}
