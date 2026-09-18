"use client";

/**
 * DynamicEntityDetails — schema-driven detail layout for a single entity.
 *
 * Reads `LoyaltySchema.attributes`, groups them by `attributeLayoutGroup`, and
 * renders each group in the prescribed slot of the layout grid (header /
 * top-left-split / top-right-split / middle / outcomes).
 *
 * Each `AttributeType` maps to the matching shared view — `JsonTreeView` for
 * raw/dynamic/keyvalue, `FriendlyDataView` for object/list, direct primitive
 * rendering for primitives. No raw `JSON.stringify` fallbacks.
 *
 * Domain-specific panels (point balances, campaign progress, eventable models)
 * are composed AROUND this component at the page level — this stays generic so
 * it works for any schema.
 */

import { useMemo, type ReactNode } from "react";
import { ChevronDown } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from "@/components/ui/collapsible";
import { FriendlyDataView } from "@/components/shared/friendly-data-view";
import { JsonTreeView } from "@/components/shared/json-tree-view";
import {
  CURATED_ATTRIBUTE_LAYOUT_GROUPS,
  type LoyaltySchema,
  type SchemaAttribute,
} from "@/lib/loyalty-schema-types";
import { cn } from "@/lib/utils";
import { formatValueAsString, getNestedValue, getSchemaFieldValue } from "@/services/loyalty/utils/grid-columns";
import { getSchemaDisplayName } from "@/services/loyalty/utils/display-labels";

import { SimpleDataTable } from "./simple-data-table";

// A renderable attribute always has a symbol — it is the key we read from the entity.
type Attribute = SchemaAttribute & {
  symbol: string;
  restrictions?: Array<{ type: string }> | null;
};

const LAYOUT_GROUPS = CURATED_ATTRIBUTE_LAYOUT_GROUPS as readonly string[];

type LayoutGroupId = (typeof CURATED_ATTRIBUTE_LAYOUT_GROUPS)[number];

export interface DynamicEntityDetailsProps {
  schema: LoyaltySchema;
  entity: Record<string, unknown>;
  /** Show the built-in model title/description header block. */
  showTitle?: boolean;
  /** Map of `modelId → schema` for nested ModelObject / ModelList rendering. */
  relatedSchemas?: Map<string, LoyaltySchema>;
  /** Map of attribute symbol → array of related rows for ModelList. */
  relatedData?: Map<string, unknown[]>;
  /** Optional slot rendered beside the header group. */
  headerSlot?: ReactNode;
  /** Optional slot rendered after the layout groups. */
  footerSlot?: ReactNode;
}

function PrimitiveValue({ value, dataType }: { value: unknown; dataType: string }) {
  if (value === null || value === undefined) {
    return <span className="text-muted-foreground italic">—</span>;
  }
  if ((dataType ?? "").toLowerCase() === "boolean" || (dataType ?? "").toLowerCase() === "bool") {
    return (
      <span className={value ? "text-green-700 dark:text-green-400" : "text-red-600 dark:text-red-400"}>
        {value ? "Yes" : "No"}
      </span>
    );
  }
  return <span>{formatValueAsString(value, dataType)}</span>;
}

function AttributeField({ attribute, value }: { attribute: Attribute; value: unknown }) {
  const isRequired = attribute.restrictions?.some((r) => r.type === "Required") ?? false;
  const isMultiLine = attribute.attributeLayoutHeight === "multi";

  return (
    <div className={cn("px-6 py-4", isMultiLine ? "space-y-2" : "grid grid-cols-1 md:grid-cols-3 gap-4")}>
      <div className={isMultiLine ? "" : "md:col-span-1"}>
        <label className="block text-sm font-medium text-foreground">
          {attribute.displayName ?? attribute.symbol}
          {isRequired && <span className="text-destructive ml-1">*</span>}
          <span className="block text-xs text-muted-foreground mt-1">{attribute.dataType ?? ""}</span>
        </label>
      </div>
      <div className={cn(isMultiLine ? "" : "md:col-span-2", "text-sm")}>
        <PrimitiveValue value={value} dataType={attribute.dataType ?? "string"} />
      </div>
    </div>
  );
}

function NestedModelGrid({
  attribute,
  relatedSchema,
  rows,
}: {
  attribute: Attribute;
  relatedSchema: LoyaltySchema;
  rows: unknown[];
}) {
  return (
    <div className="px-6 py-4 space-y-2">
      <label className="block text-sm font-medium text-foreground">{attribute.displayName ?? attribute.symbol}</label>
      <SimpleDataTable schema={relatedSchema} data={rows} />
    </div>
  );
}

function NestedModelObject({
  attribute,
  value,
  relatedSchema,
}: {
  attribute: Attribute;
  value: unknown;
  relatedSchema: LoyaltySchema;
}) {
  return (
    <Collapsible defaultOpen={false} className="border-t">
      <CollapsibleTrigger asChild>
        <Button variant="ghost" className="w-full justify-between px-6 py-4 rounded-none h-auto">
          <span className="text-sm font-semibold">{attribute.displayName ?? attribute.symbol}</span>
          <ChevronDown className="h-4 w-4 transition-transform data-[state=open]:rotate-180" aria-hidden="true" />
        </Button>
      </CollapsibleTrigger>
      <CollapsibleContent>
        <div className="divide-y">
          {((relatedSchema.attributes ?? []) as Attribute[])
            .filter((a) => a.status === "Live" && a.isDetailsViewable !== false)
            .map((a) => (
              <AttributeField key={a.symbol} attribute={a} value={getNestedValue(value, a.symbol)} />
            ))}
        </div>
      </CollapsibleContent>
    </Collapsible>
  );
}

