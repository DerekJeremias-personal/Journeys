"use client";

/**
 * ProductSelector — selection UI for catalog categories + individual SKUs.
 *
 * Composes the same `state.ts` reducer + `TaxonomyEntityPicker` from
 * `@/components/loyalty/taxonomy-picker` (Phase 04 plan §3 option b — both
 * pickers share the hooks/state but are presented differently).
 *
 * Adds:
 *   - Include/Exclude mode toggle
 *   - Tabs (Categories vs Manual SKUs)
 *   - Selection summary side panel
 *
 * Returns a `ProductSelection` shape with `mode`, `categories[]`, `entities[]`.
 *
 * Edit-existing flow: when `initialSelection` is supplied (e.g. opening a
 * saved `TaxonomicRule`), the picker is steered to the catalog the rule
 * was authored against and pre-browses the first category so the items
 * grid loads immediately.
 */

import { useCallback, useMemo, useState } from "react";

import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { ScrollArea } from "@/components/ui/scroll-area";
import { X } from "lucide-react";
import { cn } from "@/lib/utils";

import {
  TaxonomyEntityPicker,
  categoryNodeId,
  type CategoryNode,
  type CategorySelection,
  type EntityRecord,
  type TaxonomyPickerSelection,
} from "@/components/loyalty/taxonomy-picker";

export type SelectionMode = "include" | "exclude";

export interface SelectedCategory {
  id: string;
  name: string;
  categoryPath: string;
  catalogId: string;
}

export interface SelectedEntity {
  id: string;
  name: string;
  category?: string;
}

export interface ProductSelection {
  mode: SelectionMode;
  categories: SelectedCategory[];
  entities: SelectedEntity[];
}

export interface ProductSelectorProps {
  initialSelection?: ProductSelection;
  onChange?: (selection: ProductSelection) => void;
  showModeToggle?: boolean;
  defaultMode?: SelectionMode;
  defaultTab?: "categories" | "manual";
  className?: string;
}

const EMPTY_PICKER: TaxonomyPickerSelection = { categories: new Map(), entities: new Map() };

/**
 * Seed the controlled picker value from a `ProductSelection`. We re-key
 * each entry under the canonical `category-${catalogId}-${path}` id so
 * the selection lines up with the tree the picker will subsequently
 * render (`buildCategoryTree` uses the same id format).
 */
function seedPickerValue(initial: ProductSelection | undefined): TaxonomyPickerSelection {
  if (!initial) return EMPTY_PICKER;
  const categories = new Map<string, CategorySelection>();
  for (const c of initial.categories) {
    const canonicalId = c.catalogId ? categoryNodeId(c.catalogId, c.categoryPath) : c.id;
    categories.set(canonicalId, {
      id: canonicalId,
      name: c.name || c.categoryPath.split(".").pop() || c.categoryPath,
      categoryPath: c.categoryPath,
      catalogId: c.catalogId,
    });
  }
  const entities = new Map<string, EntityRecord>(
    initial.entities.map((e) => [e.id, { id: e.id, name: e.name, category: e.category }])
  );
  return { categories, entities };
}

function pickerValueToProductSelection(mode: SelectionMode, picker: TaxonomyPickerSelection): ProductSelection {
  return {
    mode,
    categories: Array.from(picker.categories.values()).map((c) => ({
      id: c.id,
      name: c.name,
      categoryPath: c.categoryPath,
      catalogId: c.catalogId,
    })),
    entities: Array.from(picker.entities.values()).map((e) => ({
      id: e.id,
      name: e.name,
      category: e.category,
    })),
  };
}

