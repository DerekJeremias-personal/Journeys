"use client";

/**
 * ConditionCard — a single condition row inside `<LoyaltyRuleBuilder>`.
 *
 * Two variants:
 *   - Standard:  `Provider {Evaluator} Provider`
 *   - Taxonomic: `event.items` ∈ products
 *
 * Uses shadcn primitives only — no bespoke styling — and consumes
 * `<ProductSelectorModal>` from `components/loyalty/product-selector` for the
 * taxonomic case.
 */

import { useState } from "react";
import { Trash2, Pencil, Package } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectLabel,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";

import { ProductSelectorModal, type ProductSelection } from "@/components/loyalty/product-selector";
import type { PointAccountType } from "@/lib/campaign-types";

import { STANDARD_PROVIDER_TYPES } from "./build-rule";
import {
  defaultHistoricalConfig,
  EVALUATORS,
  EVALUATOR_BY_VALUE,
  type HistoricalAggregateType,
  type HistoricalTemporalComparison,
  type Condition,
  type ConditionKind,
  type ConditionSide,
  type UiProviderType,
} from "./types";

export interface SchemaProperty {
  /** Path appended after `payload.` (e.g. `spendTotal`). */
  path: string;
  label: string;
  /** Wire data type (e.g. `Number`, `String`, `List`, `Object`). */
  dataType: string;
}

export interface ConditionCardProps {
  condition: Condition;
  index: number;
  isOnlyCondition: boolean;
  composite: "AndRule" | "OrRule";
  onUpdate: (updates: Partial<Condition>) => void;
  onUpdateSide: (side: "left" | "right", updates: Partial<ConditionSide>) => void;
  onRemove: () => void;
  schemaProperties: SchemaProperty[];
  pointAccountTypes: Pick<PointAccountType, "id" | "name">[];
}

const TAXONOMY_TYPES = ["TreeRoot", "GraphRoot"] as const;
const HISTORICAL_AGGREGATES: HistoricalAggregateType[] = ["Count", "Sum", "Average", "Min", "Max"];
const HISTORICAL_TEMPORAL_COMPARISONS: HistoricalTemporalComparison[] = ["OnOrAfter", "After", "OnOrBefore", "Before"];

export function ConditionCard({
  condition,
  index,
  isOnlyCondition,
  composite,
  onUpdate,
  onUpdateSide,
  onRemove,
  schemaProperties,
  pointAccountTypes,
}: ConditionCardProps) {
  const [isProductModalOpen, setIsProductModalOpen] = useState(false);
  const compositeLabel = composite === "AndRule" ? "AND" : "OR";

  const handleKindChange = (next: ConditionKind) => {
    if (next === "historical") {
      onUpdate({
        kind: next,
        historicalConfig: condition.historicalConfig ?? defaultHistoricalConfig(),
        historicalEdited: false,
      });
      return;
    }
    onUpdate({ kind: next, historicalConfig: null, historicalEdited: false });
  };

  const handleProductApply = (selection: ProductSelection) => {
    onUpdate({
      productSelection: selection,
      taxonomyId: condition.taxonomyId || selection.categories[0]?.catalogId || "",
    });
  };

  return (
    <Card className="overflow-hidden">
      <CardHeader className="flex flex-row items-center justify-between gap-2 border-b py-3">
        <div className="flex items-center gap-2">
          {index > 0 && (
            <Badge variant="outline" className="text-xs font-bold">
              {compositeLabel}
            </Badge>
          )}
          <CardTitle className="text-sm font-medium">Condition {index + 1}</CardTitle>
        </div>
        <div className="flex items-center gap-2">
          <ToggleGroup
            type="single"
            size="sm"
            value={condition.kind}
            onValueChange={(v) => v && handleKindChange(v as ConditionKind)}
          >
            <ToggleGroupItem value="standard" aria-label="Standard condition">
              Standard
            </ToggleGroupItem>
            <ToggleGroupItem value="taxonomic" aria-label="Product-based condition">
              Product-based
            </ToggleGroupItem>
            <ToggleGroupItem value="historical" aria-label="Historical condition">
              Historical
            </ToggleGroupItem>
          </ToggleGroup>
          {!isOnlyCondition && (
            <Button
              type="button"
              variant="ghost"
              size="icon"
              onClick={onRemove}
              aria-label={`Remove condition ${index + 1}`}
            >
              <Trash2 className="h-4 w-4" />
            </Button>
          )}
        </div>
      </CardHeader>

      <CardContent className="pt-4">
        {condition.kind === "taxonomic" ? (
          <TaxonomicConditionFields
            condition={condition}
            schemaProperties={schemaProperties}
            onUpdate={onUpdate}
            onOpenProductModal={() => setIsProductModalOpen(true)}
          />
        ) : condition.kind === "historical" ? (
          <HistoricalConditionFields
            condition={condition}
            schemaProperties={schemaProperties}
            pointAccountTypes={pointAccountTypes}
            onUpdate={onUpdate}
          />
        ) : (
          <StandardConditionFields
            condition={condition}
            schemaProperties={schemaProperties}
            pointAccountTypes={pointAccountTypes}
            onUpdate={onUpdate}
            onUpdateSide={onUpdateSide}
          />
        )}
      </CardContent>

      {condition.kind === "taxonomic" ? (
        <ProductSelectorModal
          open={isProductModalOpen}
          onOpenChange={setIsProductModalOpen}
          initialSelection={condition.productSelection ?? undefined}
          onApply={handleProductApply}
          title="Select products"
          description="Choose categories or individual SKUs that trigger this rule."
        />
      ) : null}
    </Card>
  );
}

