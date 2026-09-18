"use client";

/**
 * EventableModelsSection — for an account, list every Live eventable schema and
 * show one collapsible table per schema with that account's events.
 *
 * Rows are read-only: the events are shown inline here, not on another screen.
 */

import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ChevronDown } from "lucide-react";

import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from "@/components/ui/collapsible";
import { Skeleton } from "@/components/ui/skeleton";
import { SimpleDataTable } from "@/components/loyalty/dynamic-data";

import type { LoyaltySchema } from "@/lib/loyalty-schema-types";
import { getAllSchemas, queryData } from "@/services/loyalty/actions";
import { isEventableSchema } from "@/services/loyalty/eventable-schema";
import { loyaltyKeys } from "@/services/loyalty/query-keys";

const EVENT_PAGE_SIZE = 25;

/** A schema we can actually query: the model name is the query partition. */
type NamedSchema = LoyaltySchema & { name: string };

export interface EventableModelsSectionProps {
  loyaltyAccountId: string;
  className?: string;
}

export function EventableModelsSection({ loyaltyAccountId, className }: EventableModelsSectionProps) {
  const schemasQuery = useQuery({
    queryKey: loyaltyKeys.schemas.all("loyalty"),
    queryFn: async () => {
      const r = await getAllSchemas();
      if (!r.success) throw new Error(r.error ?? "Failed to load schemas");
      return r.data ?? [];
    },
  });

  // `getAllSchemas` already filters to loyalty models; widen each row to the
  // schema shape the schema-driven table reads.
  const eventableSchemas = useMemo<NamedSchema[]>(
    () =>
      (schemasQuery.data ?? [])
        .map((s): LoyaltySchema => ({ ...s }))
        .filter((s): s is NamedSchema => typeof s.name === "string" && s.name.length > 0)
        .filter((s) => s.status === "Live" && isEventableSchema(s)),
    [schemasQuery.data]
  );

  if (schemasQuery.isLoading) {
    return <Skeleton className={`h-32 w-full ${className ?? ""}`} />;
  }

  if (schemasQuery.error) {
    return (
      <Alert variant="destructive" className={className}>
        <AlertDescription>{(schemasQuery.error as Error).message}</AlertDescription>
      </Alert>
    );
  }

  if (eventableSchemas.length === 0) {
    return null;
  }

  return (
    <Card className={className}>
      <CardContent className="p-4 space-y-3">
        <div>
          <h3 className="text-base font-semibold">Eventable models</h3>
          <p className="text-sm text-muted-foreground">All event types attached to this loyalty account.</p>
        </div>

        <div className="space-y-2">
          {eventableSchemas.map((schema) => (
            <EventableSchemaPanel key={schema.id ?? schema.name} schema={schema} loyaltyAccountId={loyaltyAccountId} />
          ))}
        </div>
      </CardContent>
    </Card>
  );
}

interface EventableSchemaPanelProps {
  schema: NamedSchema;
  loyaltyAccountId: string;
}

function EventableSchemaPanel({ schema, loyaltyAccountId }: EventableSchemaPanelProps) {
  const [isOpen, setIsOpen] = useState(false);

  const dataQuery = useQuery({
    queryKey: loyaltyKeys.eventable.bySchemaAccount(schema.name, loyaltyAccountId),
    queryFn: async () => {
      const r = await queryData<Record<string, unknown>>({
        schemaName: schema.name,
        loyaltyAccountId,
        pageSize: EVENT_PAGE_SIZE,
      });
      if (!r.success) throw new Error(r.error ?? "Failed to load events");
      return r.data ?? [];
    },
    enabled: isOpen,
  });

  const rows = (dataQuery.data ?? []).map((item) => {
    const event = (item as { event?: Record<string, unknown> }).event;
    return event ?? item;
  });

  return (
    <Collapsible open={isOpen} onOpenChange={setIsOpen}>
      <CollapsibleTrigger asChild>
        <Button variant="ghost" className="w-full justify-between border rounded-lg px-4 py-3 h-auto">
          <div className="flex items-center gap-3">
            <span className="text-sm font-medium">{schema.name}</span>
            {dataQuery.data && (
              <Badge variant="secondary">
                {dataQuery.data.length}
                {dataQuery.data.length === EVENT_PAGE_SIZE ? "+" : ""}
              </Badge>
            )}
          </div>
          <ChevronDown className="h-4 w-4 transition-transform data-[state=open]:rotate-180" aria-hidden="true" />
        </Button>
      </CollapsibleTrigger>
      <CollapsibleContent className="pt-2">
        {dataQuery.isLoading ? (
          <Skeleton className="h-24 w-full" />
        ) : dataQuery.error ? (
          <Alert variant="destructive">
            <AlertDescription>{(dataQuery.error as Error).message}</AlertDescription>
          </Alert>
        ) : rows.length === 0 ? (
          <p className="text-sm text-muted-foreground p-4 text-center">No events for this account.</p>
        ) : (
          <SimpleDataTable schema={schema} data={rows} />
        )}
      </CollapsibleContent>
    </Collapsible>
  );
}
