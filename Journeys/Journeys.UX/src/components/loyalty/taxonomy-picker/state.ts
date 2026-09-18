/**
 * Taxonomy picker state — single useReducer per D12 (LOCKED).
 *
 * Replaces the legacy three-hook split (`useTaxonomyHierarchy`,
 * `useTaxonomySelection`, `useEntityFilter`) with a single reducer that
 * exposes a controlled `value` + `onChange` contract. Parents drive the
 * selection so it survives filter narrowing (the legacy bug where
 * "selection outside filter disappears" cannot occur here).
 */

import type { TaxonomyDto } from "@/lib/campaign-types";

export interface CatalogNode {
  /**
   * Canonical id used everywhere downstream (tree node ids,
   * `TaxonomicRule.TaxonomyId`). Sourced from `dto.ID`; falls back to
   * `dto.ExternalId` only if `ID` is missing.
   */
  id: string;
  name: string;
  taxonomyType: string;
  /**
   * Optional secondary identifier carried so consumers (e.g. edit-existing
   * flow) can match a saved `TaxonomyId` that was authored against the
   * external id rather than the canonical ID.
   */
  externalId: string | null;
}

export interface CategoryNode {
  id: string;
  name: string;
  taxonomyType: string;
  category: string;
  parentId: string | null;
  catalogId: string;
  hasChildren: boolean;
  children: CategoryNode[];
}

export interface EntityRecord {
  id: string;
  name: string;
  category?: string;
  status?: string;
  [key: string]: unknown;
}

/**
 * Resolved metadata for a selected category. Persisted on `value` so the
 * picker chip strip and the `ProductSelection` payload retain `name`,
 * `categoryPath`, and `catalogId` even when the current tree filter does
 * not contain the node (the legacy "selection outside filter disappears"
 * bug). Mirrors how `entities` are persisted.
 */
export interface CategorySelection {
  id: string;
  name: string;
  categoryPath: string;
  catalogId: string;
}

export interface TaxonomyPickerSelection {
  /**
   * Selected category nodes keyed by canonical id. Storing the full record
   * (vs. a bare id Set) means name/path/catalogId survive filter changes
   * AND are available to consumers that need to round-trip back into a
   * `TaxonomicRule` payload.
   */
  categories: ReadonlyMap<string, CategorySelection>;
  /**
   * Selected entity records — full record persisted across filter changes
   * so that selection outside the current filter is never lost.
   */
  entities: ReadonlyMap<string, EntityRecord>;
}

export interface TaxonomyPickerState {
  /** All catalogs (TreeRoots) for the picker. */
  catalogs: CatalogNode[];
  /** Currently active catalog. */
  activeCatalogId: string | null;
  /** Browse tree (categories) for the active catalog. */
  tree: CategoryNode[];
  /** Categories that have already been fetched (to avoid refetching). */
  loadedCatalogs: ReadonlySet<string>;
  /** Expanded category-tree node IDs. */
  expandedNodes: ReadonlySet<string>;
  /** Path of category being browsed (used for the entity grid). */
  browsedCategoryPath: string | null;
  /** Loaded entities for the current category. */
  entities: EntityRecord[];
  /** Search filter (client-side). */
  searchQuery: string;
  /** Continuation token for entity pagination. */
  continuationToken: string | null;
  /** Has more entities to load. */
  hasMoreEntities: boolean;
  /** Loading flags. */
  loading: { catalogs: boolean; categories: boolean; entities: boolean };
  /** Error string. */
  error: string | null;
}

export const initialTaxonomyPickerState: TaxonomyPickerState = {
  catalogs: [],
  activeCatalogId: null,
  tree: [],
  loadedCatalogs: new Set(),
  expandedNodes: new Set(),
  browsedCategoryPath: null,
  entities: [],
  searchQuery: "",
  continuationToken: null,
  hasMoreEntities: false,
  loading: { catalogs: false, categories: false, entities: false },
  error: null,
};

export type TaxonomyPickerAction =
  | { type: "catalogs/loading" }
  | { type: "catalogs/loaded"; catalogs: CatalogNode[]; preferredCatalogId?: string | null }
  | { type: "catalog/select"; catalogId: string | null }
  | { type: "categories/loading" }
  | { type: "categories/loaded"; catalogId: string; tree: CategoryNode[] }
  | { type: "node/toggle"; nodeId: string }
  | { type: "category/browse"; categoryPath: string | null }
  | { type: "entities/loading" }
  | { type: "entities/loaded"; entities: EntityRecord[]; reset: boolean; continuationToken: string | null }
  | { type: "filter/search"; query: string }
  | { type: "error/set"; error: string | null }
  | { type: "reset" };