function LabelledValue({ attribute, children }: { attribute: Attribute; children: ReactNode }) {
  return (
    <div className="px-6 py-4 space-y-2">
      <label className="block text-sm font-medium text-foreground">{attribute.displayName ?? attribute.symbol}</label>
      {children}
    </div>
  );
}

function renderAttribute(
  attr: Attribute,
  entity: Record<string, unknown>,
  relatedSchemas: Map<string, LoyaltySchema>,
  relatedData: Map<string, unknown[]>
): ReactNode {
  const value = getSchemaFieldValue(entity, attr.symbol);

  switch (attr.type) {
    case "Primitive":
      return <AttributeField attribute={attr} value={value} />;

    case "ModelList": {
      const related = attr.modelId ? relatedSchemas.get(attr.modelId) : undefined;
      const inlineRows = Array.isArray(value) ? value : [];
      const resolvedRelatedRows = relatedData.get(attr.symbol);
      const rows =
        Array.isArray(resolvedRelatedRows) && resolvedRelatedRows.length > 0 ? resolvedRelatedRows : inlineRows;
      if (related) {
        return <NestedModelGrid attribute={attr} relatedSchema={related} rows={rows} />;
      }
      return (
        <LabelledValue attribute={attr}>
          <FriendlyDataView value={value} />
        </LabelledValue>
      );
    }

    case "ModelObject": {
      const related = attr.modelId ? relatedSchemas.get(attr.modelId) : undefined;
      if (related && value && typeof value === "object") {
        return <NestedModelObject attribute={attr} value={value} relatedSchema={related} />;
      }
      return (
        <LabelledValue attribute={attr}>
          <FriendlyDataView value={value} />
        </LabelledValue>
      );
    }

    case "ModelKeyValue":
    case "ModelDynamic":
    case "ModelRawJson":
      return (
        <LabelledValue attribute={attr}>
          <JsonTreeView value={value} />
        </LabelledValue>
      );

    default:
      return <AttributeField attribute={attr} value={value} />;
  }
}

function LayoutGroup({ children }: { children: ReactNode }) {
  return (
    <Card className="overflow-hidden">
      <div className="divide-y">{children}</div>
    </Card>
  );
}

function resolveAttributeLayoutGroup(attribute: Attribute): LayoutGroupId {
  const groupId = attribute.attributeLayoutGroup as LayoutGroupId | undefined;
  if (groupId && LAYOUT_GROUPS.includes(groupId)) return groupId;
  return "middle-panel";
}

export function DynamicEntityDetails({
  schema,
  entity,
  showTitle = true,
  relatedSchemas = new Map(),
  relatedData = new Map(),
  headerSlot,
  footerSlot,
}: DynamicEntityDetailsProps) {
  const grouped = useMemo(() => {
    const map = new Map<LayoutGroupId, Attribute[]>();
    for (const a of (schema.attributes ?? []) as Attribute[]) {
      if (!a.symbol || a.isDetailsViewable === false || a.status !== "Live") continue;
      const groupId = resolveAttributeLayoutGroup(a);
      const arr = map.get(groupId) ?? [];
      arr.push(a);
      map.set(groupId, arr);
    }
    for (const [, arr] of map) {
      arr.sort((a, b) => (a.attributeLayoutPosition ?? 999) - (b.attributeLayoutPosition ?? 999));
    }
    return map;
  }, [schema]);

  const renderGroup = (groupId: LayoutGroupId): ReactNode => {
    const attrs = grouped.get(groupId);
    if (!attrs?.length) return null;
    return (
      <LayoutGroup>
        {attrs.map((attr) => (
          <div key={attr.symbol}>{renderAttribute(attr, entity, relatedSchemas, relatedData)}</div>
        ))}
      </LayoutGroup>
    );
  };

  const headerGroup = renderGroup("header-panel");
  const tlGroup = renderGroup("top-left-split-panel");
  const trGroup = renderGroup("top-right-split-panel");
  const middleGroup = renderGroup("middle-panel");
  // Attributes with a missing or non-curated layout group fall back to
  // `middle-panel` so payload fields stay visible when layout metadata is unset.
  const outcomesGroup = renderGroup("outcomes");
  const modelName = getSchemaDisplayName(schema);

  return (
    <div className="space-y-6">
      {showTitle && (
        <div className="border-b pb-4">
          <h2 className="text-2xl font-bold">{modelName} details</h2>
          <p className="text-sm text-muted-foreground mt-1">
            View detailed information about this {modelName.toLowerCase()}.
          </p>
        </div>
      )}

      {(headerGroup || headerSlot) && (
        <div className="flex flex-col lg:flex-row gap-6">
          <div className="flex-1 min-w-0">
            {headerGroup ?? (
              <p className="text-sm text-muted-foreground italic">No attributes assigned to header panel.</p>
            )}
          </div>
          {headerSlot && <div className="w-full lg:w-[400px] shrink-0">{headerSlot}</div>}
        </div>
      )}

      {(tlGroup || trGroup) && (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          {tlGroup}
          {trGroup}
        </div>
      )}

      {middleGroup}

      {outcomesGroup}

      {footerSlot}

      <div className="rounded-lg border bg-muted/20 p-4">
        <h3 className="text-sm font-semibold mb-2">Model information</h3>
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
          <div>
            <span className="text-muted-foreground">Model:</span>
            <p className="font-medium">{modelName}</p>
          </div>
          <div>
            <span className="text-muted-foreground">Model type:</span>
            <p className="font-medium">{schema.modelType ?? "—"}</p>
          </div>
          <div>
            <span className="text-muted-foreground">Status:</span>
            <p className="font-medium">{schema.status ?? "—"}</p>
          </div>
          <div>
            <span className="text-muted-foreground">Version:</span>
            <p className="font-medium">{schema.modelVersion ?? "—"}</p>
          </div>
        </div>
      </div>
    </div>
  );
}
