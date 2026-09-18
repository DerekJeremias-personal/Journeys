/**
 * Local schema/attribute shapes for the schema-driven loyalty tables and detail
 * views. These describe only the fields those components actually read; the
 * index signatures keep the rest of the API payload addressable without
 * modelling every field the model service can emit.
 */

export type SchemaAttribute = {
  symbol?: string;
  name?: string;
  displayName?: string;
  dataType?: string;
  type?: string;
  attributeType?: string;
  status?: string;
  isRequired?: boolean;
  /** Grid placement (`grid-columns.ts`). */
  isInGrid?: boolean;
  gridColumnNumber?: number;
  isGridSortable?: boolean;
  defaultSortDirection?: "asc" | "desc";
  /** Detail layout placement (`DynamicEntityDetails`). */
  attributeLayoutGroup?: string;
  attributeLayoutPosition?: number;
  attributeLayoutHeight?: string;
  isDetailsViewable?: boolean;
  /** Related model for ModelObject / ModelList attributes. */
  modelId?: string;
  [key: string]: unknown;
};

export type LoyaltySchema = {
  id?: string;
  name?: string;
  /** Operator-facing model name; `name` is the fallback. */
  displayName?: string;
  status?: string;
  tag?: string;
  modelType?: string;
  modelVersion?: string;
  attributes?: SchemaAttribute[];
  [key: string]: unknown;
};

export type JsonValue = string | number | boolean | null | JsonValue[] | { [key: string]: JsonValue };

export type PaginationMeta = {
  page: number;
  limit: number;
  total: number;
  totalPages: number;
  hasNext: boolean;
  hasPrev: boolean;
};

export type PaginationParams = {
  page?: number;
  limit?: number;
  sortBy?: string;
  sortOrder?: "asc" | "desc";
};

/** Canonical detail-layout groups, in render order. */
export const CURATED_ATTRIBUTE_LAYOUT_GROUPS = [
  "header-panel",
  "top-left-split-panel",
  "top-right-split-panel",
  "middle-panel",
  "outcomes",
] as const;
