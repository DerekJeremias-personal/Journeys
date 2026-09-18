/**
 * Loyalty taxonomy domain — `loyalty-data` upstream (KeyValueStorage backend).
 *
 * Source-of-truth mapping:
 *   - Route shapes: Elevate.ELP/Elevate.ELP.Infra.Backend/BackendAdapter.Taxonomy.cs
 *   - Request DTOs: Elevate.ELP/Elevate.ELP.DTO/Requests/SetTaxonomyRequest.cs
 *   - Wire DTO shapes: legacy `admin-web/src/services/loyalty/types.ts` (`TaxonomyDto`,
 *     `GetManyTaxonomiesRequest`, `GetTaxonomiesByTypeRequest`).
 *   - UI composites: legacy `admin-web/src/services/loyalty/types/taxonomy-entity-picker.ts`
 *     (`TaxonomyNode`, `Catalog`, `Entity`, `SelectionResult`, `ProductSelectionData`).
 *
 * Wire reality (Round 4):
 *   - Backend taxonomy DTOs use **PascalCase** keys end-to-end (verified against
 *     `BackendAdapter.Taxonomy.cs` JsonSerializerOptions + the `getmany` 400 response which
 *     enumerates field names like `TaxonomyIds`, `TenantId`).
 *   - The `getmany` validator ALSO requires lowercase `taxonomyType` on the body (per the 400
 *     response). This is the per-endpoint override the Phase 03 transforms map handles.
 *   - Demo tenants have NO taxonomies provisioned — every taxonomy fixture is a 400 / empty array;
 *     parse tests for the full DTO shape are deferred until ops provisions a tenant with real
 *     taxonomy data. The schemas below are sourced from legacy types + `BackendAdapter.Taxonomy.cs`.
 */
import { z } from "zod";

// ─────────────────────────────────────────────────────────────────────────────
// Open-ended enum — tenants define their own taxonomy types.
// ─────────────────────────────────────────────────────────────────────────────

/** Tenants define their own (`ProductCategory`, etc.). Open string. */
export const TaxonomyTypeSchema = z.string();
export type TaxonomyType = z.infer<typeof TaxonomyTypeSchema>;

// ─────────────────────────────────────────────────────────────────────────────
// Wire DTOs (PascalCase keys preserved)
// ─────────────────────────────────────────────────────────────────────────────

/** @see admin-web `TaxonomyCategoryHierarchyDto` + `BackendAdapter.Taxonomy.cs` route shapes */
export const TaxonomyCategoryHierarchyDtoSchema = z.object({
  Index: z.number().int(),
  Symbol: z.string(),
});
export type TaxonomyCategoryHierarchyDto = z.infer<typeof TaxonomyCategoryHierarchyDtoSchema>;

/**
 * @see admin-web `TaxonomyDto` + `BackendAdapter.Taxonomy.cs`
 *
 * Wire keys are **PascalCase** (verified against the backend adapter `JsonSerializerOptions` —
 * the default STJ casing applies, and the adapter does NOT register a camelCase naming policy).
 */
export const TaxonomyDtoSchema = z.object({
  ID: z.string().optional(),
  TenantId: z.string(),
  TaxonomyType: z.string(),
  Category: z.string().optional(),
  Name: z.string().optional(),
  Description: z.string().optional(),
  ExternalId: z.string().optional(),
  ParentId: z.string().nullable().optional(),
  ModelId: z.string().optional(),
  ModelType: z.string().optional(),
  Status: z.string().optional(),
  Hierarchy: z.array(TaxonomyCategoryHierarchyDtoSchema).optional(),
  NodeMap: z.record(z.string(), z.unknown()).optional(),
  Metadata: z.record(z.string(), z.unknown()).optional(),
  CreateDate: z.string().datetime({ offset: true }).optional(),
  LastUpdated: z.string().datetime({ offset: true }).optional(),
  Data: z.unknown().optional(),
  DataNameAttributeSymbol: z.string().optional(),
  DataExternalIdAttributeSymbol: z.string().optional(),
  DataExternalIds: z.array(z.string()).optional(),
});
export type TaxonomyDto = z.infer<typeof TaxonomyDtoSchema>;

/**
 * @see admin-web `GetManyTaxonomiesRequest` + adapter route `/taxonomy/getmany`
 *
 * Per Round 4 the backend validator ALSO accepts a lowercase `taxonomyType` field as a sibling
 * (per the 400 response). Phase 03's per-endpoint override map handles that — schema declares
 * the canonical PascalCase only.
 */
export const GetManyTaxonomiesRequestSchema = z.object({
  TenantId: z.string(),
  TaxonomyType: z.string(),
  Ids: z.array(
    z.object({
      Id: z.string(),
      TaxonomyType: z.string(),
      Category: z.string().optional(),
    })
  ),
});
export type GetManyTaxonomiesRequest = z.infer<typeof GetManyTaxonomiesRequestSchema>;

/** @see BackendAdapter.Taxonomy.cs route `/taxonomy/getmanyxids` */
export const GetManyTaxonomiesByXidRequestSchema = z.object({
  TenantId: z.string(),
  LookupKeys: z.array(z.string()),
});
export type GetManyTaxonomiesByXidRequest = z.infer<typeof GetManyTaxonomiesByXidRequestSchema>;