export function ProductSelector({
  initialSelection,
  onChange,
  showModeToggle = true,
  defaultMode = "include",
  defaultTab = "categories",
  className,
}: ProductSelectorProps) {
  // Treat initialSelection as a one-time seed for this mounted instance.
  // While the modal is open, parent state updates should not continuously
  // re-steer taxonomy auto-browse/load effects.
  const [seedSelection] = useState<ProductSelection | undefined>(() => initialSelection);

  const [mode, setMode] = useState<SelectionMode>(seedSelection?.mode ?? defaultMode);
  const [tab, setTab] = useState<"categories" | "manual">(defaultTab);
  const [pickerValue, setPickerValue] = useState<TaxonomyPickerSelection>(() => seedPickerValue(seedSelection));

  // Steer the picker at the right catalog + first category on open.
  const initialCatalogId = useMemo(() => seedSelection?.categories[0]?.catalogId ?? null, [seedSelection]);
  const initialBrowsePath = useMemo(() => seedSelection?.categories[0]?.categoryPath ?? null, [seedSelection]);

  const handlePickerChange = useCallback(
    (next: TaxonomyPickerSelection) => {
      setPickerValue(next);
      onChange?.(pickerValueToProductSelection(mode, next));
    },
    [mode, onChange]
  );

  const handleModeChange = useCallback(
    (next: SelectionMode | null) => {
      if (!next) return;
      setMode(next);
      onChange?.(pickerValueToProductSelection(next, pickerValue));
    },
    [pickerValue, onChange]
  );

  const summaryEntries = useMemo(
    () => ({
      categories: Array.from(pickerValue.categories.values()),
      entities: Array.from(pickerValue.entities.values()),
    }),
    [pickerValue]
  );

  return (
    <div className={cn("space-y-4", className)}>
      {showModeToggle && (
        <div className="flex items-center gap-3">
          <span className="text-sm font-medium">Selection mode</span>
          <ToggleGroup
            type="single"
            value={mode}
            onValueChange={(v) => handleModeChange(v ? (v as SelectionMode) : null)}
            aria-label="Selection mode"
          >
            <ToggleGroupItem value="include">Include</ToggleGroupItem>
            <ToggleGroupItem value="exclude">Exclude</ToggleGroupItem>
          </ToggleGroup>
        </div>
      )}

      <div className="grid grid-cols-1 gap-4 xl:grid-cols-[minmax(0,1fr)_280px]">
        <Tabs value={tab} onValueChange={(v) => setTab(v as "categories" | "manual")}>
          <TabsList>
            <TabsTrigger value="categories">
              Categories
              {pickerValue.categories.size > 0 && (
                <Badge variant="secondary" className="ml-2">
                  {pickerValue.categories.size}
                </Badge>
              )}
            </TabsTrigger>
            <TabsTrigger value="manual">
              Manual SKUs
              {pickerValue.entities.size > 0 && (
                <Badge variant="secondary" className="ml-2">
                  {pickerValue.entities.size}
                </Badge>
              )}
            </TabsTrigger>
          </TabsList>

          <TabsContent value="categories" className="mt-4">
            <TaxonomyEntityPicker
              value={pickerValue}
              onChange={handlePickerChange}
              showSelectedSection={false}
              allowEntitySelection={false}
              initialCatalogId={initialCatalogId}
              initialBrowsePath={initialBrowsePath}
            />
          </TabsContent>
          <TabsContent value="manual" className="mt-4">
            <TaxonomyEntityPicker
              value={pickerValue}
              onChange={handlePickerChange}
              showSelectedSection={false}
              allowTaxonomySelection={false}
              initialCatalogId={initialCatalogId}
              initialBrowsePath={initialBrowsePath}
            />
          </TabsContent>
        </Tabs>

        <SelectionSummaryPanel
          mode={mode}
          summary={summaryEntries}
          onRemoveCategory={(id) => {
            const next = new Map(pickerValue.categories);
            next.delete(id);
            handlePickerChange({ ...pickerValue, categories: next });
          }}
          onRemoveEntity={(id) => {
            const entities = new Map(pickerValue.entities);
            entities.delete(id);
            handlePickerChange({ ...pickerValue, entities });
          }}
          onClear={() => handlePickerChange(EMPTY_PICKER)}
        />
      </div>
    </div>
  );
}

interface SelectionSummaryPanelProps {
  mode: SelectionMode;
  summary: { categories: CategorySelection[]; entities: EntityRecord[] };
  onRemoveCategory: (id: string) => void;
  onRemoveEntity: (id: string) => void;
  onClear: () => void;
}

function SelectionSummaryPanel({
  mode,
  summary,
  onRemoveCategory,
  onRemoveEntity,
  onClear,
}: SelectionSummaryPanelProps) {
  const total = summary.categories.length + summary.entities.length;
  return (
    <Card className="overflow-hidden">
      <CardContent className="p-0">
        <div className="flex items-center justify-between border-b bg-muted/30 px-3 py-2">
          <div>
            <h3 className="text-xs font-semibold uppercase">Selection</h3>
            <p className="text-xs text-muted-foreground">
              Mode: <span className="font-medium">{mode}</span> · {total} item{total === 1 ? "" : "s"}
            </p>
          </div>
          {total > 0 && (
            <Button size="sm" variant="ghost" onClick={onClear}>
              Clear
            </Button>
          )}
        </div>
        <ScrollArea className="h-[400px]">
          {total === 0 ? (
            <p className="p-4 text-sm text-muted-foreground text-center">No selections yet.</p>
          ) : (
            <div className="p-2 space-y-1">
              {summary.categories.map((entry) => (
                <div key={entry.id} className="flex items-center gap-2 rounded px-2 py-1 hover:bg-muted/60">
                  <Badge variant="secondary" className="text-xs">
                    Category
                  </Badge>
                  <span className="text-xs flex-1 truncate" title={entry.categoryPath}>
                    {entry.name || entry.categoryPath || entry.id}
                  </span>
                  <button
                    type="button"
                    onClick={() => onRemoveCategory(entry.id)}
                    aria-label={`Remove ${entry.name || entry.id}`}
                    className="hover:text-destructive"
                  >
                    <X className="h-3.5 w-3.5" />
                  </button>
                </div>
              ))}
              {summary.entities.map((entity) => (
                <div key={entity.id} className="flex items-center gap-2 rounded px-2 py-1 hover:bg-muted/60">
                  <Badge variant="outline" className="text-xs">
                    SKU
                  </Badge>
                  <span className="text-xs flex-1 truncate">{entity.name || entity.id}</span>
                  <button
                    type="button"
                    onClick={() => onRemoveEntity(entity.id)}
                    aria-label={`Remove ${entity.name || entity.id}`}
                    className="hover:text-destructive"
                  >
                    <X className="h-3.5 w-3.5" />
                  </button>
                </div>
              ))}
            </div>
          )}
        </ScrollArea>
      </CardContent>
    </Card>
  );
}

// Suppress unused warning for `CategoryNode` import (kept for re-export compatibility).
export type { CategoryNode };