export function taxonomyPickerReducer(state: TaxonomyPickerState, action: TaxonomyPickerAction): TaxonomyPickerState {
  switch (action.type) {
    case "catalogs/loading":
      return { ...state, loading: { ...state.loading, catalogs: true }, error: null };
    case "catalogs/loaded": {
      const preferred = action.preferredCatalogId ?? null;
      const matchedPreferred = preferred
        ? (action.catalogs.find((c) => c.id === preferred || c.externalId === preferred)?.id ?? null)
        : null;
      const firstCatalogId = action.catalogs[0]?.id ?? null;
      return {
        ...state,
        catalogs: action.catalogs,
        activeCatalogId: state.activeCatalogId ?? matchedPreferred ?? firstCatalogId,
        loading: { ...state.loading, catalogs: false },
      };
    }
    case "catalog/select":
      return {
        ...state,
        activeCatalogId: action.catalogId,
        tree: action.catalogId === state.activeCatalogId ? state.tree : [],
        browsedCategoryPath: null,
        entities: [],
        continuationToken: null,
        hasMoreEntities: false,
      };
    case "categories/loading":
      return { ...state, loading: { ...state.loading, categories: true }, error: null };
    case "categories/loaded": {
      const next = new Set(state.loadedCatalogs);
      next.add(action.catalogId);
      return {
        ...state,
        tree: action.tree,
        loadedCatalogs: next,
        loading: { ...state.loading, categories: false },
      };
    }
    case "node/toggle": {
      const next = new Set(state.expandedNodes);
      if (next.has(action.nodeId)) next.delete(action.nodeId);
      else next.add(action.nodeId);
      return { ...state, expandedNodes: next };
    }
    case "category/browse":
      return { ...state, browsedCategoryPath: action.categoryPath, entities: [], continuationToken: null };
    case "entities/loading":
      return { ...state, loading: { ...state.loading, entities: true }, error: null };
    case "entities/loaded":
      return {
        ...state,
        entities: action.reset ? action.entities : [...state.entities, ...action.entities],
        continuationToken: action.continuationToken,
        hasMoreEntities: action.continuationToken !== null,
        loading: { ...state.loading, entities: false },
      };
    case "filter/search":
      return { ...state, searchQuery: action.query };
    case "error/set":
      return { ...state, error: action.error, loading: { catalogs: false, categories: false, entities: false } };
    case "reset":
      return { ...initialTaxonomyPickerState };
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// ID format helpers — used by both `buildCategoryTree` (data-fetched) and
// `parseTaxonomicRule` (saved-rule-fed) so the IDs match and selection
// preselects correctly when editing.
// ─────────────────────────────────────────────────────────────────────────────

/** Canonical category-node id used by the tree AND by saved-rule selections. */
export function categoryNodeId(catalogId: string, categoryPath: string): string {
  return `category-${catalogId}-${categoryPath}`;
}

/** Convert a `CategoryNode` into the persisted `CategorySelection` record. */
export function nodeToCategorySelection(node: CategoryNode): CategorySelection {
  return {
    id: node.id,
    name: node.name,
    categoryPath: node.category,
    catalogId: node.catalogId,
  };
}

// ─────────────────────────────────────────────────────────────────────────────
// DTO → CatalogNode / CategoryNode helpers
// ─────────────────────────────────────────────────────────────────────────────

/**
 * Convert a taxonomy DTO (TreeRoot) to a `CatalogNode`.
 *
 * Casing tolerance: the upstream KeyValueStorage backend has been observed
 * returning camelCase keys for these endpoints (e.g. `id`, `name`,
 * `externalId`, `dataExternalIds`) even though the canonical schema is
 * PascalCase. The legacy admin-web hook accepted both; we mirror that
 * here so catalogs render with their real names instead of "Unnamed
 * Catalog".
 *
 * Canonical id preference order:
 *   1. `dto.ID` / `dto.id` — matches `TaxonomicRule.TaxonomyId` on saved rules
 *   2. `dto.ExternalId` / `dto.externalId` / `dataExternalIds[0]` — fallback for
 *      tenants where rules were authored against the external id
 *   3. `catalog-${index}` — last-resort to keep dropdown keys unique
 */
export function dtoToCatalog(dto: TaxonomyDto, index: number, usedIds: Set<string>): CatalogNode {
  const loose = dto as TaxonomyDto & {
    id?: string;
    name?: string;
    taxonomyType?: string;
    externalId?: string;
    dataExternalIds?: readonly string[];
    category?: string;
  };

  const id = (loose.ID ?? loose.id ?? "").trim();
  const externalId = (loose.ExternalId ?? loose.externalId ?? loose.dataExternalIds?.[0] ?? "").trim() || null;
  const taxonomyType = loose.TaxonomyType ?? loose.taxonomyType ?? "TreeRoot";
  const rawName = (loose.Name ?? loose.name ?? "").trim();
  const name = rawName || externalId || (loose.Category ?? loose.category ?? "") || "Unnamed Catalog";

  let uniqueId = id || externalId || `catalog-${index}`;
  if (usedIds.has(uniqueId)) uniqueId = `${uniqueId}-${index}`;
  usedIds.add(uniqueId);

  return { id: uniqueId, name, taxonomyType, externalId };
}

/** Build a hierarchical category tree from a flat list of dot-separated paths. */
export function buildCategoryTree(categoryPaths: readonly string[], catalogId: string): CategoryNode[] {
  const nodeMap = new Map<string, CategoryNode>();
  const rootNodes: CategoryNode[] = [];
  const rootNodeIds = new Set<string>();

  for (const categoryPath of categoryPaths) {
    const segments = (categoryPath ?? "").split(".").filter((s) => s.trim().length > 0);
    if (segments.length === 0) continue;

    let currentPath = "";
    let parentId: string | null = null;
    let parentNode: CategoryNode | null = null;

    segments.forEach((segment, index) => {
      currentPath = currentPath ? `${currentPath}.${segment}` : segment;
      const nodeId = categoryNodeId(catalogId, currentPath);

      let node = nodeMap.get(nodeId);
      if (!node) {
        node = {
          id: nodeId,
          name: segment,
          taxonomyType: "ProductCategory",
          category: currentPath,
          parentId,
          catalogId,
          hasChildren: index < segments.length - 1,
          children: [],
        };
        nodeMap.set(nodeId, node);
        if (parentNode) {
          if (!parentNode.children.some((c) => c.id === node!.id)) parentNode.children.push(node);
        } else if (!rootNodeIds.has(node.id)) {
          rootNodes.push(node);
          rootNodeIds.add(node.id);
        }
      }
      parentId = nodeId;
      parentNode = node;
    });
  }

  return rootNodes;
}

/** Find a node by ID in a tree (DFS). */
export function findCategoryNode(tree: CategoryNode[], id: string): CategoryNode | null {
  for (const node of tree) {
    if (node.id === id) return node;
    const found = findCategoryNode(node.children, id);
    if (found) return found;
  }
  return null;
}

/** Find a node by `category` path in a tree (DFS). */
export function findCategoryNodeByPath(tree: CategoryNode[], categoryPath: string): CategoryNode | null {
  for (const node of tree) {
    if (node.category === categoryPath) return node;
    const found = findCategoryNodeByPath(node.children, categoryPath);
    if (found) return found;
  }
  return null;
}

/** Get every descendant of a node (flat list). */
export function getDescendants(node: CategoryNode): CategoryNode[] {
  const out: CategoryNode[] = [];
  for (const child of node.children) {
    out.push(child);
    out.push(...getDescendants(child));
  }
  return out;
}

/** Cascade-toggle a node's selection (selects/deselects with all descendants). */
export function cascadeToggle(
  current: TaxonomyPickerSelection,
  tree: CategoryNode[],
  nodeId: string
): TaxonomyPickerSelection {
  const node = findCategoryNode(tree, nodeId);
  if (!node) return current;
  const next = new Map(current.categories);
  const descendants = getDescendants(node);

  if (next.has(nodeId)) {
    next.delete(nodeId);
    descendants.forEach((d) => next.delete(d.id));
  } else {
    next.set(nodeId, nodeToCategorySelection(node));
    descendants.forEach((d) => next.set(d.id, nodeToCategorySelection(d)));
  }
  return { ...current, categories: next };
}

/** Toggle one entity in selection. The entity record is preserved verbatim. */
export function toggleEntity(current: TaxonomyPickerSelection, entity: EntityRecord): TaxonomyPickerSelection {
  const next = new Map(current.entities);
  if (next.has(entity.id)) next.delete(entity.id);
  else next.set(entity.id, entity);
  return { ...current, entities: next };
}
