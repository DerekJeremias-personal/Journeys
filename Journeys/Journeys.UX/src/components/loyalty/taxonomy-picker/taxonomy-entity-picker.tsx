"use client";

/**
 * TaxonomyEntityPicker — controlled, single-reducer port of the legacy
 * `TaxonomyEntityPicker` (D12 LOCKED).
 *
 * Selection lives outside the reducer (controlled `value` + `onChange`):
 * categories AND entities are passed in by the parent so the chip strip and
 * Apply payload include items NOT in the current filter (the legacy bug).
 *
 * Edit-existing flow: parents pass `initialCatalogId` (the rule's
 * `TaxonomyId`) and optionally `initialBrowsePath` (the first saved
 * category path) so the correct catalog is auto-selected and its items
 * grid pre-loads on open.
 *
 * Data flow:
 *   - Catalogs ← `getTaxonomiesByType({ TaxonomyType: "TreeRoot" })`
 *   - Categories per catalog ← `getTaxonomyCategories(catalogId)`
 *   - Entities per browsed category ← `browseTaxonomy({ TaxonomyType: catalogId, TaxonomyCategory: path })`
 */

import { useCallback, useEffect, useMemo, useReducer, useRef } from "react";
import { ChevronDown, ChevronRight, Search, X } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Checkbox } from "@/components/ui/checkbox";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";

import { browseTaxonomy, getTaxonomiesByType, getTaxonomyCategories } from "@/services/loyalty/taxonomy-actions";
import {
  buildCategoryTree,
  cascadeToggle,
  dtoToCatalog,
  findCategoryNodeByPath,
  initialTaxonomyPickerState,
  taxonomyPickerReducer,
  toggleEntity,
  type CategoryNode,
  type EntityRecord,
  type TaxonomyPickerSelection,
} from "./state";

export interface TaxonomyEntityPickerProps {
  /** Controlled selection — required for parent-driven survival across filters. */
  value: TaxonomyPickerSelection;
  onChange: (next: TaxonomyPickerSelection) => void;
  /** Optional model name for the entity request (carried for compatibility — backend ignores it). */
  entityModelName?: string;
  /** Show the selected-items chip strip. */
  showSelectedSection?: boolean;
  /** Allow taxonomy/category nodes to be selected. */
  allowTaxonomySelection?: boolean;
  /** Allow individual entities (SKUs/products) to be selected. */
  allowEntitySelection?: boolean;
  /** Show the catalog picker dropdown. Defaults to true when more than one catalog is available. */
  showCatalogPicker?: boolean;
  /**
   * Preferred catalog id (typically `TaxonomicRule.TaxonomyId`). When the
   * catalog list resolves we prefer this over `catalogs[0]`. Matches by
   * canonical `id` first, then by `externalId` as a fallback for rules
   * authored against the external id.
   */
  initialCatalogId?: string | null;
  /**
   * Pre-browse this category path once the tree loads so the items grid
   * is populated on open instead of showing the empty placeholder.
   */
  initialBrowsePath?: string | null;
  className?: string;
}

