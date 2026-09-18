"use client";

/**
 * LoyaltyAccountsListClient — schema-driven list view via `<DynamicDataTable>`.
 *
 * Loads the `LoyaltyAccountDetails` schema and passes it to `<DynamicDataTable>`
 * with `skipAccountSelection` and `detailRoutePath="/loyalty/accounts"`. Columns
 * come from the schema's grid config; no fixed column set.
 */

import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Skeleton } from "@/components/ui/skeleton";

import type { LoyaltySchema } from "@/lib/loyalty-schema-types";
import { getSchemaByName } from "@/services/loyalty/actions";
import { loyaltyKeys } from "@/services/loyalty/query-keys";
import { LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME } from "@/services/loyalty/schema-names";
import {
  LOYALTY_ACCOUNT_IDENTIFIER_SEARCH_FIELDS,
  resolveLoyaltyAccountId,
} from "@/services/loyalty/utils/account-identifiers";
import { DynamicDataTable } from "@/components/loyalty/dynamic-data";

export function LoyaltyAccountsListClient() {
  const schemaQuery = useQuery({
    queryKey: loyaltyKeys.schemas.byName(LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME),
    queryFn: async () => {
      const r = await getSchemaByName(LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME);
      if (!r.success) throw new Error(r.error ?? "Failed to load schema");
      return r.data ?? null;
    },
    staleTime: 5 * 60 * 1000,
  });

  // `SchemaListItem` carries the same fields the table reads; widen it to the
  // schema shape the schema-driven components expect.
  const schema = useMemo<LoyaltySchema | null>(
    () => (schemaQuery.data ? { ...schemaQuery.data } : null),
    [schemaQuery.data]
  );

  if (schemaQuery.isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-12 w-full" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (schemaQuery.error) {
    return (
      <Alert variant="destructive">
        <AlertTitle>Failed to load schema</AlertTitle>
        <AlertDescription>{(schemaQuery.error as Error).message}</AlertDescription>
      </Alert>
    );
  }

  if (!schema) {
    return (
      <p className="empty">The LoyaltyAccountDetails schema is missing or not Live.</p>
    );
  }

  return (
    <DynamicDataTable
      schema={schema}
      skipAccountSelection
      detailRoutePath="/loyalty/accounts"
      emptyMessage="No loyalty accounts found."
      countLabel={{ singular: "loyalty account", plural: "loyalty accounts" }}
      exportFilePrefix="loyalty-accounts"
      extraSearchFields={LOYALTY_ACCOUNT_IDENTIFIER_SEARCH_FIELDS}
      fallbackIdColumn={{
        label: "Loyalty account ID",
        getValue: (row) => resolveLoyaltyAccountId(row as Record<string, unknown>, row.id ?? ""),
      }}
      getDetailId={(row) => resolveLoyaltyAccountId(row as Record<string, unknown>, row.id ?? "")}
    />
  );
}
