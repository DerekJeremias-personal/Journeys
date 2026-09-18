"use client";

/**
 * SimpleDataTable — read-only schema-driven table for primitive attribute lists.
 *
 * Renders via the shared `<DataTable>` so nested grids keep the same a11y
 * affordances as the top-level list.
 */

import { useMemo } from "react";
import { DataTable, type ColumnDef } from "@/components/shared/data-table";
import type { LoyaltySchema } from "@/lib/loyalty-schema-types";
import { formatValueAsString, generateColumnsFromSchema, getNestedValue } from "@/services/loyalty/utils/grid-columns";

export interface SimpleDataTableProps<T = unknown> {
  schema: LoyaltySchema;
  data: T[];
  emptyMessage?: string;
}

export function SimpleDataTable<T = unknown>({
  schema,
  data,
  emptyMessage = "No data available.",
}: SimpleDataTableProps<T>) {
  const columns = useMemo<ColumnDef<T>[]>(() => {
    return generateColumnsFromSchema(schema).map((col) => ({
      id: col.key,
      header: col.label,
      sortable: false,
      cell: (row: T) => (
        <span className="text-sm">{formatValueAsString(getNestedValue(row, col.key), col.dataType)}</span>
      ),
    }));
  }, [schema]);

  return <DataTable<T> columns={columns} data={data} emptyMessage={emptyMessage} />;
}