export function TaxonomyEntityPicker({
  value,
  onChange,
  entityModelName: _entityModelName,
  showSelectedSection = true,
  allowTaxonomySelection = true,
  allowEntitySelection = true,
  showCatalogPicker = true,
  initialCatalogId = null,
  initialBrowsePath = null,
  className,
}: TaxonomyEntityPickerProps) {
  const [state, dispatch] = useReducer(taxonomyPickerReducer, initialTaxonomyPickerState);
  const tenantSlug = "session";

  // Track whether we've already auto-browsed the initial path so we don't
  // re-trigger after the user navigates away.
  const didAutoBrowseRef = useRef(false);
  // Request dedupe guards.
  const lastCatalogLoadTenantRef = useRef<string | null>(null);
  const requestedCategoryCatalogsRef = useRef<Set<string>>(new Set());
  const lastEntityLoadKeyRef = useRef<string | null>(null);

  // Load catalogs (once) ─────────────────────────────────────────────────────
  useEffect(() => {
    if (!tenantSlug) return;
    if (lastCatalogLoadTenantRef.current === tenantSlug) return;
    if (state.catalogs.length > 0 || state.loading.catalogs) return;
    lastCatalogLoadTenantRef.current = tenantSlug;
    dispatch({ type: "catalogs/loading" });
    void (async () => {
      const result = await getTaxonomiesByType({
        tenantId: tenantSlug,
        taxonomyType: "TreeRoot",
        pageSize: 100,
      });
      if (!result.success) {
        lastCatalogLoadTenantRef.current = null;
        dispatch({ type: "error/set", error: result.error ?? "Failed to load catalogs" });
        return;
      }
      const usedIds = new Set<string>();
      const catalogs = (result.data ?? [])
        .map((dto, i) => dtoToCatalog(dto, i, usedIds))
        .filter((c) => c.id.trim().length > 0);
      dispatch({ type: "catalogs/loaded", catalogs, preferredCatalogId: initialCatalogId });
    })();
  }, [tenantSlug, state.catalogs.length, state.loading.catalogs, initialCatalogId]);

  // Load categories when catalog selected ────────────────────────────────────
  useEffect(() => {
    const catalogId = state.activeCatalogId;
    if (!catalogId || state.loadedCatalogs.has(catalogId) || state.loading.categories) return;
    if (requestedCategoryCatalogsRef.current.has(catalogId)) return;
    requestedCategoryCatalogsRef.current.add(catalogId);
    dispatch({ type: "categories/loading" });
    void (async () => {
      const result = await getTaxonomyCategories(catalogId);
      if (!result.success) {
        requestedCategoryCatalogsRef.current.delete(catalogId);
        dispatch({ type: "error/set", error: result.error ?? "Failed to load categories" });
        return;
      }
      const tree = buildCategoryTree(result.data ?? [], catalogId);
      dispatch({ type: "categories/loaded", catalogId, tree });
    })();
  }, [state.activeCatalogId, state.loadedCatalogs, state.loading.categories]);

  // Auto-browse the initial category path once the tree for the matching
  // catalog has loaded — skipped if the user has already browsed something.
  useEffect(() => {
    if (didAutoBrowseRef.current) return;
    if (!initialBrowsePath || state.tree.length === 0) return;
    if (state.browsedCategoryPath) return;
    const node = findCategoryNodeByPath(state.tree, initialBrowsePath);
    if (!node) return;
    didAutoBrowseRef.current = true;
    dispatch({ type: "category/browse", categoryPath: node.category });
  }, [initialBrowsePath, state.tree, state.browsedCategoryPath]);

  // Load entities when a category is browsed ─────────────────────────────────
  useEffect(() => {
    const catalogId = state.activeCatalogId;
    const path = state.browsedCategoryPath;
    if (!catalogId || !path || !tenantSlug) return;
    const requestKey = `${tenantSlug}::${catalogId}::${path}`;
    if (lastEntityLoadKeyRef.current === requestKey) return;
    lastEntityLoadKeyRef.current = requestKey;
    dispatch({ type: "entities/loading" });
    void (async () => {
      const result = await browseTaxonomy({
        tenantId: tenantSlug,
        taxonomyType: catalogId,
        taxonomyCategory: path,
        pageSize: 50,
      });
      if (!result.success) {
        lastEntityLoadKeyRef.current = null;
        dispatch({ type: "error/set", error: result.error ?? "Failed to load entities" });
        return;
      }
      const entities: EntityRecord[] = (result.data ?? []).map((dto) => {
        // Backend has been observed returning camelCase too — accept both
        // so the items grid doesn't render blank rows on edit-existing.
        const loose = dto as typeof dto & {
          id?: string;
          name?: string;
          category?: string;
          status?: string;
        };
        return {
          id: (loose.ID ?? loose.id ?? "").trim(),
          name: (loose.Name ?? loose.name ?? "").trim(),
          category: loose.Category ?? loose.category ?? undefined,
          status: loose.Status ?? loose.status ?? undefined,
        };
      });
      dispatch({
        type: "entities/loaded",
        entities,
        reset: true,
        continuationToken: null,
      });
    })();
  }, [state.activeCatalogId, state.browsedCategoryPath, tenantSlug]);

  // Search filter (client-side) ──────────────────────────────────────────────
  const filteredEntities = useMemo<EntityRecord[]>(() => {
    if (!state.searchQuery.trim()) return state.entities;
    const q = state.searchQuery.toLowerCase();
    return state.entities.filter((e) => {
      const haystacks = [e.name, e.id, e["product_name"], e["sku_id"], e["description"]].filter(Boolean);
      return haystacks.some((v) => String(v).toLowerCase().includes(q));
    });
  }, [state.entities, state.searchQuery]);

  // Handlers ─────────────────────────────────────────────────────────────────
  const handleToggleCategory = useCallback(
    (nodeId: string) => {
      onChange(cascadeToggle(value, state.tree, nodeId));
    },
    [onChange, value, state.tree]
  );

  const handleToggleEntity = useCallback(
    (entity: EntityRecord) => {
      onChange(toggleEntity(value, entity));
    },
    [onChange, value]
  );

  const handleRemoveCategory = useCallback(
    (nodeId: string) => {
      const next = new Map(value.categories);
      next.delete(nodeId);
      onChange({ ...value, categories: next });
    },
    [onChange, value]
  );

  const handleRemoveEntity = useCallback(
    (entityId: string) => {
      const next = new Map(value.entities);
      next.delete(entityId);
      onChange({ ...value, entities: next });
    },
    [onChange, value]
  );

  const handleClearAll = useCallback(() => {
    onChange({ categories: new Map(), entities: new Map() });
  }, [onChange]);

  const totalSelected = value.categories.size + value.entities.size;
  const showCatalogSelect = showCatalogPicker && state.catalogs.length > 1;

  return (
    <div className={cn("flex flex-col gap-4", className)}>
      {state.error && (
        <Alert variant="destructive">
          <AlertDescription>{state.error}</AlertDescription>
        </Alert>
      )}

      {showSelectedSection && totalSelected > 0 && (
        <div className="flex flex-wrap items-center gap-2 rounded-lg border bg-muted/30 p-3">
          <span className="text-xs font-medium text-muted-foreground mr-2">Selected ({totalSelected})</span>
          {Array.from(value.categories.values()).map((entry) => (
            <Badge key={entry.id} variant="secondary" className="gap-1">
              {entry.name || entry.categoryPath || entry.id}
              <button
                type="button"
                onClick={() => handleRemoveCategory(entry.id)}
                className="hover:text-destructive ml-1"
                aria-label={`Remove ${entry.name || entry.categoryPath || entry.id}`}
              >
                <X className="h-3 w-3" />
              </button>
            </Badge>
          ))}
          {Array.from(value.entities.values()).map((entity) => (
            <Badge key={entity.id} variant="outline" className="gap-1">
              {entity.name || entity.id}
              <button
                type="button"
                onClick={() => handleRemoveEntity(entity.id)}
                className="hover:text-destructive ml-1"
                aria-label={`Remove ${entity.name || entity.id}`}
              >
                <X className="h-3 w-3" />
              </button>
            </Badge>
          ))}
          <Button variant="ghost" size="sm" className="ml-auto" onClick={handleClearAll}>
            Clear all
          </Button>
        </div>
      )}

      {showCatalogSelect && (
        <div className="flex items-center gap-2">
          <label className="text-sm font-medium" htmlFor="taxonomy-catalog-select">
            Catalog:
          </label>
          <Select
            value={state.activeCatalogId ?? undefined}
            onValueChange={(v) => dispatch({ type: "catalog/select", catalogId: v })}
            disabled={state.loading.catalogs}
          >
            <SelectTrigger id="taxonomy-catalog-select" className="w-[280px]">
              <SelectValue placeholder="Select a catalog" />
            </SelectTrigger>
            <SelectContent>
              {state.catalogs.map((c) => (
                <SelectItem key={c.id} value={c.id}>
                  {c.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-[260px_1fr] gap-4 min-h-[420px]">
        <Card className="overflow-hidden">
          <CardContent className="p-0">
            <div className="border-b bg-muted/30 px-3 py-2">
              <h3 className="text-xs font-semibold uppercase text-muted-foreground">Categories</h3>
            </div>
            <ScrollArea className="h-[400px]">
              {state.loading.categories ? (
                <div className="p-3 space-y-2">
                  {Array.from({ length: 6 }).map((_, i) => (
                    <Skeleton key={i} className="h-7 w-full" />
                  ))}
                </div>
              ) : state.tree.length === 0 ? (
                <p className="p-4 text-sm text-muted-foreground text-center">No categories.</p>
              ) : (
                <ul className="p-1">
                  {state.tree.map((node) => (
                    <CategoryTreeNode
                      key={node.id}
                      node={node}
                      depth={0}
                      expandedNodes={state.expandedNodes}
                      browsedCategoryPath={state.browsedCategoryPath}
                      selectedIds={value.categories}
                      allowSelection={allowTaxonomySelection}
                      onToggleExpand={(id) => dispatch({ type: "node/toggle", nodeId: id })}
                      onToggleSelect={handleToggleCategory}
                      onBrowse={(path) => dispatch({ type: "category/browse", categoryPath: path })}
                    />
                  ))}
                </ul>
              )}
            </ScrollArea>
          </CardContent>
        </Card>

        <Card className="overflow-hidden">
          <CardContent className="p-0">
            <div className="flex items-center gap-2 border-b bg-muted/30 px-3 py-2">
              <h3 className="text-xs font-semibold uppercase text-muted-foreground">Items</h3>
              <div className="relative ml-auto flex-1 max-w-[260px]">
                <Search className="absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-muted-foreground" />
                <Input
                  type="text"
                  placeholder="Search items"
                  value={state.searchQuery}
                  onChange={(e) => dispatch({ type: "filter/search", query: e.target.value })}
                  className="pl-8 h-8 text-xs"
                  aria-label="Search entities"
                />
              </div>
            </div>
            <ScrollArea className="h-[400px]">
              {!state.browsedCategoryPath ? (
                <p className="p-4 text-sm text-muted-foreground text-center">
                  Select a category from the left to browse items.
                </p>
              ) : state.loading.entities ? (
                <div className="p-3 space-y-2">
                  {Array.from({ length: 6 }).map((_, i) => (
                    <Skeleton key={i} className="h-12 w-full" />
                  ))}
                </div>
              ) : filteredEntities.length === 0 ? (
                <p className="p-4 text-sm text-muted-foreground text-center">No items found.</p>
              ) : (
                <ul className="divide-y">
                  {filteredEntities.map((entity) => (
                    <li
                      key={entity.id}
                      className="flex items-center gap-3 px-3 py-2 hover:bg-muted/50 cursor-pointer"
                      onClick={() => allowEntitySelection && handleToggleEntity(entity)}
                    >
                      {allowEntitySelection && (
                        <Checkbox
                          checked={value.entities.has(entity.id)}
                          aria-label={`Select ${entity.name || entity.id}`}
                          onCheckedChange={() => handleToggleEntity(entity)}
                          onClick={(e) => e.stopPropagation()}
                        />
                      )}
                      <div className="flex-1 min-w-0">
                        <p className="text-sm truncate">{entity.name || entity.id}</p>
                        {entity.category && <p className="text-xs text-muted-foreground truncate">{entity.category}</p>}
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </ScrollArea>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

interface CategoryTreeNodeProps {
  node: CategoryNode;
  depth: number;
  expandedNodes: ReadonlySet<string>;
  browsedCategoryPath: string | null;
  selectedIds: ReadonlyMap<string, unknown>;
  allowSelection: boolean;
  onToggleExpand: (id: string) => void;
  onToggleSelect: (id: string) => void;
  onBrowse: (path: string) => void;
}

function CategoryTreeNode({
  node,
  depth,
  expandedNodes,
  browsedCategoryPath,
  selectedIds,
  allowSelection,
  onToggleExpand,
  onToggleSelect,
  onBrowse,
}: CategoryTreeNodeProps) {
  const isExpanded = expandedNodes.has(node.id);
  const isSelected = selectedIds.has(node.id);
  // Active = the browsed path matches THIS node's path or is descended from it
  const isActive =
    browsedCategoryPath !== null &&
    (browsedCategoryPath === node.category || browsedCategoryPath.startsWith(`${node.category}.`));

  return (
    <li>
      <div
        className={cn(
          "flex items-center gap-1.5 rounded px-1.5 py-1 hover:bg-muted/60",
          isActive && "bg-primary/10 text-primary"
        )}
        style={{ paddingLeft: `${depth * 16 + 4}px` }}
      >
        {node.children.length > 0 ? (
          <button
            type="button"
            onClick={() => onToggleExpand(node.id)}
            aria-label={isExpanded ? `Collapse ${node.name}` : `Expand ${node.name}`}
            className="hover:bg-muted rounded p-0.5"
          >
            {isExpanded ? <ChevronDown className="h-3.5 w-3.5" /> : <ChevronRight className="h-3.5 w-3.5" />}
          </button>
        ) : (
          <span className="w-4 inline-block" aria-hidden="true" />
        )}

        {allowSelection && (
          <Checkbox
            checked={isSelected}
            onCheckedChange={() => onToggleSelect(node.id)}
            aria-label={`Select ${node.name}`}
            onClick={(e) => e.stopPropagation()}
          />
        )}

        <button type="button" onClick={() => onBrowse(node.category)} className="text-sm flex-1 text-left truncate">
          {node.name}
        </button>
      </div>
      {isExpanded && node.children.length > 0 && (
        <ul>
          {node.children.map((child) => (
            <CategoryTreeNode
              key={child.id}
              node={child}
              depth={depth + 1}
              expandedNodes={expandedNodes}
              browsedCategoryPath={browsedCategoryPath}
              selectedIds={selectedIds}
              allowSelection={allowSelection}
              onToggleExpand={onToggleExpand}
              onToggleSelect={onToggleSelect}
              onBrowse={onBrowse}
            />
          ))}
        </ul>
      )}
    </li>
  );
}
