# Task 2 uncommitted diff (no commits)

```diff
diff --git a/Journeys/Journeys.UX/src/app/loyalty/accounts/page.tsx b/Journeys/Journeys.UX/src/app/loyalty/accounts/page.tsx
index 39bc828..ddf609a 100644
--- a/Journeys/Journeys.UX/src/app/loyalty/accounts/page.tsx
+++ b/Journeys/Journeys.UX/src/app/loyalty/accounts/page.tsx
@@ -1,51 +1,17 @@
-import { queryData } from "@/services/loyalty/actions";
-import { loyaltyScreenModel } from "@/lib/loyalty-model";
-import { attributeNamesFromRows } from "@/services/loyalty/parse-list";
+import { Suspense } from "react";
 
-function formatFetchError(error?: string): string {
-  const e = error ?? "";
-  if (/401|403|unauthorized|forbidden|nope/i.test(e)) {
-    return "not authorized / check tenant or key";
-  }
-  return e || "Unknown error";
-}
-
-export default async function AccountsPage() {
-  const screen = loyaltyScreenModel("accounts");
-  const result = await queryData(screen.schemaName!);
-
-  const attributes =
-    result.success && result.data && result.data.length > 0
-      ? attributeNamesFromRows(result.data)
-      : [{ name: "id" }, { name: "name" }];
+import { LoyaltyAccountsListClient } from "@/components/loyalty/accounts";
+import { Skeleton } from "@/components/ui/skeleton";
 
+export default function AccountsPage() {
   return (
-    <>
-      <h1>Accounts</h1>
-      {!result.success ? (
-        <p className="error">{formatFetchError(result.error)}</p>
-      ) : result.data!.length === 0 ? (
-        <p className="empty">No loyalty accounts found.</p>
-      ) : (
-        <table>
-          <thead>
-            <tr>
-              {attributes.map((attr, index) => (
-                <th key={attr.name ?? index}>{attr.name}</th>
-              ))}
-            </tr>
-          </thead>
-          <tbody>
-            {result.data!.map((row, rowIndex) => (
-              <tr key={String(row.id ?? rowIndex)}>
-                {attributes.map((attr, colIndex) => (
-                  <td key={attr.name ?? colIndex}>{String(row[attr.name ?? ""] ?? "")}</td>
-                ))}
-              </tr>
-            ))}
-          </tbody>
-        </table>
-      )}
-    </>
+    <div className="space-y-6">
+      <h1 className="text-2xl font-semibold tracking-tight">Accounts</h1>
+      {/* The list reads `account` search params, so it needs its own boundary
+          to keep the rest of the route prerenderable. */}
+      <Suspense fallback={<Skeleton className="h-64 w-full" />}>
+        <LoyaltyAccountsListClient />
+      </Suspense>
+    </div>
   );
 }
diff --git a/Journeys/Journeys.UX/src/lib/loyalty-schema-types.ts b/Journeys/Journeys.UX/src/lib/loyalty-schema-types.ts
new file mode 100644
index 0000000..e06a50a
--- /dev/null
+++ b/Journeys/Journeys.UX/src/lib/loyalty-schema-types.ts
@@ -0,0 +1,67 @@
+/**
+ * Local schema/attribute shapes for the schema-driven loyalty tables and detail
+ * views. These describe only the fields those components actually read; the
+ * index signatures keep the rest of the API payload addressable without
+ * modelling every field the model service can emit.
+ */
+
+export type SchemaAttribute = {
+  symbol?: string;
+  name?: string;
+  displayName?: string;
+  dataType?: string;
+  type?: string;
+  attributeType?: string;
+  status?: string;
+  isRequired?: boolean;
+  /** Grid placement (`grid-columns.ts`). */
+  isInGrid?: boolean;
+  gridColumnNumber?: number;
+  isGridSortable?: boolean;
+  defaultSortDirection?: "asc" | "desc";
+  /** Detail layout placement (`DynamicEntityDetails`). */
+  attributeLayoutGroup?: string;
+  attributeLayoutPosition?: number;
+  attributeLayoutHeight?: string;
+  isDetailsViewable?: boolean;
+  /** Related model for ModelObject / ModelList attributes. */
+  modelId?: string;
+  [key: string]: unknown;
+};
+
+export type LoyaltySchema = {
+  id?: string;
+  name?: string;
+  status?: string;
+  tag?: string;
+  modelType?: string;
+  attributes?: SchemaAttribute[];
+  [key: string]: unknown;
+};
+
+export type JsonValue = string | number | boolean | null | JsonValue[] | { [key: string]: JsonValue };
+
+export type PaginationMeta = {
+  page: number;
+  limit: number;
+  total: number;
+  totalPages: number;
+  hasNext: boolean;
+  hasPrev: boolean;
+};
+
+export type PaginationParams = {
+  page?: number;
+  limit?: number;
+  sortBy?: string;
+  sortOrder?: "asc" | "desc";
+};
+
+/** Canonical detail-layout groups, in render order. */
+export const CURATED_ATTRIBUTE_LAYOUT_GROUPS = [
+  "header-panel",
+  "top-left-split-panel",
+  "top-right-split-panel",
+  "middle-panel",
+  "outcomes",
+] as const;
diff --git a/Journeys/Journeys.UX/src/services/loyalty/utils/account-identifiers.ts b/Journeys/Journeys.UX/src/services/loyalty/utils/account-identifiers.ts
new file mode 100644
index 0000000..b1d2e8d
--- /dev/null
+++ b/Journeys/Journeys.UX/src/services/loyalty/utils/account-identifiers.ts
@@ -0,0 +1,93 @@
+export const LOYALTY_ACCOUNT_IDENTIFIER_QUERY =
+  "c.event.id = @id OR c.id = @id OR c.event.loyaltyAccountId = @id OR c.loyaltyAccountId = @id OR c.event.profileid = @id OR c.profileid = @id OR c.event.profileId = @id OR c.profileId = @id OR c.event.customerid = @id OR c.customerid = @id OR c.event.customerId = @id OR c.customerId = @id OR c.event.extAccountId = @id OR c.extAccountId = @id";
+
+export const LOYALTY_ACCOUNT_IDENTIFIER_SEARCH_FIELDS = [
+  "id",
+  "loyaltyAccountId",
+  "loyaltyMemberId",
+  "sourceRecordId",
+  "profileid",
+  "profileId",
+  "customerid",
+  "customerId",
+  "extAccountId",
+];
+
+type LooseRecord = Record<string, unknown>;
+
+function asLooseRecord(value: unknown): LooseRecord | null {
+  return value && typeof value === "object" && !Array.isArray(value) ? (value as LooseRecord) : null;
+}
+
+function asNonEmptyString(value: unknown): string | undefined {
+  return typeof value === "string" && value.length > 0 ? value : undefined;
+}
+
+export function normalizeEntityWithEvent(entity: LooseRecord | null | undefined): LooseRecord | null {
+  const record = asLooseRecord(entity);
+  if (!record) return null;
+
+  const event = asLooseRecord(record["event"]);
+  if (!event) return record;
+
+  const { event: _event, ...rootFields } = record;
+  void _event;
+
+  // Preserve root-level fields while letting event payload shape drive display fields.
+  // Keep document id stable for downstream APIs that key by account id.
+  const documentId = asNonEmptyString(rootFields["id"]);
+  const eventId = asNonEmptyString(event["id"]);
+  const merged: LooseRecord = { ...rootFields, ...event };
+  if (documentId) {
+    merged["id"] = documentId;
+    merged["_documentId"] = documentId;
+  }
+  if (eventId && eventId !== documentId && !asNonEmptyString(merged["eventId"])) {
+    merged["eventId"] = eventId;
+  }
+  return merged;
+}
+
+export function resolveLoyaltyAccountId(entity: LooseRecord | null | undefined, fallbackId: string): string {
+  const record = asLooseRecord(entity);
+  if (!record) return fallbackId;
+
+  const event = asLooseRecord(record["event"]);
+
+  return (
+    asNonEmptyString(record["loyaltyAccountId"]) ??
+    asNonEmptyString(event?.["loyaltyAccountId"]) ??
+    asNonEmptyString(record["customerid"]) ??
+    asNonEmptyString(event?.["customerid"]) ??
+    asNonEmptyString(record["customerId"]) ??
+    asNonEmptyString(event?.["customerId"]) ??
+    asNonEmptyString(record["_documentId"]) ??
+    asNonEmptyString(record["id"]) ??
+    asNonEmptyString(record["extAccountId"]) ??
+    asNonEmptyString(event?.["extAccountId"]) ??
+    asNonEmptyString(event?.["id"]) ??
+    fallbackId
+  );
+}
+
+export function resolveLoyaltyAccountXReference(entity: LooseRecord | null | undefined, fallbackId: string): string {
+  const record = asLooseRecord(entity);
+  if (!record) return fallbackId;
+
+  const event = asLooseRecord(record["event"]);
+  const loyaltyAccountId = resolveLoyaltyAccountId(record, fallbackId);
+
+  return (
+    asNonEmptyString(event?.["profileid"]) ??
+    asNonEmptyString(record["profileid"]) ??
+    asNonEmptyString(event?.["profileId"]) ??
+    asNonEmptyString(record["profileId"]) ??
+    asNonEmptyString(event?.["customerid"]) ??
+    asNonEmptyString(record["customerid"]) ??
+    asNonEmptyString(event?.["customerId"]) ??
+    asNonEmptyString(record["customerId"]) ??
+    asNonEmptyString(event?.["extAccountId"]) ??
+    asNonEmptyString(record["extAccountId"]) ??
+    loyaltyAccountId
+  );
+}
diff --git a/Journeys/Journeys.UX/src/services/loyalty/utils/account-identifiers.test.ts b/Journeys/Journeys.UX/src/services/loyalty/utils/account-identifiers.test.ts
new file mode 100644
index 0000000..b31571a
--- /dev/null
+++ b/Journeys/Journeys.UX/src/services/loyalty/utils/account-identifiers.test.ts
@@ -0,0 +1,39 @@
+import { describe, expect, it } from "vitest";
+
+import {
+  LOYALTY_ACCOUNT_IDENTIFIER_QUERY,
+  resolveLoyaltyAccountId,
+  resolveLoyaltyAccountXReference,
+} from "./account-identifiers";
+
+describe("loyalty account identifiers", () => {
+  it("searches every account id field used by the account pickers", () => {
+    expect(LOYALTY_ACCOUNT_IDENTIFIER_QUERY).toContain("c.event.id = @id");
+    expect(LOYALTY_ACCOUNT_IDENTIFIER_QUERY).toContain("c.id = @id");
+    expect(LOYALTY_ACCOUNT_IDENTIFIER_QUERY).toContain("c.event.loyaltyAccountId = @id");
+    expect(LOYALTY_ACCOUNT_IDENTIFIER_QUERY).toContain("c.loyaltyAccountId = @id");
+    expect(LOYALTY_ACCOUNT_IDENTIFIER_QUERY).toContain("c.event.profileid = @id");
+    expect(LOYALTY_ACCOUNT_IDENTIFIER_QUERY).toContain("c.profileid = @id");
+    expect(LOYALTY_ACCOUNT_IDENTIFIER_QUERY).toContain("c.event.profileId = @id");
+    expect(LOYALTY_ACCOUNT_IDENTIFIER_QUERY).toContain("c.profileId = @id");
+    expect(LOYALTY_ACCOUNT_IDENTIFIER_QUERY).toContain("c.event.customerid = @id");
+    expect(LOYALTY_ACCOUNT_IDENTIFIER_QUERY).toContain("c.customerid = @id");
+    expect(LOYALTY_ACCOUNT_IDENTIFIER_QUERY).toContain("c.event.customerId = @id");
+    expect(LOYALTY_ACCOUNT_IDENTIFIER_QUERY).toContain("c.customerId = @id");
+    expect(LOYALTY_ACCOUNT_IDENTIFIER_QUERY).toContain("c.event.extAccountId = @id");
+    expect(LOYALTY_ACCOUNT_IDENTIFIER_QUERY).toContain("c.extAccountId = @id");
+  });
+
+  it("prefers external profile/customer ids for display while preserving the account id fallback", () => {
+    const entity = {
+      id: "document-1",
+      loyaltyAccountId: "loyalty-account-1",
+      event: {
+        profileid: "external-profile-1",
+      },
+    };
+
+    expect(resolveLoyaltyAccountId(entity, "fallback-id")).toBe("loyalty-account-1");
+    expect(resolveLoyaltyAccountXReference(entity, "fallback-id")).toBe("external-profile-1");
+  });
+});
diff --git a/Journeys/Journeys.UX/src/services/loyalty/utils/grid-columns.ts b/Journeys/Journeys.UX/src/services/loyalty/utils/grid-columns.ts
new file mode 100644
index 0000000..f65f589
--- /dev/null
+++ b/Journeys/Journeys.UX/src/services/loyalty/utils/grid-columns.ts
@@ -0,0 +1,151 @@
+/**
+ * Shared utility for generating grid columns from schema attributes.
+ * Used by the dynamic data table, the accounts selection modal, and the
+ * schema-driven entity detail views.
+ */
+
+import type { ReactNode } from "react";
+import type { LoyaltySchema as Schema, SchemaAttribute } from "@/lib/loyalty-schema-types";
+
+// The attribute shape the grid helpers read. `symbol` is required here because a
+// column without a key cannot be rendered.
+type Attribute = SchemaAttribute & {
+  symbol: string;
+};
+
+/**
+ * Get value from nested object using dot notation.
+ * @example getNestedValue(row, "event.customerId")
+ */
+export function getNestedValue(obj: unknown, path: string): unknown {
+  return path.split(".").reduce((current: unknown, key: string) => {
+    if (current && typeof current === "object" && key in current) {
+      return (current as Record<string, unknown>)[key];
+    }
+    return undefined;
+  }, obj);
+}
+
+function getCaseInsensitiveKey(record: Record<string, unknown>, key: string): string | undefined {
+  const lowered = key.toLowerCase();
+  return Object.keys(record).find((candidate) => candidate.toLowerCase() === lowered);
+}
+
+/**
+ * Read a schema-defined field from a dynamic row.
+ *
+ * Event payloads have historically mixed camelCase and lower-case symbols.
+ * Prefer exact schema matches, then fall back to case-insensitive keys so
+ * configured detail fields render existing data instead of empty dashes.
+ */
+export function getSchemaFieldValue(obj: unknown, path: string): unknown {
+  const exactValue = getNestedValue(obj, path);
+  if (exactValue !== undefined) return exactValue;
+
+  return path.split(".").reduce((current: unknown, key: string) => {
+    if (!current || typeof current !== "object" || Array.isArray(current)) {
+      return undefined;
+    }
+
+    const record = current as Record<string, unknown>;
+    const resolvedKey = key in record ? key : getCaseInsensitiveKey(record, key);
+    return resolvedKey ? record[resolvedKey] : undefined;
+  }, obj);
+}
+
+/**
+ * Format a value as a string based on the schema-declared data type.
+ * Handles Date, Number, Boolean, and falls through to String() for everything else.
+ */
+export function formatValueAsString(value: unknown, dataType: string | null | undefined): string {
+  if (value === null || value === undefined) return "";
+
+  switch ((dataType ?? "string").toLowerCase()) {
+    case "date":
+    case "datetime":
+      try {
+        return new Date(value as string | number | Date).toLocaleDateString();
+      } catch {
+        return String(value);
+      }
+    case "number":
+    case "int":
+    case "double":
+      return typeof value === "number" ? value.toLocaleString() : String(value);
+    case "boolean":
+    case "bool":
+      return value ? "Yes" : "No";
+    default:
+      return String(value);
+  }
+}
+
+/** Column definition consumed by data tables. */
+export interface GridColumn<T = unknown> {
+  key: string;
+  label: string;
+  sortable?: boolean;
+  dataType: string;
+  render?: (item: T) => ReactNode;
+}
+
+/**
+ * Filter and sort schema attributes for display in a grid.
+ * - Prefer explicitly configured Primitive attributes, even when the attribute
+ *   status is Draft. Some live tenant models store admin UI placement on Draft
+ *   attributes while the parent model is Live.
+ * - Otherwise show Live Primitive attributes
+ * - Sort by `gridColumnNumber` ascending
+ */
+export function getGridAttributes(attributes: Attribute[]): Attribute[] {
+  const primitiveAttributes = attributes.filter((attr) => attr.type === "Primitive");
+
+  const gridAttributes = primitiveAttributes.filter((attr) => attr.gridColumnNumber != null || attr.isInGrid === true);
+
+  const livePrimitiveAttributes = primitiveAttributes.filter((attr) => attr.status === "Live");
+  const displayAttributes = gridAttributes.length > 0 ? gridAttributes : livePrimitiveAttributes;
+
+  return [...displayAttributes].sort((a, b) => {
+    if (a.gridColumnNumber != null && b.gridColumnNumber != null) {
+      return a.gridColumnNumber - b.gridColumnNumber;
+    }
+    if (a.gridColumnNumber != null) return -1;
+    if (b.gridColumnNumber != null) return 1;
+    return 0;
+  });
+}
+
+/** Fallback ID column when no attributes are configured for grid display. */
+const FALLBACK_ID_COLUMN: GridColumn = {
+  key: "id",
+  label: "ID",
+  sortable: true,
+  dataType: "String",
+};
+
+/** Generate column definitions from a schema's attribute list. */
+export function generateColumnsFromSchema(schema: Schema): GridColumn[] {
+  const sortedAttributes = getGridAttributes((schema.attributes ?? []) as Attribute[]);
+
+  if (sortedAttributes.length === 0) return [FALLBACK_ID_COLUMN];
+
+  return sortedAttributes.map((attr) => ({
+    key: attr.symbol,
+    label: attr.displayName ?? attr.symbol,
+    sortable: attr.isGridSortable !== false,
+    dataType: attr.dataType ?? "String",
+  }));
+}
+
+/** Get the initial sort configuration from a schema (first attribute with defaultSortDirection). */
+export function getInitialSortFromSchema(schema: Schema): { key: string; order: "asc" | "desc" } {
+  const sortableAttr = getGridAttributes((schema.attributes ?? []) as Attribute[]).find(
+    (attr) => attr.isGridSortable !== false && attr.defaultSortDirection
+  );
+
+  if (sortableAttr?.defaultSortDirection) {
+    return { key: sortableAttr.symbol, order: sortableAttr.defaultSortDirection };
+  }
+
+  return { key: "", order: "asc" };
+}
diff --git a/Journeys/Journeys.UX/src/services/loyalty/utils/grid-columns.test.ts b/Journeys/Journeys.UX/src/services/loyalty/utils/grid-columns.test.ts
new file mode 100644
index 0000000..576fdbf
--- /dev/null
+++ b/Journeys/Journeys.UX/src/services/loyalty/utils/grid-columns.test.ts
@@ -0,0 +1,108 @@
+import { describe, expect, it } from "vitest";
+
+import { generateColumnsFromSchema, getInitialSortFromSchema, getSchemaFieldValue } from "./grid-columns";
+
+function schemaWithAttributes(attributes: unknown[]) {
+  return {
+    id: "schema-1",
+    name: "LoyaltyAccountDetails",
+    attributes,
+  } as never;
+}
+
+describe("generateColumnsFromSchema", () => {
+  it("uses configured grid attributes even when attribute status is Draft", () => {
+    const columns = generateColumnsFromSchema(
+      schemaWithAttributes([
+        {
+          type: "Primitive",
+          symbol: "email",
+          status: "Draft",
+          dataType: "String",
+          displayName: "Email",
+          isInGrid: true,
+          gridColumnNumber: 3,
+        },
+        {
+          type: "Primitive",
+          symbol: "profileid",
+          status: "Draft",
+          dataType: "String",
+          displayName: "Profile ID",
+          isInGrid: true,
+          gridColumnNumber: 0,
+        },
+        {
+          type: "Primitive",
+          symbol: "internalnote",
+          status: "Draft",
+          dataType: "String",
+          displayName: "Internal note",
+        },
+      ])
+    );
+
+    expect(columns.map((column) => column.key)).toEqual(["profileid", "email"]);
+    expect(columns.map((column) => column.label)).toEqual(["Profile ID", "Email"]);
+  });
+
+  it("falls back to live primitive attributes when no grid attributes are configured", () => {
+    const columns = generateColumnsFromSchema(
+      schemaWithAttributes([
+        {
+          type: "Primitive",
+          symbol: "firstName",
+          status: "Live",
+          dataType: "String",
+          displayName: "First name",
+        },
+        {
+          type: "Primitive",
+          symbol: "draftOnly",
+          status: "Draft",
+          dataType: "String",
+          displayName: "Draft only",
+        },
+      ])
+    );
+
+    expect(columns.map((column) => column.key)).toEqual(["firstName"]);
+  });
+});
+
+describe("getInitialSortFromSchema", () => {
+  it("uses configured draft grid attributes for initial sort", () => {
+    const sort = getInitialSortFromSchema(
+      schemaWithAttributes([
+        {
+          type: "Primitive",
+          symbol: "profileid",
+          status: "Draft",
+          dataType: "String",
+          displayName: "Profile ID",
+          isInGrid: true,
+          gridColumnNumber: 0,
+          isGridSortable: true,
+          defaultSortDirection: "asc",
+        },
+      ])
+    );
+
+    expect(sort).toEqual({ key: "profileid", order: "asc" });
+  });
+});
+
+describe("getSchemaFieldValue", () => {
+  it("reads exact and case-insensitive dynamic payload fields", () => {
+    const row = {
+      event: {
+        orderid: "wxyz_101",
+        ProfileID: "test_001",
+      },
+    };
+
+    expect(getSchemaFieldValue(row, "event.orderid")).toBe("wxyz_101");
+    expect(getSchemaFieldValue(row, "event.orderId")).toBe("wxyz_101");
+    expect(getSchemaFieldValue(row, "event.profileId")).toBe("test_001");
+  });
+});
diff --git a/Journeys/Journeys.UX/src/services/loyalty/eventable-schema.ts b/Journeys/Journeys.UX/src/services/loyalty/eventable-schema.ts
new file mode 100644
index 0000000..d971c21
--- /dev/null
+++ b/Journeys/Journeys.UX/src/services/loyalty/eventable-schema.ts
@@ -0,0 +1,7 @@
+import type { LoyaltySchema } from "@/lib/loyalty-schema-types";
+import { LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME } from "@/services/loyalty/schema-names";
+
+/** A schema whose rows are account-scoped events we can show inline on a detail page. */
+export function isEventableSchema(schema: LoyaltySchema): boolean {
+  return schema.tag === "eventable" || schema.name === LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME;
+}
diff --git a/Journeys/Journeys.UX/src/services/loyalty/eventable-schema.test.ts b/Journeys/Journeys.UX/src/services/loyalty/eventable-schema.test.ts
new file mode 100644
index 0000000..0578e35
--- /dev/null
+++ b/Journeys/Journeys.UX/src/services/loyalty/eventable-schema.test.ts
@@ -0,0 +1,18 @@
+import { describe, expect, it } from "vitest";
+
+import { isEventableSchema } from "./eventable-schema";
+
+describe("isEventableSchema", () => {
+  it("accepts schemas tagged eventable", () => {
+    expect(isEventableSchema({ name: "OrderPlaced", tag: "eventable" })).toBe(true);
+  });
+
+  it("accepts the loyalty account details schema regardless of tag", () => {
+    expect(isEventableSchema({ name: "LoyaltyAccountDetails" })).toBe(true);
+  });
+
+  it("rejects other schemas", () => {
+    expect(isEventableSchema({ name: "OrderPlaced", tag: "lookup" })).toBe(false);
+    expect(isEventableSchema({})).toBe(false);
+  });
+});
diff --git a/Journeys/Journeys.UX/src/components/ui/table.tsx b/Journeys/Journeys.UX/src/components/ui/table.tsx
new file mode 100644
index 0000000..f265bfc
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/ui/table.tsx
@@ -0,0 +1,75 @@
+"use client";
+
+import * as React from "react";
+
+import { cn } from "@/lib/utils";
+
+function Table({ className, ...props }: React.ComponentProps<"table">) {
+  return (
+    <div data-slot="table-container" className="relative w-full overflow-x-auto">
+      <table data-slot="table" className={cn("w-full caption-bottom text-sm", className)} {...props} />
+    </div>
+  );
+}
+
+function TableHeader({ className, ...props }: React.ComponentProps<"thead">) {
+  return <thead data-slot="table-header" className={cn("bg-muted/40 [&_tr]:border-b", className)} {...props} />;
+}
+
+function TableBody({ className, ...props }: React.ComponentProps<"tbody">) {
+  return <tbody data-slot="table-body" className={cn("[&_tr:last-child]:border-0", className)} {...props} />;
+}
+
+function TableFooter({ className, ...props }: React.ComponentProps<"tfoot">) {
+  return (
+    <tfoot
+      data-slot="table-footer"
+      className={cn("border-t bg-muted/50 font-medium [&>tr]:last:border-b-0", className)}
+      {...props}
+    />
+  );
+}
+
+function TableRow({ className, ...props }: React.ComponentProps<"tr">) {
+  return (
+    <tr
+      data-slot="table-row"
+      className={cn("border-b transition-colors hover:bg-muted/50 data-[state=selected]:bg-muted", className)}
+      {...props}
+    />
+  );
+}
+
+function TableHead({ className, ...props }: React.ComponentProps<"th">) {
+  return (
+    <th
+      data-slot="table-head"
+      className={cn(
+        "h-11 px-4 text-left align-middle text-xs font-semibold uppercase tracking-wider whitespace-nowrap text-muted-foreground [&:has([role=checkbox])]:pr-0 [&>[role=checkbox]]:translate-y-[2px]",
+        className
+      )}
+      {...props}
+    />
+  );
+}
+
+function TableCell({ className, ...props }: React.ComponentProps<"td">) {
+  return (
+    <td
+      data-slot="table-cell"
+      className={cn(
+        "px-4 py-3 align-middle whitespace-nowrap [&:has([role=checkbox])]:pr-0 [&>[role=checkbox]]:translate-y-[2px]",
+        className
+      )}
+      {...props}
+    />
+  );
+}
+
+function TableCaption({ className, ...props }: React.ComponentProps<"caption">) {
+  return (
+    <caption data-slot="table-caption" className={cn("mt-4 text-sm text-muted-foreground", className)} {...props} />
+  );
+}
+
+export { Table, TableHeader, TableBody, TableFooter, TableHead, TableRow, TableCell, TableCaption };
diff --git a/Journeys/Journeys.UX/src/components/ui/pagination.tsx b/Journeys/Journeys.UX/src/components/ui/pagination.tsx
new file mode 100644
index 0000000..27e3bd8
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/ui/pagination.tsx
@@ -0,0 +1,100 @@
+import * as React from "react";
+import { ChevronLeftIcon, ChevronRightIcon, MoreHorizontalIcon } from "lucide-react";
+
+import { cn } from "@/lib/utils";
+import { buttonVariants, type Button } from "@/components/ui/button";
+
+function Pagination({ className, ...props }: React.ComponentProps<"nav">) {
+  return (
+    <nav
+      role="navigation"
+      aria-label="pagination"
+      data-slot="pagination"
+      className={cn("mx-auto flex w-full justify-center", className)}
+      {...props}
+    />
+  );
+}
+
+function PaginationContent({ className, ...props }: React.ComponentProps<"ul">) {
+  return <ul data-slot="pagination-content" className={cn("flex flex-row items-center gap-1", className)} {...props} />;
+}
+
+function PaginationItem({ ...props }: React.ComponentProps<"li">) {
+  return <li data-slot="pagination-item" {...props} />;
+}
+
+type PaginationLinkProps = {
+  isActive?: boolean;
+} & Pick<React.ComponentProps<typeof Button>, "size"> &
+  React.ComponentProps<"a">;
+
+function PaginationLink({ className, isActive, size = "icon", ...props }: PaginationLinkProps) {
+  return (
+    <a
+      aria-current={isActive ? "page" : undefined}
+      data-slot="pagination-link"
+      data-active={isActive}
+      className={cn(
+        buttonVariants({
+          variant: isActive ? "outline" : "ghost",
+          size,
+        }),
+        className
+      )}
+      {...props}
+    />
+  );
+}
+
+function PaginationPrevious({ className, ...props }: React.ComponentProps<typeof PaginationLink>) {
+  return (
+    <PaginationLink
+      aria-label="Go to previous page"
+      size="default"
+      className={cn("gap-1 px-2.5 sm:pl-2.5", className)}
+      {...props}
+    >
+      <ChevronLeftIcon />
+      <span className="hidden sm:block">Previous</span>
+    </PaginationLink>
+  );
+}
+
+function PaginationNext({ className, ...props }: React.ComponentProps<typeof PaginationLink>) {
+  return (
+    <PaginationLink
+      aria-label="Go to next page"
+      size="default"
+      className={cn("gap-1 px-2.5 sm:pr-2.5", className)}
+      {...props}
+    >
+      <span className="hidden sm:block">Next</span>
+      <ChevronRightIcon />
+    </PaginationLink>
+  );
+}
+
+function PaginationEllipsis({ className, ...props }: React.ComponentProps<"span">) {
+  return (
+    <span
+      aria-hidden
+      data-slot="pagination-ellipsis"
+      className={cn("flex size-9 items-center justify-center", className)}
+      {...props}
+    >
+      <MoreHorizontalIcon className="size-4" />
+      <span className="sr-only">More pages</span>
+    </span>
+  );
+}
+
+export {
+  Pagination,
+  PaginationContent,
+  PaginationLink,
+  PaginationItem,
+  PaginationPrevious,
+  PaginationNext,
+  PaginationEllipsis,
+};
diff --git a/Journeys/Journeys.UX/src/components/shared/data-table.tsx b/Journeys/Journeys.UX/src/components/shared/data-table.tsx
new file mode 100644
index 0000000..f0e9328
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/shared/data-table.tsx
@@ -0,0 +1,260 @@
+"use client";
+
+import React from "react";
+import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
+import { Skeleton } from "@/components/ui/skeleton";
+import {
+  Pagination,
+  PaginationContent,
+  PaginationItem,
+  PaginationLink,
+  PaginationNext,
+  PaginationPrevious,
+} from "@/components/ui/pagination";
+import { ArrowUp, ArrowDown, ArrowUpDown } from "lucide-react";
+import { cn } from "@/lib/utils";
+import type { JsonValue, PaginationMeta, PaginationParams } from "@/lib/loyalty-schema-types";
+
+export interface ColumnDef<T> {
+  id: string;
+  header: string;
+  accessorFn?: (row: T) => JsonValue;
+  cell?: (row: T) => React.ReactNode;
+  sortable?: boolean;
+}
+
+type SortOrder = NonNullable<PaginationParams["sortOrder"]>;
+
+interface DataTableProps<T> {
+  columns: ColumnDef<T>[];
+  data: T[];
+  pagination?: PaginationMeta;
+  onPageChange?: (page: number) => void;
+  onSort?: (columnId: string, direction: SortOrder) => void;
+  sortColumnId?: string;
+  sortOrder?: SortOrder;
+  allowClearSort?: boolean;
+  defaultSortColumnId?: string;
+  defaultSortOrder?: SortOrder;
+  isLoading?: boolean;
+  emptyMessage?: string;
+  onRowClick?: (row: T) => void;
+  getRowId?: (row: T) => string;
+  selectedRowId?: string | null;
+}
+
+function SortIcon({
+  columnId,
+  activeId,
+  activeOrder,
+}: {
+  columnId: string;
+  activeId?: string;
+  activeOrder?: SortOrder;
+}) {
+  if (columnId !== activeId) return <ArrowUpDown className="ml-1 inline-block size-3 opacity-40" />;
+  if (activeOrder === "asc") return <ArrowUp className="ml-1 inline-block size-3" />;
+  return <ArrowDown className="ml-1 inline-block size-3" />;
+}
+
+export function DataTable<T>({
+  columns,
+  data,
+  pagination,
+  onPageChange,
+  onSort,
+  sortColumnId,
+  sortOrder,
+  allowClearSort = false,
+  defaultSortColumnId,
+  defaultSortOrder = "desc",
+  isLoading = false,
+  emptyMessage = "No results found.",
+  onRowClick,
+  getRowId,
+  selectedRowId,
+}: DataTableProps<T>) {
+  const handleHeaderClick = (col: ColumnDef<T>) => {
+    if (!col.sortable || !onSort) return;
+    const defaultColumnId = defaultSortColumnId ?? col.id;
+    const isActiveColumn = col.id === sortColumnId;
+    const isAlreadyDefaultSort = sortColumnId === defaultColumnId && sortOrder === defaultSortOrder;
+    if (allowClearSort && isActiveColumn && sortOrder === "desc" && !isAlreadyDefaultSort) {
+      onSort(defaultColumnId, defaultSortOrder);
+      return;
+    }
+    const nextOrder: SortOrder = isActiveColumn && sortOrder === "asc" ? "desc" : "asc";
+    onSort(col.id, nextOrder);
+  };
+
+  const ariaSortForColumn = (col: ColumnDef<T>): "none" | "ascending" | "descending" | undefined => {
+    if (!col.sortable || !onSort) return undefined;
+    if (col.id !== sortColumnId) return "none";
+    return sortOrder === "asc" ? "ascending" : "descending";
+  };
+
+  const renderHeader = (col: ColumnDef<T>) => {
+    if (!col.sortable || !onSort) return col.header;
+    return (
+      <button
+        type="button"
+        className="-ml-1 inline-flex items-center gap-0.5 rounded px-1 py-0.5 transition-colors hover:text-foreground"
+        onClick={() => handleHeaderClick(col)}
+      >
+        {col.header}
+        <SortIcon columnId={col.id} activeId={sortColumnId} activeOrder={sortOrder} />
+      </button>
+    );
+  };
+
+  const tableShell = (body: React.ReactNode) => (
+    <div className="overflow-hidden rounded-xl border bg-card">
+      <Table>
+        <TableHeader>
+          <TableRow className="hover:bg-muted/40">
+            {columns.map((col) => (
+              <TableHead key={col.id} aria-sort={ariaSortForColumn(col)}>
+                {renderHeader(col)}
+              </TableHead>
+            ))}
+          </TableRow>
+        </TableHeader>
+        <TableBody>{body}</TableBody>
+      </Table>
+    </div>
+  );
+
+  if (isLoading) {
+    return tableShell(
+      Array.from({ length: 5 }).map((_, i) => (
+        <TableRow key={i} className="hover:bg-transparent">
+          {columns.map((col) => (
+            <TableCell key={col.id}>
+              <Skeleton className={cn("h-4", col.id === columns[0]?.id ? "w-32" : "w-full max-w-24")} />
+            </TableCell>
+          ))}
+        </TableRow>
+      ))
+    );
+  }
+
+  // ΓöÇΓöÇ Record count ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
+  const recordCount = (() => {
+    if (!pagination) return null;
+    const { page, limit, totalPages } = pagination;
+    const total = (pagination as { total?: number }).total;
+    if (total != null) {
+      const from = (page - 1) * limit + 1;
+      const to = Math.min(page * limit, total);
+      return `${from.toLocaleString()}ΓÇô${to.toLocaleString()} of ${total.toLocaleString()}`;
+    }
+    if (totalPages > 1) return `Page ${page} of ${totalPages}`;
+    return null;
+  })();
+
+  return (
+    <div className="space-y-3">
+      {tableShell(
+        data.length === 0 ? (
+          <TableRow className="hover:bg-transparent">
+            <TableCell colSpan={columns.length} className="px-4 py-10 text-center">
+              <p className="text-sm text-muted-foreground">{emptyMessage}</p>
+            </TableCell>
+          </TableRow>
+        ) : (
+          data.map((row, i) => {
+            const rowKey = getRowId?.(row) ?? String(i);
+            const isSelected = selectedRowId != null && getRowId?.(row) === selectedRowId;
+            return (
+              <TableRow
+                key={rowKey}
+                data-state={isSelected ? "selected" : undefined}
+                tabIndex={onRowClick ? 0 : undefined}
+                className={cn(
+                  "transition-colors",
+                  onRowClick && "cursor-pointer hover:bg-muted/50",
+                  isSelected && "border-l-2 border-primary bg-primary/5 hover:bg-primary/8"
+                )}
+                onClick={onRowClick ? () => onRowClick(row) : undefined}
+                onKeyDown={
+                  onRowClick
+                    ? (e) => {
+                        if (e.key === "Enter" || e.key === " ") {
+                          e.preventDefault();
+                          onRowClick(row);
+                        }
+                      }
+                    : undefined
+                }
+              >
+                {columns.map((col) => (
+                  <TableCell key={col.id}>
+                    {col.cell ? col.cell(row) : col.accessorFn ? String(col.accessorFn(row) ?? "") : null}
+                  </TableCell>
+                ))}
+              </TableRow>
+            );
+          })
+        )
+      )}
+
+      {pagination && pagination.totalPages > 1 && (
+        <div className="flex items-center justify-between gap-4">
+          {recordCount && <p className="text-xs text-muted-foreground">{recordCount}</p>}
+          <Pagination className="mx-0 w-auto flex-1 justify-end">
+            <PaginationContent>
+              {pagination.hasPrev && (
+                <PaginationItem>
+                  <PaginationPrevious
+                    href="#"
+                    onClick={(e) => {
+                      e.preventDefault();
+                      onPageChange?.(pagination.page - 1);
+                    }}
+                  />
+                </PaginationItem>
+              )}
+              {Array.from({ length: pagination.totalPages }, (_, i) => i + 1)
+                .filter((p) => p === 1 || p === pagination.totalPages || Math.abs(p - pagination.page) <= 1)
+                .map((p, idx, arr) => {
+                  const prev = arr[idx - 1];
+                  return (
+                    <React.Fragment key={p}>
+                      {prev != null && p - prev > 1 && (
+                        <PaginationItem>
+                          <span className="px-2 text-sm text-muted-foreground">ΓÇª</span>
+                        </PaginationItem>
+                      )}
+                      <PaginationItem>
+                        <PaginationLink
+                          href="#"
+                          isActive={p === pagination.page}
+                          onClick={(e) => {
+                            e.preventDefault();
+                            onPageChange?.(p);
+                          }}
+                        >
+                          {p}
+                        </PaginationLink>
+                      </PaginationItem>
+                    </React.Fragment>
+                  );
+                })}
+              {pagination.hasNext && (
+                <PaginationItem>
+                  <PaginationNext
+                    href="#"
+                    onClick={(e) => {
+                      e.preventDefault();
+                      onPageChange?.(pagination.page + 1);
+                    }}
+                  />
+                </PaginationItem>
+              )}
+            </PaginationContent>
+          </Pagination>
+        </div>
+      )}
+    </div>
+  );
+}
diff --git a/Journeys/Journeys.UX/src/components/loyalty/dynamic-data/dynamic-data-table.tsx b/Journeys/Journeys.UX/src/components/loyalty/dynamic-data/dynamic-data-table.tsx
new file mode 100644
index 0000000..06f1bda
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/loyalty/dynamic-data/dynamic-data-table.tsx
@@ -0,0 +1,914 @@
+"use client";
+
+/**
+ * DynamicDataTable ΓÇö schema-driven data grid for loyalty events/entities.
+ *
+ * Generates columns from a `LoyaltySchema`, queries `queryData` with continuation-
+ * token pagination, supports optional account-selection gating, date-range filter,
+ * sortable columns (`SchemaAttribute.isGridSortable`/`defaultSortDirection`), CSV
+ * export, and row-click navigation.
+ *
+ * Row navigation is opt-in: pass `detailRoutePath` to make rows clickable.
+ */
+
+import { useCallback, useEffect, useMemo, useState } from "react";
+import { useRouter, useSearchParams } from "next/navigation";
+import { useQuery } from "@tanstack/react-query";
+import { Download, Plus, Search, Users, X } from "lucide-react";
+
+import { Button } from "@/components/ui/button";
+import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
+import { Card, CardContent } from "@/components/ui/card";
+import { Badge } from "@/components/ui/badge";
+import { Input } from "@/components/ui/input";
+import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
+import { DataTable, type ColumnDef } from "@/components/shared/data-table";
+import { cn } from "@/lib/utils";
+
+import { queryData } from "@/services/loyalty/actions";
+import type { LoyaltySchema } from "@/lib/loyalty-schema-types";
+import {
+  formatValueAsString,
+  generateColumnsFromSchema,
+  getInitialSortFromSchema,
+  getNestedValue,
+  getSchemaFieldValue,
+} from "@/services/loyalty/utils/grid-columns";
+import { normalizeEntityWithEvent } from "@/services/loyalty/utils/account-identifiers";
+import {
+  AccountsSelectionModal,
+  type SelectedLoyaltyAccount,
+} from "@/components/loyalty/accounts/accounts-selection-modal";
+import { DateRangeFilter } from "./date-range-filter";
+
+interface EventRow {
+  id?: string;
+  event?: Record<string, unknown>;
+}
+
+export interface DynamicDataTableProps<T extends EventRow = EventRow> {
+  schema: LoyaltySchema;
+  /** Pre-loaded data (used only when account selection is not required). */
+  initialData?: T[];
+  /** Bypass account-selection requirement (e.g. for nested grids in entity details). */
+  skipAccountSelection?: boolean;
+  /**
+   * Route prefix for row clicks, e.g. `/loyalty/accounts`. Rows are not
+   * clickable unless this is set.
+   */
+  detailRoutePath?: string | null;
+  emptyMessage?: string;
+  countLabel?: { singular: string; plural: string };
+  exportFilePrefix?: string;
+  fallbackIdColumn?: {
+    label: string;
+    getValue: (row: T) => unknown;
+  };
+  extraSearchFields?: string[];
+  getDetailId?: (row: T) => string | undefined;
+  className?: string;
+}
+
+interface QueryFilters {
+  selectedAccountIds: string[];
+  fromDate: string | null;
+  toDate: string | null;
+  sortKey: string;
+  sortOrder: "asc" | "desc";
+  searchText: string;
+  searchField: string;
+  columnFilters: AppliedFilter[];
+}
+
+interface QueryResult<T> {
+  rows: T[];
+  continuationToken: string | null;
+}
+
+type NormalizedDataType = "string" | "number" | "date" | "boolean";
+type FilterOperator = "contains" | "equals" | "startsWith" | "gt" | "gte" | "lt" | "lte" | "between";
+
+interface FilterableColumn {
+  key: string;
+  label: string;
+  dataType: NormalizedDataType;
+}
+
+interface AppliedFilter {
+  id: string;
+  key: string;
+  label: string;
+  dataType: NormalizedDataType;
+  operator: FilterOperator;
+  value: string;
+  valueTo?: string;
+}
+
+const ALL_FIELDS_SEARCH = "__all_fields__";
+const EMPTY_EXTRA_SEARCH_FIELDS: string[] = [];
+
+function normalizeDataType(dataType: string | null | undefined): NormalizedDataType {
+  switch ((dataType ?? "string").toLowerCase()) {
+    case "int":
+    case "double":
+    case "number":
+      return "number";
+    case "date":
+    case "datetime":
+      return "date";
+    case "boolean":
+    case "bool":
+      return "boolean";
+    default:
+      return "string";
+  }
+}
+
+function getOperatorsForDataType(dataType: NormalizedDataType): FilterOperator[] {
+  switch (dataType) {
+    case "string":
+      return ["contains", "startsWith", "equals"];
+    case "number":
+      return ["equals", "gt", "gte", "lt", "lte", "between"];
+    case "date":
+      return ["equals", "gt", "gte", "lt", "lte", "between"];
+    case "boolean":
+      return ["equals"];
+    default:
+      return ["equals"];
+  }
+}
+
+function getFieldExpression(symbol: string): { rootField: string; eventField: string } | null {
+  if (!/^[A-Za-z_][A-Za-z0-9_]*$/.test(symbol)) return null;
+  return {
+    rootField: `c.${symbol}`,
+    eventField: `c.event.${symbol}`,
+  };
+}
+
+function compareValues(a: unknown, b: unknown, dataType: string | null | undefined): number {
+  if (a == null && b == null) return 0;
+  if (a == null) return 1;
+  if (b == null) return -1;
+
+  if (typeof a === "number" && typeof b === "number") return a - b;
+
+  const normalizedDataType = (dataType ?? "string").toLowerCase();
+  if (normalizedDataType === "date" || normalizedDataType === "datetime") {
+    const aDate = a instanceof Date ? a.getTime() : typeof a === "string" ? Date.parse(a) : Number.NaN;
+    const bDate = b instanceof Date ? b.getTime() : typeof b === "string" ? Date.parse(b) : Number.NaN;
+    if (!Number.isNaN(aDate) && !Number.isNaN(bDate)) return aDate - bDate;
+  }
+
+  if (typeof a === "boolean" && typeof b === "boolean") return Number(a) - Number(b);
+
+  return String(a).localeCompare(String(b), undefined, { numeric: true, sensitivity: "base" });
+}
+
+function buildQuery(
+  schemaName: string,
+  dateAttributeSymbol: string | null,
+  filters: QueryFilters,
+  filterableColumns: FilterableColumn[],
+  extraSearchFields: string[],
+  continuationToken?: string
+): Parameters<typeof queryData>[0] {
+  const conditions: string[] = [];
+  const args: Record<string, unknown> = {};
+  let paramIndex = 0;
+
+  const nextParam = () => `@p${paramIndex++}`;
+
+  if (filters.selectedAccountIds.length > 0) {
+    conditions.push(`ARRAY_CONTAINS(${JSON.stringify(filters.selectedAccountIds)}, c.accountid)`);
+  }
+
+  if (dateAttributeSymbol && (filters.fromDate || filters.toDate)) {
+    const fieldExpr = getFieldExpression(dateAttributeSymbol);
+    if (fieldExpr) {
+      if (filters.fromDate) {
+        args["@fromDate"] = new Date(`${filters.fromDate}T00:00:00.000Z`).toISOString();
+        conditions.push(`(${fieldExpr.eventField} >= @fromDate OR ${fieldExpr.rootField} >= @fromDate)`);
+      }
+      if (filters.toDate) {
+        args["@toDate"] = new Date(`${filters.toDate}T23:59:59.999Z`).toISOString();
+        conditions.push(`(${fieldExpr.eventField} <= @toDate OR ${fieldExpr.rootField} <= @toDate)`);
+      }
+    }
+  }
+
+  if (filters.searchText) {
+    const searchParam = nextParam();
+    args[searchParam] = filters.searchText;
+
+    if (filters.searchField === ALL_FIELDS_SEARCH) {
+      const searchableFieldKeys = Array.from(
+        new Set([
+          ...filterableColumns.filter((col) => col.dataType === "string").map((col) => col.key),
+          ...extraSearchFields,
+        ])
+      );
+      const searchableClauses = searchableFieldKeys
+        .map((key) => getFieldExpression(key))
+        .filter((fieldExpr): fieldExpr is { rootField: string; eventField: string } => Boolean(fieldExpr))
+        .map(
+          (fieldExpr) =>
+            `CONTAINS(${fieldExpr.eventField}, ${searchParam}, true) OR CONTAINS(${fieldExpr.rootField}, ${searchParam}, true)`
+        );
+      if (searchableClauses.length > 0) {
+        conditions.push(`(${searchableClauses.join(" OR ")})`);
+      }
+    } else {
+      const fieldExpr = getFieldExpression(filters.searchField);
+      if (fieldExpr) {
+        conditions.push(
+          `(CONTAINS(${fieldExpr.eventField}, ${searchParam}, true) OR CONTAINS(${fieldExpr.rootField}, ${searchParam}, true))`
+        );
+      }
+    }
+  }
+
+  for (const filter of filters.columnFilters) {
+    const fieldExpr = getFieldExpression(filter.key);
+    if (!fieldExpr) continue;
+
+    const makeDualField = (expr: string) =>
+      `(${expr.replaceAll("__FIELD__", fieldExpr.eventField)} OR ${expr.replaceAll("__FIELD__", fieldExpr.rootField)})`;
+
+    const parseFilterValue = (value: string): unknown => {
+      if (filter.dataType === "number") {
+        const parsed = Number(value);
+        return Number.isFinite(parsed) ? parsed : undefined;
+      }
+      if (filter.dataType === "boolean") {
+        if (value === "true") return true;
+        if (value === "false") return false;
+        return undefined;
+      }
+      if (filter.dataType === "date") {
+        const parsedDate = new Date(value);
+        if (Number.isNaN(parsedDate.getTime())) return undefined;
+        return parsedDate.toISOString();
+      }
+      return value;
+    };
+
+    if (filter.operator === "between") {
+      if (!filter.value || !filter.valueTo) continue;
+      const fromParam = nextParam();
+      const toParam = nextParam();
+      const parsedFrom = parseFilterValue(filter.value);
+      let parsedTo = parseFilterValue(filter.valueTo);
+      if (filter.dataType === "date" && typeof parsedTo === "string") {
+        const endDate = new Date(parsedTo);
+        if (Number.isNaN(endDate.getTime())) continue;
+        endDate.setUTCHours(23, 59, 59, 999);
+        parsedTo = endDate.toISOString();
+      }
+      if (parsedFrom == null || parsedTo == null) continue;
+      args[fromParam] = parsedFrom;
+      args[toParam] = parsedTo;
+      conditions.push(makeDualField(`(__FIELD__ >= ${fromParam} AND __FIELD__ <= ${toParam})`));
+      continue;
+    }
+
+    if (!filter.value) continue;
+    const valueParam = nextParam();
+    const parsedValue = parseFilterValue(filter.value);
+    if (parsedValue == null) continue;
+    args[valueParam] = parsedValue;
+
+    switch (filter.operator) {
+      case "contains":
+        conditions.push(makeDualField(`CONTAINS(__FIELD__, ${valueParam}, true)`));
+        break;
+      case "startsWith":
+        conditions.push(makeDualField(`STARTSWITH(__FIELD__, ${valueParam}, true)`));
+        break;
+      case "equals":
+        if (filter.dataType === "date") {
+          const date = new Date(filter.value);
+          if (Number.isNaN(date.getTime())) break;
+          const endOfDay = new Date(date);
+          endOfDay.setUTCHours(23, 59, 59, 999);
+          const endParam = nextParam();
+          args[valueParam] = date.toISOString();
+          args[endParam] = endOfDay.toISOString();
+          conditions.push(makeDualField(`(__FIELD__ >= ${valueParam} AND __FIELD__ <= ${endParam})`));
+          break;
+        }
+        conditions.push(makeDualField(`__FIELD__ = ${valueParam}`));
+        break;
+      case "gt":
+        conditions.push(makeDualField(`__FIELD__ > ${valueParam}`));
+        break;
+      case "gte":
+        conditions.push(makeDualField(`__FIELD__ >= ${valueParam}`));
+        break;
+      case "lt":
+        conditions.push(makeDualField(`__FIELD__ < ${valueParam}`));
+        break;
+      case "lte":
+        conditions.push(makeDualField(`__FIELD__ <= ${valueParam}`));
+        break;
+      default:
+        break;
+    }
+  }
+
+  return {
+    schemaName,
+    queryString: conditions.join(" AND "),
+    queryArgs: args,
+    pageSize: 50,
+    continuationToken: continuationToken ?? undefined,
+    sortBy: filters.sortKey || undefined,
+    sortOrder: filters.sortOrder.toUpperCase() as "ASC" | "DESC",
+  };
+}
+
+export function DynamicDataTable<T extends EventRow = EventRow>({
+  schema,
+  initialData,
+  skipAccountSelection = false,
+  detailRoutePath,
+  emptyMessage,
+  countLabel,
+  exportFilePrefix,
+  fallbackIdColumn,
+  extraSearchFields = EMPTY_EXTRA_SEARCH_FIELDS,
+  getDetailId,
+  className,
+}: DynamicDataTableProps<T>) {
+  const router = useRouter();
+  const searchParams = useSearchParams();
+  const schemaName = schema.name ?? "";
+  const schemaLabel = schemaName.toLowerCase();
+  // gridRequiresAccount must be explicitly true to gate the grid.
+  // `undefined` / `null` (fields not yet configured) should NOT block data access.
+  const requiresAccountSelection = !skipAccountSelection && schema.gridRequiresAccount === true;
+
+  const initialSort = useMemo(() => getInitialSortFromSchema(schema), [schema]);
+  const [sortKey, setSortKey] = useState("");
+  const [sortOrder, setSortOrder] = useState<"asc" | "desc">(initialSort.order);
+  const [fromDate, setFromDate] = useState<string | null>(null);
+  const [toDate, setToDate] = useState<string | null>(null);
+  const [searchInput, setSearchInput] = useState("");
+  const [searchText, setSearchText] = useState("");
+  const [searchField, setSearchField] = useState(ALL_FIELDS_SEARCH);
+  const [draftFilterKey, setDraftFilterKey] = useState("");
+  const [draftFilterOperator, setDraftFilterOperator] = useState<FilterOperator>("contains");
+  const [draftFilterValue, setDraftFilterValue] = useState("");
+  const [draftFilterValueTo, setDraftFilterValueTo] = useState("");
+  const [columnFilters, setColumnFilters] = useState<AppliedFilter[]>([]);
+  const accountIdsFromUrl = useMemo(() => {
+    const direct = searchParams.getAll("account");
+    const split = direct.flatMap((value) => value.split(","));
+    return Array.from(new Set(split.map((v) => v.trim()).filter((v) => v.length > 0)));
+  }, [searchParams]);
+
+  const [selectedAccounts, setSelectedAccounts] = useState<SelectedLoyaltyAccount[]>(() =>
+    accountIdsFromUrl.map((id) => ({
+      loyaltyAccountId: id,
+      displayLabel: id,
+      eventData: {},
+    }))
+  );
+  const [accountsModalOpen, setAccountsModalOpen] = useState(false);
+  const [continuationToken, setContinuationToken] = useState<string | null>(null);
+  const [allRows, setAllRows] = useState<T[]>([]);
+
+  useEffect(() => {
+    setSortKey("");
+    setSortOrder(initialSort.order);
+  }, [schema.id, initialSort.order]);
+
+  useEffect(() => {
+    if (!requiresAccountSelection || accountIdsFromUrl.length === 0) return;
+    setSelectedAccounts((prev) => {
+      const prevIds = new Set(prev.map((a) => a.loyaltyAccountId));
+      const sameLength = prev.length === accountIdsFromUrl.length;
+      const sameValues = sameLength && accountIdsFromUrl.every((id) => prevIds.has(id));
+      if (sameValues) return prev;
+      return accountIdsFromUrl.map((id) => ({
+        loyaltyAccountId: id,
+        displayLabel: id,
+        eventData: {},
+      }));
+    });
+  }, [requiresAccountSelection, accountIdsFromUrl]);
+
+  const dateAttribute = useMemo(() => {
+    return (schema.attributes ?? []).find(
+      (a) => a.type === "Primitive" && a.status === "Live" && (a.dataType ?? "").toLowerCase() === "date"
+    );
+  }, [schema]);
+
+  const filterableColumns = useMemo<FilterableColumn[]>(() => {
+    return (schema.attributes ?? [])
+      .filter((a) => a.type === "Primitive" && a.status === "Live" && Boolean(a.symbol))
+      .map((a) => ({
+        key: a.symbol as string,
+        label: a.displayName ?? (a.symbol as string),
+        dataType: normalizeDataType(a.dataType),
+      }))
+      .filter((column) => Boolean(getFieldExpression(column.key)));
+  }, [schema]);
+
+  const selectedDraftColumn = useMemo(
+    () => filterableColumns.find((column) => column.key === draftFilterKey) ?? null,
+    [filterableColumns, draftFilterKey]
+  );
+
+  const availableOperators = useMemo<FilterOperator[]>(() => {
+    if (!selectedDraftColumn) return ["contains", "equals"];
+    return getOperatorsForDataType(selectedDraftColumn.dataType);
+  }, [selectedDraftColumn]);
+
+  useEffect(() => {
+    if (!availableOperators.includes(draftFilterOperator)) {
+      setDraftFilterOperator(availableOperators[0] ?? "equals");
+    }
+  }, [availableOperators, draftFilterOperator]);
+
+  const filters = useMemo<QueryFilters>(
+    () => ({
+      selectedAccountIds: selectedAccounts.map((a) => a.loyaltyAccountId),
+      fromDate,
+      toDate,
+      sortKey: sortKey === initialSort.key ? "" : sortKey,
+      sortOrder: sortKey === initialSort.key ? initialSort.order : sortOrder,
+      searchText,
+      searchField,
+      columnFilters,
+    }),
+    [
+      selectedAccounts,
+      fromDate,
+      toDate,
+      initialSort.key,
+      initialSort.order,
+      sortKey,
+      sortOrder,
+      searchText,
+      searchField,
+      columnFilters,
+    ]
+  );
+
+  const isQueryEnabled = !requiresAccountSelection || selectedAccounts.length > 0;
+
+  const queryResult = useQuery<QueryResult<T>>({
+    queryKey: [
+      "loyalty",
+      "dynamic-data",
+      schema.id,
+      schemaName,
+      filters.selectedAccountIds,
+      filters.fromDate,
+      filters.toDate,
+      filters.sortKey,
+      filters.sortOrder,
+      filters.searchText,
+      filters.searchField,
+      filters.columnFilters,
+      extraSearchFields,
+      // Note: we intentionally exclude `continuationToken` from the key so that
+      // appending more pages doesn't re-fetch from page 1.
+    ],
+    queryFn: async () => {
+      const response = await queryData<T>(
+        buildQuery(schemaName, dateAttribute?.symbol ?? null, filters, filterableColumns, extraSearchFields)
+      );
+      if (!response.success) throw new Error(response.error ?? "Failed to load data");
+      const rawData: T[] = response.data ?? [];
+      const rows = rawData
+        .map((item) => (normalizeEntityWithEvent(item as Record<string, unknown>) ?? item) as T)
+        .filter(Boolean);
+      const newToken = response.meta?.continuationToken ?? null;
+      // On a fresh query (no existing token), reset accumulated rows
+      setAllRows(rows);
+      setContinuationToken(newToken);
+      return {
+        rows,
+        continuationToken: newToken,
+      };
+    },
+    enabled: isQueryEnabled,
+    placeholderData: (previous) => previous,
+  });
+
+  const [isLoadingMore, setIsLoadingMore] = useState(false);
+
+  const handleLoadMore = useCallback(async () => {
+    if (!continuationToken || isLoadingMore) return;
+    setIsLoadingMore(true);
+    try {
+      const response = await queryData<T>(
+        buildQuery(
+          schemaName,
+          dateAttribute?.symbol ?? null,
+          filters,
+          filterableColumns,
+          extraSearchFields,
+          continuationToken
+        )
+      );
+      if (!response.success) return;
+      const rawData: T[] = response.data ?? [];
+      const newRows = rawData
+        .map((item) => (normalizeEntityWithEvent(item as Record<string, unknown>) ?? item) as T)
+        .filter(Boolean);
+      setAllRows((prev) => [...prev, ...newRows]);
+      setContinuationToken(response.meta?.continuationToken ?? null);
+    } finally {
+      setIsLoadingMore(false);
+    }
+  }, [
+    continuationToken,
+    isLoadingMore,
+    schemaName,
+    dateAttribute?.symbol,
+    filters,
+    filterableColumns,
+    extraSearchFields,
+  ]);
+
+  const rawData = useMemo<T[]>(() => {
+    const queryRows = queryResult.data?.rows ?? [];
+    if (allRows.length > 0) return allRows;
+    if (queryRows.length > 0) return queryRows;
+    return requiresAccountSelection ? [] : (initialData ?? []);
+  }, [allRows, queryResult.data, requiresAccountSelection, initialData]);
+  const schemaColumns = useMemo(() => generateColumnsFromSchema(schema), [schema]);
+  const columns = useMemo<ColumnDef<T>[]>(() => {
+    return schemaColumns.map((col) => ({
+      id: col.key,
+      header: col.key === "id" && fallbackIdColumn ? fallbackIdColumn.label : col.label,
+      sortable: col.sortable,
+      cell: (row: T) => {
+        const value =
+          col.key === "id" && fallbackIdColumn ? fallbackIdColumn.getValue(row) : getSchemaFieldValue(row, col.key);
+        return <span className="text-sm">{formatValueAsString(value, col.dataType)}</span>;
+      },
+    }));
+  }, [schemaColumns, fallbackIdColumn]);
+  const sortColumn = useMemo(
+    () => schemaColumns.find((column) => column.key === sortKey) ?? null,
+    [schemaColumns, sortKey]
+  );
+  const data = useMemo<T[]>(() => {
+    if (!sortKey || sortKey !== initialSort.key) return rawData;
+
+    return [...rawData].sort((left, right) => {
+      const leftValue = getNestedValue(left, sortKey);
+      const rightValue = getNestedValue(right, sortKey);
+      const compared = compareValues(leftValue, rightValue, sortColumn?.dataType);
+      return sortOrder === "asc" ? compared : -compared;
+    });
+  }, [initialSort.key, rawData, sortColumn?.dataType, sortKey, sortOrder]);
+  const isLoading = queryResult.isLoading;
+  const errorMessage = (queryResult.error as Error | undefined)?.message;
+
+  const handleSort = useCallback((columnId: string, direction: "asc" | "desc") => {
+    setSortKey(columnId);
+    setSortOrder(direction);
+  }, []);
+
+  const handleSearch = useCallback(() => {
+    setSearchText(searchInput.trim());
+  }, [searchInput]);
+
+  const handleClearSearch = useCallback(() => {
+    setSearchInput("");
+    setSearchText("");
+  }, []);
+
+  const handleAddFilter = useCallback(() => {
+    if (!selectedDraftColumn) return;
+    const value = draftFilterValue.trim();
+    const valueTo = draftFilterValueTo.trim();
+    if (!value) return;
+    if (draftFilterOperator === "between" && !valueTo) return;
+
+    setColumnFilters((prev) => [
+      ...prev,
+      {
+        id: crypto.randomUUID(),
+        key: selectedDraftColumn.key,
+        label: selectedDraftColumn.label,
+        dataType: selectedDraftColumn.dataType,
+        operator: draftFilterOperator,
+        value,
+        valueTo: draftFilterOperator === "between" ? valueTo : undefined,
+      },
+    ]);
+
+    setDraftFilterValue("");
+    setDraftFilterValueTo("");
+  }, [selectedDraftColumn, draftFilterOperator, draftFilterValue, draftFilterValueTo]);
+
+  const handleRemoveFilter = useCallback((id: string) => {
+    setColumnFilters((prev) => prev.filter((filter) => filter.id !== id));
+  }, []);
+
+  const handleClearFilters = useCallback(() => {
+    setColumnFilters([]);
+  }, []);
+
+  const handleRowClick = useCallback(
+    (row: T) => {
+      if (!detailRoutePath) return;
+      const id = getDetailId?.(row) || row.id || (getNestedValue(row, "id") as string | undefined);
+      if (!id) return;
+      const target = new URL(`${detailRoutePath}/${encodeURIComponent(id)}`, "http://localhost");
+      for (const accountId of filters.selectedAccountIds) {
+        target.searchParams.append("account", accountId);
+      }
+      router.push(`${target.pathname}${target.search}`);
+    },
+    [router, detailRoutePath, getDetailId, filters.selectedAccountIds]
+  );
+
+  const handleExport = useCallback(() => {
+    if (data.length === 0) return;
+    const headerRow = columns.map((c) => c.header).join(",");
+    const dataRows = data.map((row) =>
+      columns
+        .map((col) => {
+          const value = getSchemaFieldValue(row, col.id);
+          const stringValue = String(value ?? "").replace(/"/g, '""');
+          return `"${stringValue}"`;
+        })
+        .join(",")
+    );
+    const csv = [headerRow, ...dataRows].join("\n");
+    const blob = new Blob([csv], { type: "text/csv" });
+    const url = URL.createObjectURL(blob);
+    const a = document.createElement("a");
+    a.href = url;
+    a.download = `${exportFilePrefix ?? schemaName}-${new Date().toISOString()}.csv`;
+    a.click();
+    URL.revokeObjectURL(url);
+  }, [data, columns, exportFilePrefix, schemaName]);
+
+  if (requiresAccountSelection && selectedAccounts.length === 0) {
+    return (
+      <>
+        <Card>
+          <CardContent className="p-12">
+            <div className="flex flex-col items-center justify-center text-center space-y-3">
+              <Users className="h-10 w-10 text-muted-foreground" aria-hidden="true" />
+              <div>
+                <h3 className="text-lg font-semibold">No data to display</h3>
+                <p className="text-sm text-muted-foreground mt-1">
+                  Select one or more loyalty accounts to view their {schemaLabel}.
+                </p>
+              </div>
+              <Button onClick={() => setAccountsModalOpen(true)}>Select loyalty accounts</Button>
+            </div>
+          </CardContent>
+        </Card>
+        <AccountsSelectionModal
+          open={accountsModalOpen}
+          onOpenChange={setAccountsModalOpen}
+          onApply={setSelectedAccounts}
+          initialSelections={selectedAccounts}
+        />
+      </>
+    );
+  }
+
+  return (
+    <div className={cn("space-y-4", className)}>
+      <Card>
+        <CardContent className="p-4 space-y-4">
+          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
+            <div className="flex flex-wrap items-center gap-2">
+              {requiresAccountSelection && (
+                <Button size="sm" variant="outline" onClick={() => setAccountsModalOpen(true)}>
+                  Change accounts ({selectedAccounts.length})
+                </Button>
+              )}
+              <div className="flex items-center gap-2">
+                <Select value={searchField} onValueChange={setSearchField}>
+                  <SelectTrigger className="h-8 w-[180px]">
+                    <SelectValue placeholder="Search field" />
+                  </SelectTrigger>
+                  <SelectContent>
+                    <SelectItem value={ALL_FIELDS_SEARCH}>All text fields</SelectItem>
+                    {filterableColumns
+                      .filter((column) => column.dataType === "string")
+                      .map((column) => (
+                        <SelectItem key={column.key} value={column.key}>
+                          {column.label}
+                        </SelectItem>
+                      ))}
+                  </SelectContent>
+                </Select>
+                <div className="relative w-[220px]">
+                  <Search className="pointer-events-none absolute left-2 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
+                  <Input
+                    value={searchInput}
+                    onChange={(event) => setSearchInput(event.target.value)}
+                    onKeyDown={(event) => {
+                      if (event.key === "Enter") {
+                        event.preventDefault();
+                        handleSearch();
+                      }
+                    }}
+                    placeholder="Search"
+                    className="h-8 pl-8"
+                  />
+                </div>
+                <Button size="sm" variant="outline" onClick={handleSearch}>
+                  Search
+                </Button>
+                {(searchText || searchInput) && (
+                  <Button size="sm" variant="ghost" onClick={handleClearSearch}>
+                    Clear
+                  </Button>
+                )}
+              </div>
+              {dateAttribute && (
+                <DateRangeFilter
+                  fromDate={fromDate}
+                  toDate={toDate}
+                  onChange={(from, to) => {
+                    setFromDate(from);
+                    setToDate(to);
+                  }}
+                />
+              )}
+            </div>
+            <Button
+              size="sm"
+              variant="outline"
+              onClick={handleExport}
+              disabled={data.length === 0}
+              title="Export currently loaded rows to CSV (server-side pagination not included)"
+            >
+              <Download className="mr-2 h-4 w-4" />
+              Export
+            </Button>
+          </div>
+
+          <div className="flex flex-wrap items-center gap-2">
+            <Select value={draftFilterKey} onValueChange={setDraftFilterKey}>
+              <SelectTrigger className="h-8 w-[190px]">
+                <SelectValue placeholder="Filter field" />
+              </SelectTrigger>
+              <SelectContent>
+                {filterableColumns.map((column) => (
+                  <SelectItem key={column.key} value={column.key}>
+                    {column.label}
+                  </SelectItem>
+                ))}
+              </SelectContent>
+            </Select>
+            <Select
+              value={draftFilterOperator}
+              onValueChange={(value) => setDraftFilterOperator(value as FilterOperator)}
+              disabled={!selectedDraftColumn}
+            >
+              <SelectTrigger className="h-8 w-[130px]">
+                <SelectValue placeholder="Operator" />
+              </SelectTrigger>
+              <SelectContent>
+                {availableOperators.map((operator) => (
+                  <SelectItem key={operator} value={operator}>
+                    {operator}
+                  </SelectItem>
+                ))}
+              </SelectContent>
+            </Select>
+            {selectedDraftColumn?.dataType !== "boolean" && (
+              <Input
+                value={draftFilterValue}
+                onChange={(event) => setDraftFilterValue(event.target.value)}
+                className="h-8 w-[170px]"
+                placeholder="Value"
+                type={
+                  selectedDraftColumn?.dataType === "number"
+                    ? "number"
+                    : selectedDraftColumn?.dataType === "date"
+                      ? "date"
+                      : "text"
+                }
+              />
+            )}
+            {draftFilterOperator === "between" && (
+              <Input
+                value={draftFilterValueTo}
+                onChange={(event) => setDraftFilterValueTo(event.target.value)}
+                className="h-8 w-[170px]"
+                placeholder="And value"
+                type={
+                  selectedDraftColumn?.dataType === "number"
+                    ? "number"
+                    : selectedDraftColumn?.dataType === "date"
+                      ? "date"
+                      : "text"
+                }
+              />
+            )}
+            {selectedDraftColumn?.dataType === "boolean" && (
+              <Select value={draftFilterValue} onValueChange={setDraftFilterValue}>
+                <SelectTrigger className="h-8 w-[130px]">
+                  <SelectValue placeholder="True / False" />
+                </SelectTrigger>
+                <SelectContent>
+                  <SelectItem value="true">true</SelectItem>
+                  <SelectItem value="false">false</SelectItem>
+                </SelectContent>
+              </Select>
+            )}
+            <Button
+              size="sm"
+              variant="outline"
+              onClick={handleAddFilter}
+              disabled={
+                !selectedDraftColumn || !draftFilterValue || (draftFilterOperator === "between" && !draftFilterValueTo)
+              }
+            >
+              <Plus className="mr-1 h-3 w-3" />
+              Add filter
+            </Button>
+            {columnFilters.length > 0 && (
+              <Button size="sm" variant="ghost" onClick={handleClearFilters}>
+                Clear filters
+              </Button>
+            )}
+          </div>
+
+          {columnFilters.length > 0 && (
+            <div className="flex flex-wrap gap-2">
+              {columnFilters.map((filter) => (
+                <Badge key={filter.id} variant="secondary" className="gap-1">
+                  {filter.label} {filter.operator} {filter.value}
+                  {filter.valueTo ? ` - ${filter.valueTo}` : ""}
+                  <button
+                    type="button"
+                    aria-label={`Remove filter ${filter.label}`}
+                    onClick={() => handleRemoveFilter(filter.id)}
+                    className="ml-1 hover:text-destructive"
+                  >
+                    <X className="h-3 w-3" />
+                  </button>
+                </Badge>
+              ))}
+            </div>
+          )}
+
+          {errorMessage && (
+            <Alert variant="destructive">
+              <AlertTitle>Failed to load data</AlertTitle>
+              <AlertDescription>{errorMessage}</AlertDescription>
+            </Alert>
+          )}
+
+          <DataTable<T>
+            columns={columns}
+            data={data}
+            isLoading={isLoading}
+            emptyMessage={emptyMessage ?? `No ${schemaLabel} found.`}
+            onRowClick={detailRoutePath ? handleRowClick : undefined}
+            getRowId={(row) => row.id ?? ""}
+            onSort={handleSort}
+            sortColumnId={sortKey}
+            sortOrder={sortOrder}
+            allowClearSort
+          />
+
+          <p className="text-xs text-muted-foreground text-center">
+            Showing {data.length}{" "}
+            {data.length === 1 ? (countLabel?.singular ?? schemaLabel) : (countLabel?.plural ?? `${schemaLabel}s`)}
+            {continuationToken && (
+              <>
+                {" ΓÇö "}
+                <button
+                  type="button"
+                  onClick={() => void handleLoadMore()}
+                  disabled={isLoadingMore}
+                  className="underline hover:text-foreground disabled:opacity-50"
+                >
+                  {isLoadingMore ? "LoadingΓÇª" : "Load more"}
+                </button>
+              </>
+            )}
+          </p>
+        </CardContent>
+      </Card>
+
+      {requiresAccountSelection && (
+        <AccountsSelectionModal
+          open={accountsModalOpen}
+          onOpenChange={setAccountsModalOpen}
+          onApply={setSelectedAccounts}
+          initialSelections={selectedAccounts}
+        />
+      )}
+    </div>
+  );
+}
diff --git a/Journeys/Journeys.UX/src/components/loyalty/dynamic-data/simple-data-table.tsx b/Journeys/Journeys.UX/src/components/loyalty/dynamic-data/simple-data-table.tsx
new file mode 100644
index 0000000..48aad70
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/loyalty/dynamic-data/simple-data-table.tsx
@@ -0,0 +1,38 @@
+"use client";
+
+/**
+ * SimpleDataTable ΓÇö read-only schema-driven table for primitive attribute lists.
+ *
+ * Renders via the shared `<DataTable>` so nested grids keep the same a11y
+ * affordances as the top-level list.
+ */
+
+import { useMemo } from "react";
+import { DataTable, type ColumnDef } from "@/components/shared/data-table";
+import type { LoyaltySchema } from "@/lib/loyalty-schema-types";
+import { formatValueAsString, generateColumnsFromSchema, getNestedValue } from "@/services/loyalty/utils/grid-columns";
+
+export interface SimpleDataTableProps<T = unknown> {
+  schema: LoyaltySchema;
+  data: T[];
+  emptyMessage?: string;
+}
+
+export function SimpleDataTable<T = unknown>({
+  schema,
+  data,
+  emptyMessage = "No data available.",
+}: SimpleDataTableProps<T>) {
+  const columns = useMemo<ColumnDef<T>[]>(() => {
+    return generateColumnsFromSchema(schema).map((col) => ({
+      id: col.key,
+      header: col.label,
+      sortable: false,
+      cell: (row: T) => (
+        <span className="text-sm">{formatValueAsString(getNestedValue(row, col.key), col.dataType)}</span>
+      ),
+    }));
+  }, [schema]);
+
+  return <DataTable<T> columns={columns} data={data} emptyMessage={emptyMessage} />;
+}
diff --git a/Journeys/Journeys.UX/src/components/loyalty/dynamic-data/date-range-filter.tsx b/Journeys/Journeys.UX/src/components/loyalty/dynamic-data/date-range-filter.tsx
new file mode 100644
index 0000000..98b4acf
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/loyalty/dynamic-data/date-range-filter.tsx
@@ -0,0 +1,65 @@
+"use client";
+
+/**
+ * Date range filter for the dynamic data table.
+ *
+ * Emits ISO date strings (`fromDate`, `toDate`) suitable for use in the
+ * event query filters built by `DynamicDataTable`.
+ */
+
+import { useCallback } from "react";
+
+import { Button } from "@/components/ui/button";
+import { Input } from "@/components/ui/input";
+import { cn } from "@/lib/utils";
+
+export interface DateRangeFilterProps {
+  fromDate: string | null;
+  toDate: string | null;
+  onChange: (fromDate: string | null, toDate: string | null) => void;
+  className?: string;
+  placeholder?: string;
+}
+
+export function DateRangeFilter({
+  fromDate,
+  toDate,
+  onChange,
+  className,
+  placeholder = "Filter by date",
+}: DateRangeFilterProps) {
+  const handleFromChange = useCallback(
+    (value: string) => onChange(value || null, toDate),
+    [onChange, toDate]
+  );
+
+  const handleToChange = useCallback(
+    (value: string) => onChange(fromDate, value || null),
+    [onChange, fromDate]
+  );
+
+  return (
+    <div className={cn("flex items-center gap-2", className)}>
+      <Input
+        type="date"
+        value={fromDate ?? ""}
+        onChange={(event) => handleFromChange(event.target.value)}
+        aria-label={`${placeholder} from`}
+        className="h-8 w-[150px]"
+      />
+      <span className="text-sm text-muted-foreground">to</span>
+      <Input
+        type="date"
+        value={toDate ?? ""}
+        onChange={(event) => handleToChange(event.target.value)}
+        aria-label={`${placeholder} to`}
+        className="h-8 w-[150px]"
+      />
+      {(fromDate || toDate) && (
+        <Button size="sm" variant="ghost" onClick={() => onChange(null, null)}>
+          Clear dates
+        </Button>
+      )}
+    </div>
+  );
+}
diff --git a/Journeys/Journeys.UX/src/components/loyalty/dynamic-data/index.ts b/Journeys/Journeys.UX/src/components/loyalty/dynamic-data/index.ts
new file mode 100644
index 0000000..a26f330
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/loyalty/dynamic-data/index.ts
@@ -0,0 +1,3 @@
+export { DynamicDataTable, type DynamicDataTableProps } from "./dynamic-data-table";
+export { SimpleDataTable, type SimpleDataTableProps } from "./simple-data-table";
+export { DateRangeFilter, type DateRangeFilterProps } from "./date-range-filter";
diff --git a/Journeys/Journeys.UX/src/components/loyalty/accounts/accounts-selection-modal.tsx b/Journeys/Journeys.UX/src/components/loyalty/accounts/accounts-selection-modal.tsx
new file mode 100644
index 0000000..975277a
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/loyalty/accounts/accounts-selection-modal.tsx
@@ -0,0 +1,368 @@
+"use client";
+
+/**
+ * AccountsSelectionModal ΓÇö loyalty account picker used by grids that are gated
+ * on an account selection.
+ *
+ *   - schema provided ΓåÆ schema-driven dynamic columns + rich `eventData`/`displayLabel` payload
+ *   - schema omitted ΓåÆ the modal loads `LoyaltyAccountDetails` itself, and falls
+ *     back to a fixed column set if that fetch fails
+ *
+ * Selection state persists across search/filter changes (selected accounts NOT
+ * in the current filter remain in the chip strip and the apply payload). Lookup
+ * is keyed by `loyaltyAccountId`.
+ */
+
+import { useCallback, useMemo, useState } from "react";
+import { useQuery } from "@tanstack/react-query";
+import { Search, X } from "lucide-react";
+
+import {
+  Dialog,
+  DialogContent,
+  DialogDescription,
+  DialogFooter,
+  DialogHeader,
+  DialogTitle,
+} from "@/components/ui/dialog";
+import { Button } from "@/components/ui/button";
+import { Input } from "@/components/ui/input";
+import { Badge } from "@/components/ui/badge";
+import { Checkbox } from "@/components/ui/checkbox";
+import { Alert, AlertDescription } from "@/components/ui/alert";
+import { DataTable, type ColumnDef } from "@/components/shared/data-table";
+import { Skeleton } from "@/components/ui/skeleton";
+
+import { getSchemaByName, queryData } from "@/services/loyalty/actions";
+import type { LoyaltySchema } from "@/lib/loyalty-schema-types";
+import { LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME } from "@/services/loyalty/schema-names";
+import { loyaltyKeys } from "@/services/loyalty/query-keys";
+import { formatValueAsString, generateColumnsFromSchema, getNestedValue } from "@/services/loyalty/utils/grid-columns";
+import {
+  LOYALTY_ACCOUNT_IDENTIFIER_QUERY,
+  resolveLoyaltyAccountXReference,
+} from "@/services/loyalty/utils/account-identifiers";
+
+interface AccountRecord {
+  loyaltyAccountId: string;
+  event: Record<string, unknown>;
+}
+
+export interface SelectedLoyaltyAccount {
+  loyaltyAccountId: string;
+  displayLabel: string;
+  eventData: Record<string, unknown>;
+}
+
+export interface AccountsSelectionModalProps {
+  open: boolean;
+  onOpenChange: (open: boolean) => void;
+  /** Called with the selected accounts when the user clicks Apply. */
+  onApply: (selected: SelectedLoyaltyAccount[]) => void;
+  /** Pre-selected accounts on open. */
+  initialSelections?: SelectedLoyaltyAccount[];
+  /**
+   * Optional schema to drive columns and label. When omitted, the modal:
+   *   - fetches `LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME` from `getSchemaByName`
+   *   - falls back to a fixed column set if the schema fetch fails
+   */
+  schema?: LoyaltySchema | null;
+  /** Title shown in the dialog header. */
+  title?: string;
+  /** Description shown in the dialog header. */
+  description?: string;
+  /** Search field placeholder. Defaults to "Search by account ID". */
+  searchPlaceholder?: string;
+}
+
+const FIXED_COLUMNS_FALLBACK = [
+  { key: "customerid", label: "Customer ID", dataType: "String", sortable: true },
+  { key: "region", label: "Region", dataType: "String", sortable: true },
+] as const;
+
+function getAccountDisplayId(row: AccountRecord): string {
+  return resolveLoyaltyAccountXReference(row as unknown as Record<string, unknown>, row.loyaltyAccountId);
+}
+
+export function AccountsSelectionModal({
+  open,
+  onOpenChange,
+  onApply,
+  initialSelections = [],
+  schema: providedSchema = null,
+  title = "Select Loyalty Accounts",
+  description = "Choose one or more loyalty accounts to view their data.",
+  searchPlaceholder = "Search by account ID",
+}: AccountsSelectionModalProps) {
+  const [filterText, setFilterText] = useState("");
+  const [activeFilter, setActiveFilter] = useState("");
+  const [selected, setSelected] = useState<Map<string, SelectedLoyaltyAccount>>(
+    () => new Map(initialSelections.map((s) => [s.loyaltyAccountId, s]))
+  );
+
+  // Re-seed on open via onOpenChange, not useEffect (avoids react-hooks/set-state-in-effect)
+  const handleOpenChange = (next: boolean) => {
+    if (next) {
+      setSelected(new Map(initialSelections.map((s) => [s.loyaltyAccountId, s])));
+      setFilterText("");
+      setActiveFilter("");
+    }
+    onOpenChange(next);
+  };
+
+  const schemaQuery = useQuery({
+    queryKey: loyaltyKeys.schemas.byName(LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME),
+    queryFn: async () => {
+      const result = await getSchemaByName(LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME);
+      if (!result.success) throw new Error(result.error ?? "Failed to load schema");
+      return result.data ?? null;
+    },
+    enabled: open && !providedSchema,
+    staleTime: 5 * 60 * 1000,
+  });
+
+  const schema: LoyaltySchema | null = providedSchema ?? schemaQuery.data ?? null;
+
+  const dataQuery = useQuery({
+    queryKey: ["loyalty-accounts-modal", LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME, activeFilter],
+    queryFn: async () => {
+      const queryString = activeFilter ? LOYALTY_ACCOUNT_IDENTIFIER_QUERY : "";
+      const queryArgs = activeFilter ? { "@id": activeFilter } : {};
+
+      const response = await queryData<AccountRecord>({
+        schemaName: LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME,
+        queryString,
+        queryArgs,
+        pageSize: 50,
+      });
+
+      if (!response.success) throw new Error(response.error ?? "Failed to load accounts");
+      return response.data ?? [];
+    },
+    enabled: open,
+  });
+
+  const firstColumnKey = useMemo<string | null>(() => {
+    if (schema) {
+      const cols = generateColumnsFromSchema(schema);
+      return cols[0]?.key ?? null;
+    }
+    return FIXED_COLUMNS_FALLBACK[0]?.key ?? null;
+  }, [schema]);
+
+  const buildDisplayLabel = useCallback(
+    (row: AccountRecord): string => {
+      const displayId = getAccountDisplayId(row);
+      if (displayId) return displayId;
+
+      if (firstColumnKey) {
+        const value = getNestedValue(row.event, firstColumnKey);
+        if (value !== null && value !== undefined) return String(value);
+      }
+      return row.loyaltyAccountId;
+    },
+    [firstColumnKey]
+  );
+
+  const toggleSelection = useCallback(
+    (row: AccountRecord) => {
+      setSelected((prev) => {
+        const next = new Map(prev);
+        if (next.has(row.loyaltyAccountId)) {
+          next.delete(row.loyaltyAccountId);
+        } else {
+          next.set(row.loyaltyAccountId, {
+            loyaltyAccountId: row.loyaltyAccountId,
+            displayLabel: buildDisplayLabel(row),
+            eventData: row.event,
+          });
+        }
+        return next;
+      });
+    },
+    [buildDisplayLabel]
+  );
+
+  // `columns` recomputes when `schema`, `selected`, or `toggleSelection` changes.
+  // `selected` is a Map and changes on every selection toggle, but columns are
+  // cheap to recompute inside a modal. The cell renderers for the checkbox column
+  // need the current `selected` state and `toggleSelection` ΓÇö both listed in deps.
+  const columns: ColumnDef<AccountRecord>[] = useMemo(() => {
+    const selectColumn: ColumnDef<AccountRecord> = {
+      id: "select",
+      header: "",
+      cell: (row) => (
+        <span
+          className="inline-flex"
+          onClick={(event) => event.stopPropagation()}
+          onKeyDown={(event) => event.stopPropagation()}
+        >
+          <Checkbox
+            checked={selected.has(row.loyaltyAccountId)}
+            aria-label={`Select account ${row.loyaltyAccountId}`}
+            onCheckedChange={() => toggleSelection(row)}
+          />
+        </span>
+      ),
+    };
+    const accountIdColumn: ColumnDef<AccountRecord> = {
+      id: "extAccountId",
+      header: "External account ID",
+      cell: (row) => (
+        <div className="space-y-0.5">
+          <code className="text-xs">{getAccountDisplayId(row)}</code>
+          {row.loyaltyAccountId !== getAccountDisplayId(row) && (
+            <p className="text-[11px] text-muted-foreground break-all">{row.loyaltyAccountId}</p>
+          )}
+        </div>
+      ),
+    };
+
+    if (schema) {
+      const schemaCols = generateColumnsFromSchema(schema)
+        .filter(
+          (col) => !["id", "profileid", "profileId", "customerid", "customerId", "extAccountId"].includes(col.key)
+        )
+        .map((col) => ({
+          id: col.key,
+          header: col.label,
+          cell: (row: AccountRecord) => (
+            <span className="text-sm">{formatValueAsString(getNestedValue(row.event, col.key), col.dataType)}</span>
+          ),
+        }));
+      return [selectColumn, accountIdColumn, ...schemaCols];
+    }
+
+    return [
+      selectColumn,
+      accountIdColumn,
+      ...FIXED_COLUMNS_FALLBACK.map((col) => ({
+        id: col.key,
+        header: col.label,
+        cell: (row: AccountRecord) => (
+          <span className="text-sm">{formatValueAsString(getNestedValue(row.event, col.key), col.dataType)}</span>
+        ),
+      })),
+    ];
+  }, [schema, selected, toggleSelection]);
+
+  const removeBadge = useCallback((id: string) => {
+    setSelected((prev) => {
+      const next = new Map(prev);
+      next.delete(id);
+      return next;
+    });
+  }, []);
+
+  const handleSearch = useCallback(() => {
+    setActiveFilter(filterText.trim());
+  }, [filterText]);
+
+  const handleReset = useCallback(() => {
+    setFilterText("");
+    setActiveFilter("");
+    setSelected(new Map());
+  }, []);
+
+  const handleApply = useCallback(() => {
+    onApply(Array.from(selected.values()));
+    onOpenChange(false);
+  }, [selected, onApply, onOpenChange]);
+
+  const data = dataQuery.data ?? [];
+  const isLoading = dataQuery.isLoading || (!providedSchema && schemaQuery.isLoading);
+  const error = (dataQuery.error as Error | undefined)?.message ?? (schemaQuery.error as Error | undefined)?.message;
+
+  return (
+    <Dialog open={open} onOpenChange={handleOpenChange}>
+      <DialogContent className="flex h-[calc(100vh-2rem)] max-h-[760px] w-[calc(100vw-2rem)] max-w-[calc(100vw-2rem)] flex-col overflow-hidden sm:max-w-[calc(100vw-2rem)] xl:max-w-7xl">
+        <DialogHeader className="min-w-0">
+          <DialogTitle>{title}</DialogTitle>
+          <DialogDescription>{description}</DialogDescription>
+        </DialogHeader>
+
+        {selected.size > 0 && (
+          <div className="max-h-20 overflow-y-auto pr-1">
+            <div className="flex flex-wrap gap-2">
+              {Array.from(selected.values()).map((account) => (
+                <Badge key={account.loyaltyAccountId} variant="secondary" className="gap-1">
+                  {account.displayLabel}
+                  <button
+                    type="button"
+                    onClick={() => removeBadge(account.loyaltyAccountId)}
+                    className="hover:text-destructive ml-1"
+                    aria-label={`Remove ${account.displayLabel}`}
+                  >
+                    <X className="h-3 w-3" />
+                  </button>
+                </Badge>
+              ))}
+            </div>
+          </div>
+        )}
+
+        <div className="flex shrink-0 items-center gap-2">
+          <div className="relative flex-1 max-w-sm">
+            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
+            <Input
+              type="text"
+              placeholder={searchPlaceholder}
+              value={filterText}
+              onChange={(e) => setFilterText(e.target.value)}
+              onKeyDown={(e) => {
+                if (e.key === "Enter") {
+                  e.preventDefault();
+                  handleSearch();
+                }
+              }}
+              className="pl-9"
+              aria-label={searchPlaceholder}
+            />
+          </div>
+          <Button size="sm" onClick={handleSearch}>
+            Search
+          </Button>
+          <Button size="sm" variant="outline" onClick={handleReset}>
+            Reset
+          </Button>
+        </div>
+
+        {error && (
+          <Alert variant="destructive">
+            <AlertDescription>{error}</AlertDescription>
+          </Alert>
+        )}
+
+        <div className="min-h-0 flex-1 overflow-y-auto rounded-md">
+          {isLoading && data.length === 0 ? (
+            <div className="space-y-2">
+              {Array.from({ length: 6 }).map((_, i) => (
+                <Skeleton key={i} className="h-10 w-full" />
+              ))}
+            </div>
+          ) : (
+            <div className="max-w-full overflow-x-auto pb-2">
+              <DataTable<AccountRecord>
+                columns={columns}
+                data={data}
+                isLoading={dataQuery.isLoading}
+                emptyMessage="No accounts found. Try adjusting your search."
+                getRowId={(row) => row.loyaltyAccountId}
+                onRowClick={toggleSelection}
+              />
+            </div>
+          )}
+        </div>
+
+        <DialogFooter className="shrink-0 items-center border-t pt-3">
+          <Button variant="outline" onClick={() => onOpenChange(false)}>
+            Cancel
+          </Button>
+          <Button onClick={handleApply} disabled={selected.size === 0 && initialSelections.length === 0}>
+            Apply ({selected.size})
+          </Button>
+        </DialogFooter>
+      </DialogContent>
+    </Dialog>
+  );
+}
diff --git a/Journeys/Journeys.UX/src/components/loyalty/accounts/loyalty-accounts-list-client.tsx b/Journeys/Journeys.UX/src/components/loyalty/accounts/loyalty-accounts-list-client.tsx
new file mode 100644
index 0000000..fe7eff4
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/loyalty/accounts/loyalty-accounts-list-client.tsx
@@ -0,0 +1,85 @@
+"use client";
+
+/**
+ * LoyaltyAccountsListClient ΓÇö schema-driven list view via `<DynamicDataTable>`.
+ *
+ * Loads the `LoyaltyAccountDetails` schema and passes it to `<DynamicDataTable>`
+ * with `skipAccountSelection` and `detailRoutePath="/loyalty/accounts"`. Columns
+ * come from the schema's grid config; no fixed column set.
+ */
+
+import { useMemo } from "react";
+import { useQuery } from "@tanstack/react-query";
+
+import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
+import { Skeleton } from "@/components/ui/skeleton";
+
+import type { LoyaltySchema } from "@/lib/loyalty-schema-types";
+import { getSchemaByName } from "@/services/loyalty/actions";
+import { loyaltyKeys } from "@/services/loyalty/query-keys";
+import { LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME } from "@/services/loyalty/schema-names";
+import {
+  LOYALTY_ACCOUNT_IDENTIFIER_SEARCH_FIELDS,
+  resolveLoyaltyAccountId,
+} from "@/services/loyalty/utils/account-identifiers";
+import { DynamicDataTable } from "@/components/loyalty/dynamic-data";
+
+export function LoyaltyAccountsListClient() {
+  const schemaQuery = useQuery({
+    queryKey: loyaltyKeys.schemas.byName(LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME),
+    queryFn: async () => {
+      const r = await getSchemaByName(LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME);
+      if (!r.success) throw new Error(r.error ?? "Failed to load schema");
+      return r.data ?? null;
+    },
+    staleTime: 5 * 60 * 1000,
+  });
+
+  // `SchemaListItem` carries the same fields the table reads; widen it to the
+  // schema shape the schema-driven components expect.
+  const schema = useMemo<LoyaltySchema | null>(
+    () => (schemaQuery.data ? { ...schemaQuery.data } : null),
+    [schemaQuery.data]
+  );
+
+  if (schemaQuery.isLoading) {
+    return (
+      <div className="space-y-4">
+        <Skeleton className="h-12 w-full" />
+        <Skeleton className="h-64 w-full" />
+      </div>
+    );
+  }
+
+  if (schemaQuery.error) {
+    return (
+      <Alert variant="destructive">
+        <AlertTitle>Failed to load schema</AlertTitle>
+        <AlertDescription>{(schemaQuery.error as Error).message}</AlertDescription>
+      </Alert>
+    );
+  }
+
+  if (!schema) {
+    return (
+      <p className="empty">The LoyaltyAccountDetails schema is missing or not Live.</p>
+    );
+  }
+
+  return (
+    <DynamicDataTable
+      schema={schema}
+      skipAccountSelection
+      detailRoutePath="/loyalty/accounts"
+      emptyMessage="No loyalty accounts found."
+      countLabel={{ singular: "loyalty account", plural: "loyalty accounts" }}
+      exportFilePrefix="loyalty-accounts"
+      extraSearchFields={LOYALTY_ACCOUNT_IDENTIFIER_SEARCH_FIELDS}
+      fallbackIdColumn={{
+        label: "Loyalty account ID",
+        getValue: (row) => resolveLoyaltyAccountId(row as Record<string, unknown>, row.id ?? ""),
+      }}
+      getDetailId={(row) => resolveLoyaltyAccountId(row as Record<string, unknown>, row.id ?? "")}
+    />
+  );
+}
diff --git a/Journeys/Journeys.UX/src/components/loyalty/accounts/loyalty-accounts-list-client.source.test.ts b/Journeys/Journeys.UX/src/components/loyalty/accounts/loyalty-accounts-list-client.source.test.ts
new file mode 100644
index 0000000..8a828ba
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/loyalty/accounts/loyalty-accounts-list-client.source.test.ts
@@ -0,0 +1,29 @@
+import { readFileSync } from "node:fs";
+import { dirname, resolve } from "node:path";
+import { fileURLToPath } from "node:url";
+
+import { describe, expect, it } from "vitest";
+
+const source = readFileSync(
+  resolve(dirname(fileURLToPath(import.meta.url)), "loyalty-accounts-list-client.tsx"),
+  "utf8"
+);
+
+describe("loyalty accounts list client", () => {
+  it("does not link to an account model builder", () => {
+    expect(source).not.toContain("/builder");
+    expect(source).not.toContain("builder");
+  });
+
+  it("does not link to data explorer routes", () => {
+    expect(source).not.toContain("data-explorer");
+  });
+
+  it("states the missing-schema case in one sentence with no call to action", () => {
+    expect(source).toContain("The LoyaltyAccountDetails schema is missing or not Live.");
+  });
+
+  it("routes row clicks at the accounts detail route", () => {
+    expect(source).toContain('detailRoutePath="/loyalty/accounts"');
+  });
+});
diff --git a/Journeys/Journeys.UX/src/components/loyalty/accounts/index.ts b/Journeys/Journeys.UX/src/components/loyalty/accounts/index.ts
new file mode 100644
index 0000000..addc5ea
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/loyalty/accounts/index.ts
@@ -0,0 +1,6 @@
+export { LoyaltyAccountsListClient } from "./loyalty-accounts-list-client";
+export {
+  AccountsSelectionModal,
+  type AccountsSelectionModalProps,
+  type SelectedLoyaltyAccount,
+} from "./accounts-selection-modal";
```