/** @see admin-web `GetTaxonomiesByTypeRequest` */
export const GetTaxonomiesByTypeRequestSchema = z.object({
  TenantId: z.string(),
  TaxonomyType: z.string(),
  TaxonomyCategory: z.string().nullable().optional(),
  PageSize: z.number().int().optional(),
  ContinuationToken: z.string().nullable().optional(),
});
export type GetTaxonomiesByTypeRequest = z.infer<typeof GetTaxonomiesByTypeRequestSchema>;

// ─────────────────────────────────────────────────────────────────────────────
// Lookup-related request schemas (admin UI does NOT currently call these write endpoints)
// ─────────────────────────────────────────────────────────────────────────────

/**
 * @internal — not currently used by admin-web. Defined for completeness.
 * @see Elevate.ELP/Elevate.ELP.DTO/Requests/SetTaxonomyRequest.cs (`SetTaxonomyRequestDto`)
 */
export const SetTaxonomyRequestSchema = z.object({
  TaxonomyId: z.string(),
  TaxonomyName: z.string(),
  TaxonomyDescription: z.string(),
  TaxonomyType: z.string(),
});
export type SetTaxonomyRequest = z.infer<typeof SetTaxonomyRequestSchema>;

/** @internal — not currently used by admin-web. */
export const CreateTaxonomyLookupRequestSchema = z.object({
  LookupKey: z.string(),
  TaxonomyId: z.string(),
  TaxonomyType: z.string(),
  Category: z.string(),
  Metadata: z.record(z.string(), z.string()).optional(),
});
export type CreateTaxonomyLookupRequest = z.infer<typeof CreateTaxonomyLookupRequestSchema>;

/** @internal — not currently used by admin-web. */
export const CreateModelLookupRequestSchema = z.object({
  LookupKey: z.string(),
  ModelId: z.string(),
  ModelType: z.string(),
  Metadata: z.record(z.string(), z.string()).optional(),
});
export type CreateModelLookupRequest = z.infer<typeof CreateModelLookupRequestSchema>;

/** @internal — stub — full backend `LookupDto` is in `Backend.Dto.dll` (compiled binary). */
export const LookupDtoSchema = z.object({
  LookupKey: z.string().optional(),
  TaxonomyId: z.string().optional(),
  TaxonomyType: z.string().optional(),
  Category: z.string().optional(),
  ModelId: z.string().optional(),
  ModelType: z.string().optional(),
  Metadata: z.record(z.string(), z.string()).optional(),
});
export type LookupDto = z.infer<typeof LookupDtoSchema>;

/** @internal — not currently used by admin-web. */
export const LookupResultSchema = z.object({
  Found: z.boolean(),
  Lookup: LookupDtoSchema.nullable().optional(),
  ErrorMessage: z.string().nullable().optional(),
});
export type LookupResult = z.infer<typeof LookupResultSchema>;

// ─────────────────────────────────────────────────────────────────────────────
// UI-facing composites (camelCase) — sourced from legacy types/taxonomy-entity-picker.ts
// ─────────────────────────────────────────────────────────────────────────────

export type TaxonomyNode = {
  id: string;
  name: string;
  taxonomyType: string;
  category: string;
  parentId?: string | null | undefined;
  children?: Array<TaxonomyNode> | undefined;
  externalId?: string | undefined;
  metadata?: Record<string, unknown> | undefined;
  hasChildren?: boolean | undefined;
  categoryPath?: string | undefined;
  isCatalog?: boolean | undefined;
  catalogId?: string | undefined;
};
/** UI-facing normalized tree node. Recursive. */
export const TaxonomyNodeSchema: z.ZodType<TaxonomyNode> = z.lazy(() =>
  z.object({
    id: z.string(),
    name: z.string(),
    taxonomyType: z.string(),
    category: z.string(),
    parentId: z.string().nullable().optional(),
    children: z.array(TaxonomyNodeSchema).optional(),
    externalId: z.string().optional(),
    metadata: z.record(z.string(), z.unknown()).optional(),
    hasChildren: z.boolean().optional(),
    categoryPath: z.string().optional(),
    isCatalog: z.boolean().optional(),
    catalogId: z.string().optional(),
  })
);

export const CatalogSchema = z.object({
  id: z.string(),
  name: z.string(),
  taxonomyType: z.string(),
});
export type Catalog = z.infer<typeof CatalogSchema>;

/** Dynamic — entity with id required, all other props open. */
export const EntitySchema = z.object({ id: z.string() }).catchall(z.unknown());
export type Entity = z.infer<typeof EntitySchema>;

export const SelectionResultSchema = z.object({
  taxonomies: z.array(TaxonomyNodeSchema),
  entities: z.array(EntitySchema),
});
export type SelectionResult = z.infer<typeof SelectionResultSchema>;

/** @see admin-web `ProductSelectionData` — used by the campaign wizard taxonomic rule provider. */
export const ProductSelectionSchema = z.object({
  mode: z.enum(["include", "exclude"]),
  categories: z.array(
    z.object({
      id: z.string(),
      name: z.string(),
      categoryPath: z.string(),
    })
  ),
  entities: z.array(
    z.object({
      id: z.string(),
      name: z.string(),
      sku: z.string().optional(),
    })
  ),
  catalogId: z.string().optional(),
});
export type ProductSelection = z.infer<typeof ProductSelectionSchema>;