interface HistoricalConditionFieldsProps {
  condition: Condition;
  schemaProperties: SchemaProperty[];
  pointAccountTypes: Pick<PointAccountType, "id" | "name">[];
  onUpdate: (updates: Partial<Condition>) => void;
}

function HistoricalConditionFields({
  condition,
  schemaProperties,
  pointAccountTypes,
  onUpdate,
}: HistoricalConditionFieldsProps) {
  const config = condition.historicalConfig ?? defaultHistoricalConfig();
  const numericOptions = schemaProperties.filter((p) => p.dataType === "Number" || p.dataType === "Integer");
  const dateOptions = schemaProperties.filter((p) => p.dataType === "Date" || p.dataType === "DateTime");

  const updateConfig = (updates: Partial<typeof config>) => {
    onUpdate({
      historicalConfig: {
        ...config,
        ...updates,
      },
      historicalEdited: true,
    });
  };

  return (
    <div className="space-y-3">
      {condition.historicalRule && !condition.historicalEdited ? (
        <p className="text-xs text-muted-foreground">Using preserved payload until edited.</p>
      ) : null}
      <div className="grid grid-cols-1 gap-3 lg:grid-cols-3">
        <div>
          <Label className="text-xs text-muted-foreground">Aggregate</Label>
          <Select
            value={config.aggregateType}
            onValueChange={(v) => updateConfig({ aggregateType: v as HistoricalAggregateType })}
          >
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {HISTORICAL_AGGREGATES.map((agg) => (
                <SelectItem key={agg} value={agg}>
                  {agg}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div>
          <Label className="text-xs text-muted-foreground">Window (days)</Label>
          <Input
            type="number"
            min={1}
            value={config.windowDays}
            onChange={(e) => updateConfig({ windowDays: Math.max(1, Number(e.target.value || 1)) })}
          />
        </div>
        <div>
          <Label className="text-xs text-muted-foreground">Temporal comparison</Label>
          <Select
            value={config.temporalComparison}
            onValueChange={(v) => updateConfig({ temporalComparison: v as HistoricalTemporalComparison })}
          >
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {HISTORICAL_TEMPORAL_COMPARISONS.map((cmp) => (
                <SelectItem key={cmp} value={cmp}>
                  {cmp}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>
      <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
        <div>
          <Label className="text-xs text-muted-foreground">Value path</Label>
          <Input
            placeholder="event.price"
            value={config.valuePath}
            list={numericOptions.length > 0 ? "historical-value-paths" : undefined}
            onChange={(e) => updateConfig({ valuePath: e.target.value })}
          />
          {numericOptions.length > 0 ? (
            <datalist id="historical-value-paths">
              {numericOptions.map((p) => (
                <option key={p.path} value={`event.${p.path}`}>
                  {p.label}
                </option>
              ))}
            </datalist>
          ) : null}
        </div>
        <div>
          <Label className="text-xs text-muted-foreground">Occurrence path</Label>
          <Input
            placeholder="event.transactionDate"
            value={config.occurrencePath}
            list={dateOptions.length > 0 ? "historical-occurrence-paths" : undefined}
            onChange={(e) => updateConfig({ occurrencePath: e.target.value })}
          />
          {dateOptions.length > 0 ? (
            <datalist id="historical-occurrence-paths">
              {dateOptions.map((p) => (
                <option key={p.path} value={`event.${p.path}`}>
                  {p.label}
                </option>
              ))}
            </datalist>
          ) : null}
        </div>
      </div>
      <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
        <div>
          <Label className="text-xs text-muted-foreground">Threshold provider</Label>
          <Select
            value={config.thresholdProviderType}
            onValueChange={(v) =>
              updateConfig({
                thresholdProviderType: v as "ConstantValueProvider" | "PathValueProvider" | "PointBalanceProvider",
              })
            }
          >
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="ConstantValueProvider">Fixed value</SelectItem>
              <SelectItem value="PathValueProvider">Event property</SelectItem>
              <SelectItem value="PointBalanceProvider">Point balance</SelectItem>
            </SelectContent>
          </Select>
        </div>
        <div>
          <Label className="text-xs text-muted-foreground">Threshold (&gt;=)</Label>
          {config.thresholdProviderType === "ConstantValueProvider" ? (
            <Input
              type="number"
              value={config.thresholdValue}
              onChange={(e) => updateConfig({ thresholdValue: Number(e.target.value || 0) })}
            />
          ) : config.thresholdProviderType === "PathValueProvider" ? (
            <>
              <Input
                placeholder="event.threshold"
                value={config.thresholdPath}
                list={numericOptions.length > 0 ? "historical-threshold-paths" : undefined}
                onChange={(e) => updateConfig({ thresholdPath: e.target.value })}
              />
              {numericOptions.length > 0 ? (
                <datalist id="historical-threshold-paths">
                  {numericOptions.map((p) => (
                    <option key={p.path} value={`event.${p.path}`}>
                      {p.label}
                    </option>
                  ))}
                </datalist>
              ) : null}
            </>
          ) : (
            <Select
              value={config.thresholdPointAccountTypeId}
              onValueChange={(v) => updateConfig({ thresholdPointAccountTypeId: v })}
            >
              <SelectTrigger>
                <SelectValue placeholder="Select point account..." />
              </SelectTrigger>
              <SelectContent>
                {pointAccountTypes.length === 0 ? (
                  <div className="px-2 py-1.5 text-xs text-muted-foreground">No point accounts available</div>
                ) : (
                  pointAccountTypes
                    .filter(
                      (pat): pat is { id: string; name: string } => typeof pat.id === "string" && pat.id.length > 0
                    )
                    .map((pat) => (
                      <SelectItem key={pat.id} value={pat.id}>
                        {pat.name}
                      </SelectItem>
                    ))
                )}
              </SelectContent>
            </Select>
          )}
        </div>
      </div>
      <p className="text-xs text-muted-foreground">
        ELP evaluates historical rules as <strong>left &gt;= right</strong>.
      </p>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Standard condition fields
// ─────────────────────────────────────────────────────────────────────────────

interface StandardConditionFieldsProps {
  condition: Condition;
  schemaProperties: SchemaProperty[];
  pointAccountTypes: Pick<PointAccountType, "id" | "name">[];
  onUpdate: (updates: Partial<Condition>) => void;
  onUpdateSide: (side: "left" | "right", updates: Partial<ConditionSide>) => void;
}

function StandardConditionFields({
  condition,
  schemaProperties,
  pointAccountTypes,
  onUpdate,
  onUpdateSide,
}: StandardConditionFieldsProps) {
  return (
    <div className="grid grid-cols-1 gap-3 lg:grid-cols-[1fr_auto_1fr] lg:items-start">
      <SideEditor
        side={condition.left}
        evaluator={condition.evaluator}
        label="Left value"
        schemaProperties={schemaProperties}
        pointAccountTypes={pointAccountTypes}
        onUpdate={(updates) => onUpdateSide("left", updates)}
      />

      <div className="flex flex-col items-center gap-1.5 lg:pt-6">
        <Label className="text-xs text-muted-foreground">Operator</Label>
        <Select value={condition.evaluator} onValueChange={(v) => onUpdate({ evaluator: v })}>
          <SelectTrigger className="w-full lg:min-w-[200px]">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectGroup>
              <SelectLabel>Numeric</SelectLabel>
              {EVALUATORS.filter((e) => e.class === "numeric").map((e) => (
                <SelectItem key={e.value} value={e.value}>
                  {e.label}
                </SelectItem>
              ))}
            </SelectGroup>
            <SelectGroup>
              <SelectLabel>Text</SelectLabel>
              {EVALUATORS.filter((e) => e.class === "string").map((e) => (
                <SelectItem key={e.value} value={e.value}>
                  {e.label}
                </SelectItem>
              ))}
            </SelectGroup>
            <SelectGroup>
              <SelectLabel>Boolean</SelectLabel>
              {EVALUATORS.filter((e) => e.class === "boolean").map((e) => (
                <SelectItem key={e.value} value={e.value}>
                  {e.label}
                </SelectItem>
              ))}
            </SelectGroup>
          </SelectContent>
        </Select>
      </div>

      <SideEditor
        side={condition.right}
        evaluator={condition.evaluator}
        label="Right value"
        schemaProperties={schemaProperties}
        pointAccountTypes={pointAccountTypes}
        onUpdate={(updates) => onUpdateSide("right", updates)}
      />
    </div>
  );
}

interface SideEditorProps {
  side: ConditionSide;
  evaluator: string;
  label: string;
  schemaProperties: SchemaProperty[];
  pointAccountTypes: Pick<PointAccountType, "id" | "name">[];
  onUpdate: (updates: Partial<ConditionSide>) => void;
}

function SideEditor({ side, evaluator, label, schemaProperties, pointAccountTypes, onUpdate }: SideEditorProps) {
  const evClass = EVALUATOR_BY_VALUE.get(evaluator)?.class;
  const isBoolean = evClass === "boolean";
  const isNumeric = evClass === "numeric";

  return (
    <div className="space-y-2">
      <div>
        <Label className="text-xs text-muted-foreground">{label}</Label>
        <Select
          value={side.providerType}
          onValueChange={(v) => onUpdate({ providerType: v as UiProviderType, productSelection: null })}
        >
          <SelectTrigger>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {STANDARD_PROVIDER_TYPES.map((p) => (
              <SelectItem key={p.value} value={p.value}>
                {p.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {side.providerType === "PointBalanceProvider" ? (
        <Select value={side.pointAccountTypeId} onValueChange={(v) => onUpdate({ pointAccountTypeId: v })}>
          <SelectTrigger>
            <SelectValue placeholder="Select point account..." />
          </SelectTrigger>
          <SelectContent>
            {pointAccountTypes.length === 0 ? (
              <div className="px-2 py-1.5 text-xs text-muted-foreground">No point accounts available</div>
            ) : (
              pointAccountTypes
                .filter((pat): pat is { id: string; name: string } => typeof pat.id === "string" && pat.id.length > 0)
                .map((pat) => (
                  <SelectItem key={pat.id} value={pat.id}>
                    {pat.name}
                  </SelectItem>
                ))
            )}
          </SelectContent>
        </Select>
      ) : side.providerType === "PathValueProvider" ? (
        <Input
          placeholder="payload.spendTotal"
          value={side.propertyPath}
          list={schemaProperties.length > 0 ? `schema-properties-${label}` : undefined}
          onChange={(e) => onUpdate({ propertyPath: e.target.value })}
        />
      ) : side.providerType === "AggregateValueProvider" ? (
        <p className="text-xs text-muted-foreground">
          Aggregate (Sum) is selected. Edit nested aggregation parameters via the JSON view (advanced).
        </p>
      ) : (
        <ConstantInput value={side.constantValue} isBoolean={isBoolean} isNumeric={isNumeric} onUpdate={onUpdate} />
      )}

      {side.providerType === "PathValueProvider" && schemaProperties.length > 0 && (
        <datalist id={`schema-properties-${label}`}>
          {schemaProperties.map((p) => (
            <option key={p.path} value={`payload.${p.path}`}>
              {p.label} ({p.dataType})
            </option>
          ))}
        </datalist>
      )}
    </div>
  );
}

interface ConstantInputProps {
  value: string | number | boolean;
  isBoolean: boolean;
  isNumeric: boolean;
  onUpdate: (updates: Partial<ConditionSide>) => void;
}

function ConstantInput({ value, isBoolean, isNumeric, onUpdate }: ConstantInputProps) {
  if (isBoolean) {
    const checked = typeof value === "boolean" ? value : value === "true";
    return (
      <div className="flex items-center gap-2">
        <Switch checked={checked} onCheckedChange={(v) => onUpdate({ constantValue: v })} />
        <span className="text-sm">{checked ? "True" : "False"}</span>
      </div>
    );
  }
  return (
    <Input
      placeholder="Enter value..."
      type={isNumeric ? "number" : "text"}
      value={typeof value === "boolean" ? "" : String(value)}
      onChange={(e) => {
        const raw = e.target.value;
        const next: string | number = isNumeric && raw !== "" && !Number.isNaN(Number(raw)) ? Number(raw) : raw;
        onUpdate({ constantValue: next });
      }}
    />
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Taxonomic condition fields
// ─────────────────────────────────────────────────────────────────────────────

interface TaxonomicConditionFieldsProps {
  condition: Condition;
  schemaProperties: SchemaProperty[];
  onUpdate: (updates: Partial<Condition>) => void;
  onOpenProductModal: () => void;
}

function TaxonomicConditionFields({
  condition,
  schemaProperties,
  onUpdate,
  onOpenProductModal,
}: TaxonomicConditionFieldsProps) {
  const selection = condition.productSelection;
  const totalCount = (selection?.categories.length ?? 0) + (selection?.entities.length ?? 0);
  const itemPathOptions = schemaProperties.filter((p) => p.dataType === "List" || p.dataType === "Object");

  return (
    <div className="space-y-4">
      <div>
        <Label className="text-xs text-muted-foreground">
          Product / category selection <span className="text-destructive">*</span>
        </Label>
        {selection && totalCount > 0 ? (
          <div className="mt-1.5 flex items-center justify-between rounded-md border bg-muted/30 px-3 py-2.5">
            <div className="flex items-center gap-2">
              <Package className="h-4 w-4 text-muted-foreground" />
              <Badge variant={selection.mode === "include" ? "default" : "destructive"}>
                {selection.mode === "include" ? "Include" : "Exclude"}
              </Badge>
              <span className="text-sm">
                {totalCount} item{totalCount === 1 ? "" : "s"} selected ({selection.categories.length} categories,{" "}
                {selection.entities.length} SKUs)
              </span>
            </div>
            <div className="flex items-center gap-1">
              <Button
                type="button"
                variant="ghost"
                size="icon"
                onClick={onOpenProductModal}
                aria-label="Edit selection"
              >
                <Pencil className="h-4 w-4" />
              </Button>
              <Button
                type="button"
                variant="ghost"
                size="icon"
                onClick={() => onUpdate({ productSelection: null, taxonomyId: "" })}
                aria-label="Clear selection"
              >
                <Trash2 className="h-4 w-4" />
              </Button>
            </div>
          </div>
        ) : (
          <Button type="button" variant="outline" className="mt-1.5 w-full border-dashed" onClick={onOpenProductModal}>
            <Package className="mr-2 h-4 w-4" />
            Select products & categories
          </Button>
        )}
      </div>

      <div className="grid grid-cols-1 gap-3 lg:grid-cols-3">
        <div>
          <Label className="text-xs text-muted-foreground">
            Items array path <span className="text-destructive">*</span>
          </Label>
          <Input
            placeholder="event.items"
            value={condition.itemsPropertyPath}
            list={itemPathOptions.length > 0 ? "taxonomic-item-paths" : undefined}
            onChange={(e) => onUpdate({ itemsPropertyPath: e.target.value })}
          />
          {itemPathOptions.length > 0 && (
            <datalist id="taxonomic-item-paths">
              <option value="event.items">event.items</option>
              {itemPathOptions.map((p) => (
                <option key={p.path} value={`event.${p.path}`}>
                  event.{p.path}
                </option>
              ))}
            </datalist>
          )}
          <p className="mt-1 text-xs text-muted-foreground">Event property containing the array of line items.</p>
        </div>

        <div>
          <Label className="text-xs text-muted-foreground">
            SKU match field <span className="text-destructive">*</span>
          </Label>
          <Input
            placeholder="sku"
            value={condition.keySymbolPath}
            onChange={(e) => onUpdate({ keySymbolPath: e.target.value })}
          />
          <p className="mt-1 text-xs text-muted-foreground">Field on each line item used to match products.</p>
        </div>

        <div>
          <Label className="text-xs text-muted-foreground">Taxonomy type</Label>
          <Select value={condition.taxonomyType} onValueChange={(v) => onUpdate({ taxonomyType: v })}>
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {TAXONOMY_TYPES.map((t) => (
                <SelectItem key={t} value={t}>
                  {t}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>
    </div>
  );
}
