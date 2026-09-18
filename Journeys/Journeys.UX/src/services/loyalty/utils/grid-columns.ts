/**
 * Shared utility for generating grid columns from schema attributes.
 * Used by the dynamic data table, the accounts selection modal, and the
 * schema-driven entity detail views.
 */

import type { ReactNode } from "react";
import type { LoyaltySchema as Schema, SchemaAttribute } from "@/lib/loyalty-schema-types";

// The attribute shape the grid helpers read. `symbol` is required here because a
// column without a key cannot be rendered.
type Attribute = SchemaAttribute & {
  symbol: string;
};

/**
 * Get value from nested object using dot notation.
 * @example getNestedValue(row, "event.customerId")
 */
export function getNestedValue(obj: unknown, path: string): unknown {
  return path.split(".").reduce((current: unknown, key: string) => {
    if (current && typeof current === "object" && key in current) {
      return (current as Record<string, unknown>)[key];
    }
    return undefined;
  }, obj);
}

function getCaseInsensitiveKey(record: Record<string, unknown>, key: string): string | undefined {
  const lowered = key.toLowerCase();
  return Object.keys(record).find((candidate) => candidate.toLowerCase() === lowered);
}

/**
 * Read a schema-defined field from a dynamic row.
 *
 * Event payloads have historically mixed camelCase and lower-case symbols.
 * Prefer exact schema matches, then fall back to case-insensitive keys so
 * configured detail fields render existing data instead of empty dashes.
 */
export function getSchemaFieldValue(obj: unknown, path: string): unknown {
  const exactValue = getNestedValue(obj, path);
  if (exactValue !== undefined) return exactValue;

  return path.split(".").reduce((current: unknown, key: string) => {
    if (!current || typeof current !== "object" || Array.isArray(current)) {
      return undefined;
    }

    const record = current as Record<string, unknown>;
    const resolvedKey = key in record ? key : getCaseInsensitiveKey(record, key);
    return resolvedKey ? record[resolvedKey] : undefined;
  }, obj);
}

/**
 * Format a value as a string based on the schema-declared data type.
 * Handles Date, Number, Boolean, and falls through to String() for everything else.
 */
export function formatValueAsString(value: unknown, dataType: string | null | undefined): string {
  if (value === null || value === undefined) return "";

  switch ((dataType ?? "string").toLowerCase()) {
    case "date":
    case "datetime":
      try {
        return new Date(value as string | number | Date).toLocaleDateString();
      } catch {
        return String(value);
      }
    case "number":
    case "int":
    case "double":
      return typeof value === "number" ? value.toLocaleString() : String(value);
    case "boolean":
    case "bool":
      return value ? "Yes" : "No";
    default:
      return String(value);
  }
}

/** Column definition consumed by data tables. */
export interface GridColumn<T = unknown> {
  key: string;
  label: string;
  sortable?: boolean;
  dataType: string;
  render?: (item: T) => ReactNode;
}

/**
 * Filter and sort schema attributes for display in a grid.
 * - Prefer explicitly configured Primitive attributes, even when the attribute
 *   status is Draft. Some live tenant models store admin UI placement on Draft
 *   attributes while the parent model is Live.
 * - Otherwise show Live Primitive attributes
 * - Sort by `gridColumnNumber` ascending
 */
export function getGridAttributes(attributes: Attribute[]): Attribute[] {
  const primitiveAttributes = attributes.filter((attr) => attr.type === "Primitive");

  const gridAttributes = primitiveAttributes.filter((attr) => attr.gridColumnNumber != null || attr.isInGrid === true);

  const livePrimitiveAttributes = primitiveAttributes.filter((attr) => attr.status === "Live");
  const displayAttributes = gridAttributes.length > 0 ? gridAttributes : livePrimitiveAttributes;

  return [...displayAttributes].sort((a, b) => {
    if (a.gridColumnNumber != null && b.gridColumnNumber != null) {
      return a.gridColumnNumber - b.gridColumnNumber;
    }
    if (a.gridColumnNumber != null) return -1;
    if (b.gridColumnNumber != null) return 1;
    return 0;
  });
}

/** Fallback ID column when no attributes are configured for grid display. */
const FALLBACK_ID_COLUMN: GridColumn = {
  key: "id",
  label: "ID",
  sortable: true,
  dataType: "String",
};

/** Generate column definitions from a schema's attribute list. */
export function generateColumnsFromSchema(schema: Schema): GridColumn[] {
  const sortedAttributes = getGridAttributes((schema.attributes ?? []) as Attribute[]);

  if (sortedAttributes.length === 0) return [FALLBACK_ID_COLUMN];

  return sortedAttributes.map((attr) => ({
    key: attr.symbol,
    label: attr.displayName ?? attr.symbol,
    sortable: attr.isGridSortable !== false,
    dataType: attr.dataType ?? "String",
  }));
}

/** Get the initial sort configuration from a schema (first attribute with defaultSortDirection). */
export function getInitialSortFromSchema(schema: Schema): { key: string; order: "asc" | "desc" } {
  const sortableAttr = getGridAttributes((schema.attributes ?? []) as Attribute[]).find(
    (attr) => attr.isGridSortable !== false && attr.defaultSortDirection
  );

  if (sortableAttr?.defaultSortDirection) {
    return { key: sortableAttr.symbol, order: sortableAttr.defaultSortDirection };
  }

  return { key: "", order: "asc" };
}
