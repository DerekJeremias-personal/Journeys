# Task 3 uncommitted diff

```diff
diff --git a/Journeys/Journeys.UX/src/services/loyalty/actions.ts b/Journeys/Journeys.UX/src/services/loyalty/actions.ts
index af313d2..79a71e4 100644
--- a/Journeys/Journeys.UX/src/services/loyalty/actions.ts
+++ b/Journeys/Journeys.UX/src/services/loyalty/actions.ts
@@ -1,23 +1,195 @@
 "use server";
 
-import type { ApiResponse, CampaignListItem, SchemaListItem } from "@/lib/api-types";
+import type {
+  AccountPointBalance,
+  AgentConversationListItem,
+  ApiResponse,
+  CampaignListItem,
+  PointAccountTypeListItem,
+  QueryDataParams,
+  SchemaListItem
+} from "@/lib/api-types";
+import type { Campaign } from "@/lib/campaign-types";
 import { journeysFetch } from "@/lib/journeys-fetch";
 import { getManyModelsListBody, isUsableModelId, LOYALTY_MODEL_TYPE } from "@/lib/loyalty-model";
-import { extractEntities, normalizeSchema, pickLiveSchema } from "./parse-list";
+import { mergeCampaignLists } from "@/lib/campaign-authoring-list";
+import {
+  extractContinuationToken,
+  extractConversationIds,
+  extractEntities,
+  normalizeCampaignRow,
+  normalizeSchema,
+  pickLiveSchema
+} from "./parse-list";
 
 const TENANT_SLUG = "session";
 
+function asCampaignRows(data: unknown): CampaignListItem[] {
+  return extractEntities(data)
+    .map(normalizeCampaignRow)
+    .filter((row): row is CampaignListItem => row !== null);
+}
+
+async function getCampaignsByStatus(
+  status: string
+): Promise<ApiResponse<CampaignListItem[]>> {
+  const res = await journeysFetch<unknown>(`campaigns/${TENANT_SLUG}/query`, {
+    method: "POST",
+    body: {
+      query: "c.status = @status",
+      parameters: { "@status": status },
+      pageSize: 100,
+      continuationToken: null
+    }
+  });
+  if (!res.success) return res as ApiResponse<CampaignListItem[]>;
+  return { ...res, data: asCampaignRows(res.data) };
+}
+
 export async function getCampaigns(): Promise<ApiResponse<CampaignListItem[]>> {
-  const res = await journeysFetch<unknown>(`campaigns/${TENANT_SLUG}/getall`, {
+  const getAll = await journeysFetch<unknown>(`campaigns/${TENANT_SLUG}/getall`, {
     method: "POST",
     body: { pageSize: 100, continuationToken: null }
   });
+  const getAllPage: ApiResponse<CampaignListItem[]> = getAll.success
+    ? { ...getAll, data: asCampaignRows(getAll.data) }
+    : (getAll as ApiResponse<CampaignListItem[]>);
+
+  const pages = await Promise.all([
+    Promise.resolve(getAllPage),
+    getCampaignsByStatus("draft"),
+    getCampaignsByStatus("pause")
+  ]);
+  const succeeded = pages.filter((page) => page.success);
+  if (succeeded.length === 0) {
+    return pages[0] ?? {
+      success: false,
+      error: "Failed to load campaigns",
+      timestamp: new Date().toISOString()
+    };
+  }
+  return {
+    success: true,
+    data: mergeCampaignLists(succeeded.map((page) => page.data ?? [])),
+    timestamp: new Date().toISOString()
+  };
+}
+
+export async function getCampaignsByFilters(
+  filters: Record<string, unknown>
+): Promise<ApiResponse<CampaignListItem[]>> {
+  const res = await journeysFetch<unknown>(`campaigns/${TENANT_SLUG}/query`, {
+    method: "POST",
+    body: filters
+  });
+  if (!res.success) return res as ApiResponse<CampaignListItem[]>;
+  return { ...res, data: asCampaignRows(res.data) };
+}
+
+export async function getCampaign(
+  id: string,
+  status?: string
+): Promise<ApiResponse<CampaignListItem>> {
+  return journeysFetch<CampaignListItem>(`campaigns/${TENANT_SLUG}/${id}`, {
+    searchParams: { campaignStatus: status }
+  });
+}
+
+export async function updateCampaign(
+  data: Campaign | CampaignListItem
+): Promise<ApiResponse<CampaignListItem>> {
+  return journeysFetch<CampaignListItem>(`campaigns/${TENANT_SLUG}/save`, {
+    method: "POST",
+    body: data
+  });
+}
+
+export async function deleteCampaign(
+  id: string,
+  status: string
+): Promise<ApiResponse<void>> {
+  return journeysFetch<void>(`campaigns/${TENANT_SLUG}/${id}`, {
+    method: "DELETE",
+    searchParams: { status }
+  });
+}
+
+export async function copyCampaign(
+  id: string,
+  status: string,
+  name?: string
+): Promise<ApiResponse<CampaignListItem>> {
+  const res = await journeysFetch<unknown>(`campaigns/${TENANT_SLUG}/${id}/copy`, {
+    method: "POST",
+    searchParams: { status },
+    body: name ? { name } : {}
+  });
+  if (!res.success) return res as ApiResponse<CampaignListItem>;
+  const copied = normalizeCampaignRow(res.data);
+  return { ...res, data: copied ?? undefined };
+}
+
+export async function restoreCampaign(id: string): Promise<ApiResponse<CampaignListItem>> {
+  return journeysFetch<CampaignListItem>(`campaigns/${TENANT_SLUG}/${id}/restore`, {
+    method: "POST",
+    searchParams: { status: "archive" }
+  });
+}
+
+export async function validateCampaign(
+  data: CampaignListItem
+): Promise<ApiResponse<unknown>> {
+  return journeysFetch<unknown>(`campaigns/${TENANT_SLUG}/validate`, {
+    method: "POST",
+    body: data
+  });
+}
+
+export async function getCampaignVersions(
+  ext: string
+): Promise<ApiResponse<CampaignListItem[]>> {
+  const res = await journeysFetch<unknown>(`campaigns/${TENANT_SLUG}/versions/${ext}`);
   if (!res.success) return res as ApiResponse<CampaignListItem[]>;
   return { ...res, data: extractEntities(res.data) as CampaignListItem[] };
 }
 
+export async function getArchivedCampaigns(): Promise<ApiResponse<CampaignListItem[]>> {
+  const res = await journeysFetch<unknown>(`campaigns/${TENANT_SLUG}/archived`);
+  if (!res.success) return res as ApiResponse<CampaignListItem[]>;
+  return { ...res, data: extractEntities(res.data) as CampaignListItem[] };
+}
+
+export async function getLiveByExt(ext: string): Promise<ApiResponse<CampaignListItem>> {
+  return journeysFetch<CampaignListItem>(`campaigns/${TENANT_SLUG}/live/${ext}`);
+}
+
+export async function getDraftByExt(ext: string): Promise<ApiResponse<CampaignListItem>> {
+  return journeysFetch<CampaignListItem>(`campaigns/${TENANT_SLUG}/draft/${ext}`);
+}
+
+export async function getPointAccountTypes(): Promise<ApiResponse<PointAccountTypeListItem[]>> {
+  const res = await journeysFetch<unknown>(
+    `campaigns/${TENANT_SLUG}/pointaccounttype/getall`,
+    {
+      method: "POST",
+      body: { pageSize: 100, continuationToken: null }
+    }
+  );
+  if (!res.success) return res as ApiResponse<PointAccountTypeListItem[]>;
+  return { ...res, data: extractEntities(res.data) as PointAccountTypeListItem[] };
+}
+
+export async function listAgentConversations(): Promise<
+  ApiResponse<AgentConversationListItem[]>
+> {
+  const res = await journeysFetch<unknown>("campaign-agent/conversations");
+  if (!res.success) return res as ApiResponse<AgentConversationListItem[]>;
+  const items = extractConversationIds(res.data).map((conversationId) => ({ conversationId }));
+  return { ...res, data: items };
+}
+
 export async function getAllSchemas(): Promise<ApiResponse<SchemaListItem[]>> {
   const res = await journeysFetch<unknown>(`schemas/${TENANT_SLUG}/model/all`, {
     method: "POST",
     body: getManyModelsListBody()
   });
@@ -35,27 +207,76 @@ export async function getSchemaByName(
   const res = await getAllSchemas();
   if (!res.success) return res as ApiResponse<SchemaListItem | null>;
   return { ...res, data: pickLiveSchema(res.data ?? [], name) };
 }
 
-export async function queryData(schemaName: string): Promise<ApiResponse<Record<string, unknown>[]>> {
-  if (!isUsableModelId(schemaName)) {
+/**
+ * Account reads. These are GETs, so they never carry the `X-Journeys-Audit`
+ * header ΓÇö only account/journey writes do.
+ */
+export async function getLoyaltyAccountById(
+  id: string
+): Promise<ApiResponse<Record<string, unknown>>> {
+  return journeysFetch<Record<string, unknown>>(`accounts/${TENANT_SLUG}/${id}`);
+}
+
+export async function getLoyaltyAccountByExternalId(
+  extId: string
+): Promise<ApiResponse<Record<string, unknown>>> {
+  return journeysFetch<Record<string, unknown>>(`accounts/${TENANT_SLUG}/ext/${extId}`);
+}
+
+export async function getAccountPointBalances(
+  loyaltyAccountId: string
+): Promise<ApiResponse<AccountPointBalance[]>> {
+  const res = await journeysFetch<unknown>(
+    `accounts/${TENANT_SLUG}/points/balances/${loyaltyAccountId}`
+  );
+  if (!res.success) return res as ApiResponse<AccountPointBalance[]>;
+  return { ...res, data: extractEntities(res.data) as AccountPointBalance[] };
+}
+
+/** Point ledger rows for the account; campaign progress reads balances from these. */
+export async function getAccountPoints(
+  loyaltyAccountId: string
+): Promise<ApiResponse<unknown[]>> {
+  const res = await journeysFetch<unknown>(`accounts/${TENANT_SLUG}/points/${loyaltyAccountId}`);
+  if (!res.success) return res as ApiResponse<unknown[]>;
+  return { ...res, data: extractEntities(res.data) };
+}
+
+export async function queryData<T = Record<string, unknown>>(
+  params: QueryDataParams
+): Promise<ApiResponse<T[]>> {
+  if (!isUsableModelId(params.schemaName)) {
     return {
       success: false,
       error: "A real model name is required; unknown model ids are not sent.",
       timestamp: new Date().toISOString()
     };
   }
-  const res = await journeysFetch<unknown>(`events/${TENANT_SLUG}/${schemaName}/admin/query`, {
-    method: "POST",
-    body: {
-      query: "",
-      parameters: {},
-      pageSize: 50,
-      continuationToken: null,
-      sortBy: "",
-      sortOrder: "ASC"
+  const pageSize = params.pageSize ?? 50;
+  const res = await journeysFetch<unknown>(
+    `events/${TENANT_SLUG}/${params.schemaName}/admin/query`,
+    {
+      method: "POST",
+      body: {
+        query: params.queryString ?? "",
+        parameters: params.queryArgs ?? {},
+        pageSize,
+        continuationToken: params.continuationToken ?? null,
+        sortBy: params.sortBy ?? "",
+        sortOrder: params.sortOrder ?? "ASC",
+        loyaltyAccountId: params.loyaltyAccountId
+      }
     }
-  });
-  if (!res.success) return res as ApiResponse<Record<string, unknown>[]>;
-  return { ...res, data: extractEntities(res.data) as Record<string, unknown>[] };
+  );
+  if (!res.success) return res as ApiResponse<T[]>;
+  return {
+    ...res,
+    data: extractEntities(res.data) as T[],
+    meta: {
+      continuationToken: extractContinuationToken(res.data),
+      pageSize
+    }
+  };
 }
diff --git a/Journeys/Journeys.UX/src/services/loyalty/parse-list.test.ts b/Journeys/Journeys.UX/src/services/loyalty/parse-list.test.ts
index c9cd727..4e89af1 100644
--- a/Journeys/Journeys.UX/src/services/loyalty/parse-list.test.ts
+++ b/Journeys/Journeys.UX/src/services/loyalty/parse-list.test.ts
@@ -1,7 +1,15 @@
 import { describe, expect, it } from "vitest";
-import { attributeNamesFromRows, extractEntities, normalizeSchema, pickLiveSchema } from "./parse-list";
+import {
+  attributeNamesFromRows,
+  extractContinuationToken,
+  extractConversationIds,
+  extractEntities,
+  normalizeCampaignRow,
+  normalizeSchema,
+  pickLiveSchema
+} from "./parse-list";
 
 describe("extractEntities", () => {
   it("returns arrays as-is", () => {
     expect(extractEntities([{ a: 1 }])).toEqual([{ a: 1 }]);
   });
@@ -11,10 +19,44 @@ describe("extractEntities", () => {
   it("reads Items", () => {
     expect(extractEntities({ Items: [{ id: "2" }] })).toEqual([{ id: "2" }]);
   });
 });
 
+describe("extractConversationIds", () => {
+  it("reads camelCase items.conversationId", () => {
+    expect(extractConversationIds({ items: [{ conversationId: "abc" }] })).toEqual(["abc"]);
+  });
+
+  it("reads PascalCase Items.ConversationId", () => {
+    expect(
+      extractConversationIds({ Items: [{ ConversationId: "def" }] })
+    ).toEqual(["def"]);
+  });
+});
+
+describe("normalizeCampaignRow", () => {
+  it("keeps the journey tree so account tier progress can read it", () => {
+    const row = normalizeCampaignRow({
+      id: "camp-1",
+      name: "Tier campaign",
+      journey: { id: "journey-1", rootNodeId: "root-1", children: [{ id: "tier-bronze" }] }
+    });
+    expect(row?.journey).toEqual({
+      id: "journey-1",
+      rootNodeId: "root-1",
+      children: [{ id: "tier-bronze" }]
+    });
+  });
+
+  it("reads a PascalCase journey and omits a non-object one", () => {
+    expect(normalizeCampaignRow({ id: "camp-1", Journey: { id: "journey-1" } })?.journey).toEqual({
+      id: "journey-1"
+    });
+    expect(normalizeCampaignRow({ id: "camp-1", journey: "nope" })?.journey).toBeUndefined();
+  });
+});
+
 describe("normalizeSchema", () => {
   it("reads PascalCase Name/Status/ModelType", () => {
     const s = normalizeSchema({
       ID: "guid-1",
       Name: "LoyaltyAccountDetails",
@@ -53,10 +95,22 @@ describe("pickLiveSchema", () => {
       )
     ).toBeNull();
   });
 });
 
+describe("extractContinuationToken", () => {
+  it("reads continuationToken", () => {
+    expect(extractContinuationToken({ entities: [], continuationToken: "tok" })).toBe("tok");
+  });
+  it("reads ContinuationToken", () => {
+    expect(extractContinuationToken({ Entities: [], ContinuationToken: "tok2" })).toBe("tok2");
+  });
+  it("returns null when missing", () => {
+    expect(extractContinuationToken({ entities: [] })).toBeNull();
+  });
+});
+
 describe("attributeNamesFromRows", () => {
   it("uses keys from the first row", () => {
     expect(attributeNamesFromRows([{ id: "1", name: "A" }])).toEqual([
       { name: "id" },
       { name: "name" }
diff --git a/Journeys/Journeys.UX/src/services/loyalty/parse-list.ts b/Journeys/Journeys.UX/src/services/loyalty/parse-list.ts
index 26e3d06..c3039a3 100644
--- a/Journeys/Journeys.UX/src/services/loyalty/parse-list.ts
+++ b/Journeys/Journeys.UX/src/services/loyalty/parse-list.ts
@@ -1,16 +1,49 @@
-import type { SchemaListItem } from "@/lib/api-types";
+import type { CampaignListItem, SchemaListItem } from "@/lib/api-types";
 import { LOYALTY_MODEL_TYPE } from "@/lib/loyalty-model";
 
 function readString(row: Record<string, unknown>, ...keys: string[]): string | undefined {
   for (const key of keys) {
     const value = row[key];
     if (typeof value === "string" && value.trim()) return value.trim();
   }
   return undefined;
 }
 
+export function extractConversationIds(data: unknown): string[] {
+  const ids: string[] = [];
+  for (const item of extractEntities(data)) {
+    if (!item || typeof item !== "object") continue;
+    const id = readString(item as Record<string, unknown>, "conversationId", "ConversationId");
+    if (id) ids.push(id);
+  }
+  return ids;
+}
+
+function readRecord(row: Record<string, unknown>, ...keys: string[]): Record<string, unknown> | undefined {
+  for (const key of keys) {
+    const value = row[key];
+    if (value && typeof value === "object" && !Array.isArray(value)) return value as Record<string, unknown>;
+  }
+  return undefined;
+}
+
+export function normalizeCampaignRow(row: unknown): CampaignListItem | null {
+  if (!row || typeof row !== "object") return null;
+  const rec = row as Record<string, unknown>;
+  return {
+    id: readString(rec, "id", "Id", "ID"),
+    name: readString(rec, "name", "Name"),
+    status: readString(rec, "status", "Status"),
+    extCampaignId: readString(rec, "extCampaignId", "ExtCampaignId"),
+    startDate: readString(rec, "startDate", "StartDate"),
+    endDate: readString(rec, "endDate", "EndDate"),
+    // Account tier/progress reads the journey tree off the campaign list.
+    journey: readRecord(rec, "journey", "Journey")
+  };
+}
+
 export function extractEntities(data: unknown): unknown[] {
   if (Array.isArray(data)) return data;
   if (data && typeof data === "object") {
     const obj = data as Record<string, unknown>;
     for (const key of ["entities", "Entities", "items", "Items"]) {
@@ -27,18 +60,26 @@ export function normalizeSchema(row: unknown): SchemaListItem | null {
   return {
     id: readString(rec, "id", "ID", "Id"),
     name: readString(rec, "name", "Name"),
     status: readString(rec, "status", "Status"),
     modelType: readString(rec, "modelType", "ModelType"),
+    tag: readString(rec, "tag", "Tag"),
     attributes: Array.isArray(rec.attributes)
       ? (rec.attributes as SchemaListItem["attributes"])
       : Array.isArray(rec.Attributes)
         ? (rec.Attributes as SchemaListItem["attributes"])
         : undefined
   };
 }
 
+export function extractContinuationToken(data: unknown): string | null {
+  if (!data || typeof data !== "object" || Array.isArray(data)) return null;
+  const rec = data as Record<string, unknown>;
+  const token = rec.continuationToken ?? rec.ContinuationToken;
+  return typeof token === "string" && token.length > 0 ? token : null;
+}
+
 export function attributeNamesFromRows(
   rows: Record<string, unknown>[]
 ): { name: string }[] {
   if (rows.length === 0) return [{ name: "id" }, { name: "name" }];
   return Object.keys(rows[0]).map((name) => ({ name }));
diff --git a/Journeys/Journeys.UX/src/components/loyalty/accounts/loyalty-account-detail-client.tsx b/Journeys/Journeys.UX/src/components/loyalty/accounts/loyalty-account-detail-client.tsx
new file mode 100644
index 0000000..e4c7e15
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/loyalty/accounts/loyalty-account-detail-client.tsx
@@ -0,0 +1,372 @@
+"use client";
+
+/**
+ * LoyaltyAccountDetailClient ΓÇö composes the account detail screen.
+ *
+ * Layout:
+ *   - Account summary and point balances
+ *   - Schema-driven `<DynamicEntityDetails>` for the remaining fields
+ *   - `<AccountCampaignProgress>` ΓÇö campaign journey progress
+ *   - `<EventableModelsSection>` ΓÇö events grouped by Live eventable schemas
+ */
+
+import { useMemo } from "react";
+import { useQuery } from "@tanstack/react-query";
+
+import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
+import { Badge } from "@/components/ui/badge";
+import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
+import { Skeleton } from "@/components/ui/skeleton";
+
+import { DynamicEntityDetails } from "@/components/loyalty/dynamic-data";
+import { PointsAccountsCard } from "@/components/loyalty/points";
+import type { AccountPointBalance } from "@/lib/api-types";
+import type { LoyaltySchema } from "@/lib/loyalty-schema-types";
+import {
+  getAccountPointBalances,
+  getCampaigns,
+  getLoyaltyAccountByExternalId,
+  getLoyaltyAccountById,
+  getPointAccountTypes,
+  getSchemaByName,
+  queryData,
+} from "@/services/loyalty/actions";
+import { loyaltyKeys } from "@/services/loyalty/query-keys";
+import { LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME } from "@/services/loyalty/schema-names";
+import {
+  LOYALTY_ACCOUNT_IDENTIFIER_QUERY,
+  normalizeEntityWithEvent,
+  resolveLoyaltyAccountId,
+} from "@/services/loyalty/utils/account-identifiers";
+import { getSchemaFieldValue } from "@/services/loyalty/utils/grid-columns";
+
+import { AccountCampaignProgress } from "./account-campaign-progress";
+import { EventableModelsSection } from "./eventable-models-section";
+import { resolveCurrentTierLabel, type JourneyRef } from "./account-tier";
+
+interface AccountDetailEntity {
+  id?: string;
+  loyaltyAccountId?: string;
+  event?: Record<string, unknown>;
+  [key: string]: unknown;
+}
+
+export interface LoyaltyAccountDetailClientProps {
+  accountId: string;
+}
+
+const INTERNAL_ACCOUNT_ID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
+
+function readAccountText(entity: Record<string, unknown>, fields: string[]): string | null {
+  for (const field of fields) {
+    const value = getSchemaFieldValue(entity, field);
+    if (typeof value === "string" && value.trim().length > 0) return value;
+    if (typeof value === "number" && Number.isFinite(value)) return value.toString();
+  }
+  return null;
+}
+
+function getStatusLabel(value: unknown): string {
+  if (typeof value === "boolean") return value ? "Active" : "Inactive";
+  if (typeof value === "string" && value.trim().length > 0) return value;
+  return "Unknown";
+}
+
+function looksLikeInternalAccountId(value: string): boolean {
+  return INTERNAL_ACCOUNT_ID_PATTERN.test(value);
+}
+
+function getExternalAccountIds(account: AccountDetailEntity): string[] {
+  const ids = new Set<string>();
+  const extAccountId = account["extAccountId"];
+  if (typeof extAccountId === "string" && extAccountId.trim().length > 0) ids.add(extAccountId);
+
+  const knownExternalIds = account["knownExternalIds"];
+  if (Array.isArray(knownExternalIds)) {
+    for (const id of knownExternalIds) {
+      if (typeof id === "string" && id.trim().length > 0) ids.add(id);
+    }
+  }
+
+  return [...ids];
+}
+
+async function getAccountDetailEntityByIdentifier(
+  identifier: string
+): Promise<Record<string, unknown> | null> {
+  const result = await queryData<AccountDetailEntity>({
+    schemaName: LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME,
+    queryString: LOYALTY_ACCOUNT_IDENTIFIER_QUERY,
+    queryArgs: { "@id": identifier },
+    pageSize: 1,
+  });
+  if (!result.success) throw new Error(result.error ?? "Failed to load account");
+  return normalizeEntityWithEvent(result.data?.[0]);
+}
+
+async function mergeAccountWithEventPayload(
+  account: AccountDetailEntity
+): Promise<AccountDetailEntity> {
+  for (const externalId of getExternalAccountIds(account)) {
+    const eventEntity = await getAccountDetailEntityByIdentifier(externalId);
+    if (eventEntity) return { ...account, ...eventEntity };
+  }
+  return account;
+}
+
+function AccountSummaryCard({
+  entity,
+  loyaltyAccountId,
+  pointAccounts,
+  currentTierLabel,
+}: {
+  entity: Record<string, unknown>;
+  loyaltyAccountId: string;
+  pointAccounts: AccountPointBalance[];
+  currentTierLabel: string | null;
+}) {
+  const firstName = readAccountText(entity, ["firstName", "firstname"]);
+  const lastName = readAccountText(entity, ["lastName", "lastname"]);
+  const joinedName = [firstName, lastName].filter(Boolean).join(" ");
+  const fullName =
+    readAccountText(entity, ["fullName", "fullname", "memberName", "name"]) ??
+    (joinedName.length > 0 ? joinedName : null);
+  const externalId = readAccountText(entity, [
+    "sourceRecordId",
+    "extAccountId",
+    "customerid",
+    "customerId",
+    "profileid",
+    "profileId",
+    "eventId",
+  ]);
+  const memberIdentifier =
+    readAccountText(entity, ["loyaltyMemberId", "emailAddress", "email", "mobilePhone", "mobilephone"]) ??
+    externalId;
+  const status = getStatusLabel(
+    getSchemaFieldValue(entity, "status") ??
+      getSchemaFieldValue(entity, "accountStatus") ??
+      getSchemaFieldValue(entity, "isActive") ??
+      getSchemaFieldValue(entity, "active") ??
+      getSchemaFieldValue(entity, "isloyaltymember") ??
+      getSchemaFieldValue(entity, "isLoyaltyMember")
+  );
+  const currentTier =
+    currentTierLabel ?? readAccountText(entity, ["currentTier", "tier", "tierName", "campaignName"]);
+  const totalBalance = pointAccounts.reduce((sum, account) => sum + (account.currentBalance ?? 0), 0);
+
+  const fields = [
+    { label: "External ID", value: externalId },
+    { label: "Member identifier", value: memberIdentifier },
+    { label: "Current tier", value: currentTier },
+    { label: "Points balance", value: pointAccounts.length > 0 ? totalBalance.toLocaleString() : null },
+    { label: "Status", value: status },
+    { label: "Account ID", value: loyaltyAccountId },
+  ];
+
+  return (
+    <Card>
+      <CardHeader className="flex flex-row items-start justify-between gap-4">
+        <div>
+          <CardTitle className="text-base">Account profile</CardTitle>
+          <p className="mt-1 text-sm text-muted-foreground">{fullName ?? externalId ?? loyaltyAccountId}</p>
+        </div>
+        <Badge variant="outline">{status}</Badge>
+      </CardHeader>
+      <CardContent>
+        <dl className="grid gap-3 text-sm sm:grid-cols-2">
+          {fields.map((field) => (
+            <div key={field.label} className="min-w-0 rounded-md border bg-muted/20 p-3">
+              <dt className="text-xs font-medium uppercase text-muted-foreground">{field.label}</dt>
+              <dd className="mt-1 break-words font-medium">{field.value ?? "ΓÇö"}</dd>
+            </div>
+          ))}
+        </dl>
+      </CardContent>
+    </Card>
+  );
+}
+
+export function LoyaltyAccountDetailClient({ accountId }: LoyaltyAccountDetailClientProps) {
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
+  const entityQuery = useQuery({
+    queryKey: loyaltyKeys.accounts.detail(accountId),
+    queryFn: async () => {
+      if (looksLikeInternalAccountId(accountId)) {
+        const byInternal = await getLoyaltyAccountById(accountId);
+        if (byInternal.success && byInternal.data) {
+          return mergeAccountWithEventPayload(byInternal.data as AccountDetailEntity);
+        }
+      }
+
+      const entity = await getAccountDetailEntityByIdentifier(accountId);
+      if (entity) return entity;
+
+      const byInternal = await getLoyaltyAccountById(accountId);
+      if (byInternal.success && byInternal.data) {
+        return mergeAccountWithEventPayload(byInternal.data as AccountDetailEntity);
+      }
+
+      const byExternal = await getLoyaltyAccountByExternalId(accountId);
+      if (byExternal.success && byExternal.data) {
+        const account = byExternal.data as AccountDetailEntity;
+        const eventEntity = await getAccountDetailEntityByIdentifier(accountId);
+        return eventEntity ? { ...account, ...eventEntity } : account;
+      }
+
+      return null;
+    },
+  });
+
+  // Points / journey / ledger endpoints partition by the loyalty account id.
+  // The URL may be either the schema document id or the loyalty account id, so
+  // resolve it from the loaded entity and fall back to the route param.
+  const entityRecord = (entityQuery.data ?? null) as Record<string, unknown> | null;
+  const loyaltyAccountId = resolveLoyaltyAccountId(entityRecord, accountId);
+
+  const balancesQuery = useQuery({
+    queryKey: loyaltyKeys.points.balancesByAccount(loyaltyAccountId),
+    queryFn: async () => {
+      const r = await getAccountPointBalances(loyaltyAccountId);
+      if (!r.success) throw new Error(r.error ?? "Failed to load balances");
+      return r.data ?? [];
+    },
+    enabled: Boolean(entityQuery.data),
+    refetchOnWindowFocus: true,
+  });
+
+  const patsQuery = useQuery({
+    queryKey: loyaltyKeys.pointAccountTypes.all,
+    queryFn: async () => {
+      const r = await getPointAccountTypes();
+      if (!r.success) throw new Error(r.error ?? "Failed to load point account types");
+      return r.data ?? [];
+    },
+    staleTime: 5 * 60 * 1000,
+  });
+
+  const pointsAccounts: AccountPointBalance[] = useMemo(() => balancesQuery.data ?? [], [balancesQuery.data]);
+  const pointAccountTypes = patsQuery.data ?? [];
+
+  const accountJourneyQuery = useQuery({
+    queryKey: loyaltyKeys.accounts.loyaltyDetail(loyaltyAccountId),
+    queryFn: async () => {
+      const r = await getLoyaltyAccountById(loyaltyAccountId);
+      if (!r.success) throw new Error(r.error ?? "Failed to load account journeys");
+      return r.data ?? null;
+    },
+    enabled: Boolean(entityQuery.data),
+  });
+
+  const campaignsQuery = useQuery({
+    queryKey: loyaltyKeys.campaigns.list({}),
+    queryFn: async () => {
+      const r = await getCampaigns();
+      if (!r.success) throw new Error(r.error ?? "Failed to load campaigns");
+      return r.data ?? [];
+    },
+    enabled: Boolean(entityQuery.data),
+  });
+
+  const currentTierLabel = useMemo(() => {
+    const accountJourneys =
+      (accountJourneyQuery.data as { journeys?: JourneyRef[] } | null)?.journeys ?? [];
+    return resolveCurrentTierLabel(campaignsQuery.data ?? [], accountJourneys);
+  }, [accountJourneyQuery.data, campaignsQuery.data]);
+
+  // `SchemaListItem` carries the fields the detail view reads; widen it to the
+  // schema shape the schema-driven components expect.
+  const schema = useMemo<LoyaltySchema | null>(
+    () => (schemaQuery.data ? { ...schemaQuery.data } : null),
+    [schemaQuery.data]
+  );
+
+  if (schemaQuery.isLoading || entityQuery.isLoading) {
+    return (
+      <div className="space-y-4">
+        <Skeleton className="h-32 w-full" />
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
+  if (entityQuery.error) {
+    return (
+      <Alert variant="destructive">
+        <AlertTitle>Failed to load account</AlertTitle>
+        <AlertDescription>{(entityQuery.error as Error).message}</AlertDescription>
+      </Alert>
+    );
+  }
+
+  if (!schema) {
+    return <p className="empty">The LoyaltyAccountDetails schema is missing or not Live.</p>;
+  }
+
+  if (!entityQuery.data) {
+    return (
+      <Alert>
+        <AlertTitle>Account not found</AlertTitle>
+        <AlertDescription>No account exists with ID {accountId}.</AlertDescription>
+      </Alert>
+    );
+  }
+
+  const entity = entityQuery.data as Record<string, unknown>;
+
+  const balancesError = balancesQuery.error as Error | null;
+  const patsError = patsQuery.error as Error | null;
+
+  return (
+    <div className="space-y-6">
+      {(balancesError || patsError) && (
+        <Alert variant="destructive">
+          <AlertTitle>Couldn&apos;t load point account data</AlertTitle>
+          <AlertDescription>{balancesError?.message ?? patsError?.message ?? "Unknown error."}</AlertDescription>
+        </Alert>
+      )}
+      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_400px]">
+        <AccountSummaryCard
+          entity={entity}
+          loyaltyAccountId={loyaltyAccountId}
+          pointAccounts={pointsAccounts}
+          currentTierLabel={currentTierLabel}
+        />
+        <PointsAccountsCard
+          pointAccounts={pointsAccounts}
+          pointAccountTypes={pointAccountTypes}
+          isLoading={balancesQuery.isLoading || patsQuery.isLoading}
+        />
+      </div>
+      <DynamicEntityDetails
+        schema={schema}
+        entity={entity}
+        showTitle={false}
+        footerSlot={
+          <>
+            <AccountCampaignProgress loyaltyAccountId={loyaltyAccountId} />
+            <EventableModelsSection loyaltyAccountId={loyaltyAccountId} />
+          </>
+        }
+      />
+    </div>
+  );
+}
diff --git a/Journeys/Journeys.UX/src/components/loyalty/accounts/account-tier.ts b/Journeys/Journeys.UX/src/components/loyalty/accounts/account-tier.ts
new file mode 100644
index 0000000..0dd3caf
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/loyalty/accounts/account-tier.ts
@@ -0,0 +1,87 @@
+/**
+ * Journey/tier helpers shared by the account detail surfaces.
+ *
+ * Campaign journey payloads arrive with mixed casing (`children` / `Children`,
+ * `id` / `Id`), so every reader here accepts both spellings.
+ */
+
+export type Loose = Record<string, unknown>;
+
+export type JourneyRef = {
+  rootJourneyNodeId?: string | null;
+  journeyNodeIds?: string[] | null;
+};
+
+export type CampaignWithJourney = {
+  id?: string | null;
+  name?: string | null;
+  journey?: unknown;
+};
+
+export function asLoose(value: unknown): Loose {
+  return value && typeof value === "object" && !Array.isArray(value) ? (value as Loose) : {};
+}
+
+export function normalizeId(value: unknown): string {
+  return typeof value === "string" ? value.trim().toLowerCase() : "";
+}
+
+export function getNodeId(node: Loose): string | null {
+  const id = node["id"] ?? node["Id"];
+  return typeof id === "string" && id.length > 0 ? id : null;
+}
+
+export function getNodeName(node: Loose): string | null {
+  const name = node["name"] ?? node["Name"];
+  return typeof name === "string" && name.trim().length > 0 ? name.trim() : null;
+}
+
+export function getJourneyRootId(journey: Loose): string {
+  return normalizeId(journey["rootNodeId"] ?? journey["RootNodeId"] ?? journey["id"] ?? journey["Id"]);
+}
+
+export function getJourneyEnrollmentId(journey: Loose): string {
+  return normalizeId(journey["id"] ?? journey["Id"]);
+}
+
+export function flattenJourneyNodes(node: Loose): Loose[] {
+  const out: Loose[] = [node];
+  const children = Array.isArray(node["children"])
+    ? (node["children"] as Loose[])
+    : Array.isArray(node["Children"])
+      ? (node["Children"] as Loose[])
+      : [];
+  for (const child of children) out.push(...flattenJourneyNodes(asLoose(child)));
+  return out;
+}
+
+export function journeyMatchesAccountJourney(journey: Loose, accountJourney: JourneyRef): boolean {
+  const accountRootId = normalizeId(accountJourney.rootJourneyNodeId);
+  if (!accountRootId) return false;
+  return accountRootId === getJourneyRootId(journey) || accountRootId === getJourneyEnrollmentId(journey);
+}
+
+export function resolveCurrentTierLabel(
+  campaigns: CampaignWithJourney[],
+  accountJourneys: JourneyRef[]
+): string | null {
+  for (const accountJourney of accountJourneys) {
+    const currentNodeId = accountJourney.journeyNodeIds?.length
+      ? accountJourney.journeyNodeIds[accountJourney.journeyNodeIds.length - 1]
+      : null;
+    if (!currentNodeId) continue;
+
+    const campaign = campaigns.find((candidate) =>
+      journeyMatchesAccountJourney(asLoose(candidate.journey), accountJourney)
+    );
+    if (!campaign) continue;
+
+    const currentNode = flattenJourneyNodes(asLoose(campaign.journey)).find(
+      (node) => normalizeId(getNodeId(node)) === normalizeId(currentNodeId)
+    );
+
+    return getNodeName(asLoose(currentNode)) ?? currentNodeId;
+  }
+
+  return null;
+}
diff --git a/Journeys/Journeys.UX/src/components/loyalty/accounts/account-tier.test.ts b/Journeys/Journeys.UX/src/components/loyalty/accounts/account-tier.test.ts
new file mode 100644
index 0000000..12b4b7e
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/loyalty/accounts/account-tier.test.ts
@@ -0,0 +1,45 @@
+import { describe, expect, it } from "vitest";
+
+import { resolveCurrentTierLabel } from "./account-tier";
+
+describe("resolveCurrentTierLabel", () => {
+  it("resolves the current account journey node to the human tier name", () => {
+    const campaigns = [
+      {
+        id: "campaign-1",
+        name: "Tier campaign",
+        journey: {
+          id: "journey-1",
+          rootNodeId: "root-1",
+          name: "Tier Program",
+          children: [
+            { id: "tier-bronze", name: "Bronze Tier" },
+            { id: "tier-silver", name: "Silver Tier" },
+          ],
+        },
+      },
+    ];
+
+    expect(
+      resolveCurrentTierLabel(campaigns, [
+        { rootJourneyNodeId: "journey-1", journeyNodeIds: ["tier-bronze", "tier-silver"] },
+      ])
+    ).toBe("Silver Tier");
+  });
+
+  it("falls back to the current tier id when a node label is missing", () => {
+    const campaigns = [
+      {
+        id: "campaign-1",
+        journey: {
+          rootNodeId: "root-1",
+          children: [{ id: "tier-node-without-name" }],
+        },
+      },
+    ];
+
+    expect(
+      resolveCurrentTierLabel(campaigns, [{ rootJourneyNodeId: "root-1", journeyNodeIds: ["tier-node-without-name"] }])
+    ).toBe("tier-node-without-name");
+  });
+});
diff --git a/Journeys/Journeys.UX/src/components/loyalty/accounts/account-campaign-progress.tsx b/Journeys/Journeys.UX/src/components/loyalty/accounts/account-campaign-progress.tsx
new file mode 100644
index 0000000..d6e29c3
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/loyalty/accounts/account-campaign-progress.tsx
@@ -0,0 +1,560 @@
+"use client";
+
+/**
+ * AccountCampaignProgress ΓÇö journey progress for the campaigns an account is
+ * enrolled in.
+ *
+ * Data sources:
+ *   - `getLoyaltyAccountById` ΓÇö `journeys[]` with `journeyNodeIds`, which tells
+ *     us the node the account currently sits on.
+ *   - `getCampaigns` ΓÇö each campaign carries its `journey` tree.
+ *   - `getAccountPoints` ΓÇö current balance per point account type.
+ *   - `getPointAccountTypes` ΓÇö names for labels.
+ *
+ * Progress %: for each enrolled campaign, find the current node, read the
+ * node's navigation transition `navConstraint` (NumericPropertyRule or
+ * AndRule) to extract the point threshold, then compare it against the
+ * account's balance for that point account type.
+ */
+
+import { useMemo, useState } from "react";
+import { useQuery } from "@tanstack/react-query";
+import { Award, CheckCircle2, ChevronDown, ChevronUp, CornerDownRight, Info } from "lucide-react";
+
+import { Alert, AlertDescription } from "@/components/ui/alert";
+import { Button } from "@/components/ui/button";
+import { Card, CardContent } from "@/components/ui/card";
+import { Command, CommandEmpty, CommandInput, CommandItem, CommandList } from "@/components/ui/command";
+import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
+import { Skeleton } from "@/components/ui/skeleton";
+import { cn } from "@/lib/utils";
+
+import {
+  getAccountPoints,
+  getCampaigns,
+  getLoyaltyAccountById,
+  getPointAccountTypes,
+} from "@/services/loyalty/actions";
+import { loyaltyKeys } from "@/services/loyalty/query-keys";
+import {
+  asLoose,
+  flattenJourneyNodes,
+  getJourneyRootId,
+  getNodeId,
+  journeyMatchesAccountJourney,
+  normalizeId,
+  resolveCurrentTierLabel,
+  type JourneyRef,
+  type Loose,
+} from "./account-tier";
+
+export interface AccountCampaignProgressProps {
+  loyaltyAccountId: string;
+  className?: string;
+}
+
+/** A point ledger/balance row as the progress math reads it. */
+type PointRow = { pointAccountTypeId?: string | null; currentBalance?: number | null };
+
+type ProgressTarget = { patId: string | null; lowerBound: number; upperBound: number | null; isRange: boolean };
+
+function getCampaignKey(
+  campaign: { id?: string | null; name?: string | null; journey?: unknown },
+  index: number
+): string {
+  const idPart = normalizeId(campaign.id);
+  const namePart = normalizeId(campaign.name);
+  const journeyPart = getJourneyRootId(asLoose(campaign.journey));
+  return `${idPart || "no-id"}|${namePart || "no-name"}|${journeyPart || "no-journey"}|${index}`;
+}
+
+function getNumericValue(value: unknown): number | null {
+  if (typeof value === "number" && Number.isFinite(value)) return value;
+  if (typeof value === "string") {
+    const parsed = Number(value.trim());
+    return Number.isFinite(parsed) ? parsed : null;
+  }
+  return null;
+}
+
+function matchesComparison(comparison: unknown, expectedNumeric: number, expectedLabel: string): boolean {
+  if (typeof comparison === "number") return comparison === expectedNumeric;
+  if (typeof comparison === "string") return comparison.toLowerCase() === expectedLabel.toLowerCase();
+  return false;
+}
+
+function getNavigationConstraint(node: Loose): Loose | null {
+  const nav = asLoose(node["navigation"] ?? node["Navigation"]);
+  const transition = asLoose(nav["transition"] ?? nav["Transition"]);
+  const rawConstraint = transition["navConstraint"] ?? transition["NavConstraint"];
+  if (!rawConstraint || typeof rawConstraint !== "object") return null;
+  return asLoose(rawConstraint);
+}
+
+function extractProgressTargetFromConstraint(constraint: Loose): ProgressTarget | null {
+  const kind = String(constraint["Kind"] ?? constraint["kind"] ?? "").trim();
+  if (!kind) return null;
+
+  if (kind === "NumericPropertyRule") {
+    const right = asLoose(constraint["RightProvider"] ?? constraint["rightProvider"]);
+    const left = asLoose(constraint["LeftProvider"] ?? constraint["leftProvider"]);
+    const target = getNumericValue(right["Value"]) ?? getNumericValue(right["value"]);
+    const patId = ((left["PointAccountTypeId"] ?? left["pointAccountTypeId"]) as string | undefined) ?? null;
+    if (target === null) return null;
+    return { patId, lowerBound: Math.max(0, target), upperBound: null, isRange: false };
+  }
+
+  if (kind === "AndRule") {
+    const children = Array.isArray(constraint["Children"])
+      ? (constraint["Children"] as Loose[])
+      : Array.isArray(constraint["children"])
+        ? (constraint["children"] as Loose[])
+        : [];
+
+    const lower = children.find((c) => {
+      const ck = asLoose(c);
+      const ek = asLoose(ck["Evaluator"] ?? ck["evaluator"]);
+      const comparison = ek["Comparison"] ?? ek["comparison"];
+      return (
+        (ck["Kind"] === "NumericPropertyRule" || ck["kind"] === "NumericPropertyRule") &&
+        matchesComparison(comparison, 3, "GreaterThanOrEqual")
+      );
+    });
+
+    const upper = children.find((c) => {
+      const ck = asLoose(c);
+      const ek = asLoose(ck["Evaluator"] ?? ck["evaluator"]);
+      const comparison = ek["Comparison"] ?? ek["comparison"];
+      return (
+        (ck["Kind"] === "NumericPropertyRule" || ck["kind"] === "NumericPropertyRule") &&
+        matchesComparison(comparison, 5, "LessThanOrEqual")
+      );
+    });
+
+    if (!lower) return null;
+
+    const lowerRule = asLoose(lower);
+    const lowerRight = asLoose(lowerRule["RightProvider"] ?? lowerRule["rightProvider"]);
+    const lowerLeft = asLoose(lowerRule["LeftProvider"] ?? lowerRule["leftProvider"]);
+    const lowerValue = getNumericValue(lowerRight["Value"]) ?? getNumericValue(lowerRight["value"]);
+    if (lowerValue === null) return null;
+
+    const patId = ((lowerLeft["PointAccountTypeId"] ?? lowerLeft["pointAccountTypeId"]) as string | undefined) ?? null;
+    const upperRule = asLoose(upper);
+    const upperRight = asLoose(upperRule["RightProvider"] ?? upperRule["rightProvider"]);
+    const upperValue = getNumericValue(upperRight["Value"]) ?? getNumericValue(upperRight["value"]);
+
+    return {
+      patId,
+      lowerBound: Math.max(0, lowerValue),
+      upperBound: upperValue ?? null,
+      isRange: true,
+    };
+  }
+
+  return null;
+}
+
+function extractProgressTarget(node: Loose): ProgressTarget | null {
+  const constraint = getNavigationConstraint(node);
+  if (!constraint) return null;
+  return extractProgressTargetFromConstraint(constraint);
+}
+
+function isProgressVisibleForNode(node: Loose): boolean {
+  const constraint = getNavigationConstraint(node);
+  if (!constraint) return false;
+
+  const left = asLoose(constraint["leftProvider"] ?? constraint["LeftProvider"]);
+  const right = asLoose(constraint["rightProvider"] ?? constraint["RightProvider"]);
+  const leftValue = left["value"] ?? left["Value"];
+  const rightValue = right["value"] ?? right["Value"];
+
+  return !(leftValue === false && rightValue === true);
+}
+
+function getCurrentBalance(points: PointRow[], patId: string): number {
+  const pointEntry = points.find((p) => normalizeId(p.pointAccountTypeId) === normalizeId(patId));
+  return pointEntry?.currentBalance ?? 0;
+}
+
+function getProgressPercentage(node: Loose, points: PointRow[]): number {
+  const target = extractProgressTarget(node);
+  if (!target?.patId) return 0;
+
+  const currentBalance = getCurrentBalance(points, target.patId);
+  if (target.isRange) {
+    const min = target.lowerBound;
+    const max = target.upperBound ?? (min > 0 ? min * 2 : 1);
+    if (max <= min) return currentBalance >= min ? 100 : 0;
+    return Math.min(100, Math.max(0, ((currentBalance - min) / (max - min)) * 100));
+  }
+  if (target.lowerBound <= 0) return 100;
+  return Math.min(100, (currentBalance / target.lowerBound) * 100);
+}
+
+function findNextNode(node: Loose, campaignId: string, flatNodesByCampaign: Record<string, Loose[]>): Loose | null {
+  const nodeId = normalizeId(getNodeId(node));
+  if (!nodeId) return null;
+  const flat = flatNodesByCampaign[campaignId] ?? [];
+  const idx = flat.findIndex((n) => normalizeId(getNodeId(n)) === nodeId);
+  if (idx < 0 || idx >= flat.length - 1) return null;
+  return flat[idx + 1] ?? null;
+}
+
+function getProgressText(
+  node: Loose,
+  campaignId: string,
+  flatNodesByCampaign: Record<string, Loose[]>,
+  points: PointRow[]
+): string {
+  const nextNode = findNextNode(node, campaignId, flatNodesByCampaign);
+  if (!nextNode) return "N/A";
+  const nextTarget = extractProgressTarget(nextNode);
+  const currentTarget = extractProgressTarget(node);
+  const currentBalance = currentTarget?.patId ? getCurrentBalance(points, currentTarget.patId) : 0;
+  const requiredLower = nextTarget?.lowerBound ?? 0;
+  const remaining = Math.max(requiredLower - currentBalance, 0);
+  return `${currentBalance.toLocaleString()} / ${requiredLower.toLocaleString()} points (${remaining.toLocaleString()} remaining)`;
+}
+
+function getRuleCalculationText(node: Loose, points: PointRow[], patNames: Map<string, string>): string {
+  const target = extractProgressTarget(node);
+  if (!target?.patId) return "N/A";
+
+  const currentBalance = getCurrentBalance(points, target.patId);
+  const accountName = patNames.get(normalizeId(target.patId)) ?? "Points";
+  if (target.isRange) {
+    if (target.upperBound === null) return "N/A";
+    return `${accountName} Requirements\n${target.lowerBound.toLocaleString()} Γëñ ${currentBalance.toLocaleString()} < ${target.upperBound.toLocaleString()}`;
+  }
+  return `${accountName} Requirements\n${target.lowerBound.toLocaleString()} Γëñ ${currentBalance.toLocaleString()}`;
+}
+
+interface JourneyProgressNodeProps {
+  node: Loose;
+  level: number;
+  points: PointRow[];
+  campaignId: string;
+  flatNodesByCampaign: Record<string, Loose[]>;
+  patNames: Map<string, string>;
+}
+
+function JourneyProgressNode({
+  node,
+  level,
+  points,
+  campaignId,
+  flatNodesByCampaign,
+  patNames,
+}: JourneyProgressNodeProps) {
+  const pct = getProgressPercentage(node, points);
+  const pctLabel = `${pct.toFixed(2)}%`;
+  const nodeName = String(node["name"] ?? "Unnamed node");
+  const achievedDate = (node["achievedDate"] ?? node["AchievedDate"]) as string | undefined;
+  const ruleText = getRuleCalculationText(node, points, patNames);
+  const progressText = getProgressText(node, campaignId, flatNodesByCampaign, points);
+  const children = Array.isArray(node["children"])
+    ? (node["children"] as Loose[])
+    : Array.isArray(node["Children"])
+      ? (node["Children"] as Loose[])
+      : [];
+
+  const fillWidth = pct > 0 ? `${Math.min(100, Math.max(0, pct))}%` : "0%";
+
+  return (
+    <div className="space-y-2">
+      <div className="flex items-center gap-2">
+        {level > 0 && <CornerDownRight className="h-4 w-4 text-muted-foreground" />}
+        <div className="relative h-8 flex-1 overflow-hidden rounded-full border bg-muted/50">
+          {pct > 0 && (
+            <div
+              className="h-full rounded-full bg-primary/90 transition-all duration-300"
+              style={{ width: fillWidth }}
+            />
+          )}
+          <div className="absolute inset-0 flex items-center justify-between px-3 text-xs">
+            <div className={cn("font-semibold", pct > 0 ? "text-primary-foreground" : "text-foreground")}>
+              {pct >= 100 ? (
+                <span className="inline-flex items-center gap-1.5">
+                  <CheckCircle2 className="h-3.5 w-3.5" />
+                  {nodeName}
+                </span>
+              ) : (
+                nodeName
+              )}
+            </div>
+            {pct > 0 && <span className="font-semibold text-primary-foreground">{pctLabel}</span>}
+          </div>
+        </div>
+
+        <Popover>
+          <PopoverTrigger asChild>
+            <Button type="button" variant="ghost" size="icon" className="h-7 w-7">
+              <Info className="h-4 w-4" />
+            </Button>
+          </PopoverTrigger>
+          <PopoverContent align="end" className="w-[320px] space-y-4">
+            <div>
+              <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Rule calculation</p>
+              <p className="mt-1 whitespace-pre-line text-sm">{ruleText}</p>
+            </div>
+            <div>
+              <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Progress</p>
+              <p className="mt-1 text-sm">{progressText}</p>
+            </div>
+          </PopoverContent>
+        </Popover>
+      </div>
+
+      {achievedDate && <p className="pl-2 text-xs text-muted-foreground">Achieved {achievedDate}</p>}
+
+      {children.length > 0 && (
+        <div className="space-y-2 pl-5">
+          {children.map((child, index) => (
+            <JourneyProgressNode
+              key={getNodeId(child) ?? `${level}-${index}`}
+              node={asLoose(child)}
+              level={level + 1}
+              points={points}
+              campaignId={campaignId}
+              flatNodesByCampaign={flatNodesByCampaign}
+              patNames={patNames}
+            />
+          ))}
+        </div>
+      )}
+    </div>
+  );
+}
+
+export function AccountCampaignProgress({ loyaltyAccountId, className }: AccountCampaignProgressProps) {
+  const [isExpanded, setIsExpanded] = useState(true);
+  const [selectedCampaignKey, setSelectedCampaignKey] = useState<string>("");
+  const [campaignPickerOpen, setCampaignPickerOpen] = useState(false);
+
+  const campaignsQuery = useQuery({
+    queryKey: loyaltyKeys.campaigns.list({}),
+    queryFn: async () => {
+      const r = await getCampaigns();
+      if (!r.success) throw new Error(r.error ?? "Failed to load campaigns");
+      return r.data ?? [];
+    },
+  });
+
+  const accountQuery = useQuery({
+    queryKey: loyaltyKeys.accounts.loyaltyDetail(loyaltyAccountId),
+    queryFn: async () => {
+      const r = await getLoyaltyAccountById(loyaltyAccountId);
+      if (!r.success) throw new Error(r.error ?? "Failed to load account");
+      return r.data ?? null;
+    },
+  });
+
+  const pointsQuery = useQuery({
+    queryKey: loyaltyKeys.points.ledgersByAccount(loyaltyAccountId),
+    queryFn: async () => {
+      const r = await getAccountPoints(loyaltyAccountId);
+      if (!r.success) throw new Error(r.error ?? "Failed to load points");
+      return (r.data ?? []) as PointRow[];
+    },
+  });
+
+  const patsQuery = useQuery({
+    queryKey: loyaltyKeys.pointAccountTypes.all,
+    queryFn: async () => {
+      const r = await getPointAccountTypes();
+      if (!r.success) throw new Error(r.error ?? "Failed to load point types");
+      return r.data ?? [];
+    },
+    staleTime: 5 * 60 * 1000,
+  });
+
+  const isLoading = campaignsQuery.isLoading || accountQuery.isLoading || pointsQuery.isLoading;
+
+  const accountJourneys = useMemo<JourneyRef[]>(() => {
+    const account = accountQuery.data;
+    if (!account) return [];
+    return (account as { journeys?: JourneyRef[] }).journeys ?? [];
+  }, [accountQuery.data]);
+
+  const enrolledCampaigns = useMemo(() => {
+    const allCampaigns = campaignsQuery.data ?? [];
+    return allCampaigns.filter((campaign) =>
+      accountJourneys.some((accountJourney) => journeyMatchesAccountJourney(asLoose(campaign.journey), accountJourney))
+    );
+  }, [campaignsQuery.data, accountJourneys]);
+
+  const keyedCampaigns = useMemo(
+    () => enrolledCampaigns.map((campaign, index) => ({ key: getCampaignKey(campaign, index), campaign })),
+    [enrolledCampaigns]
+  );
+
+  const flatNodesByCampaign = useMemo<Record<string, Loose[]>>(() => {
+    return keyedCampaigns.reduce<Record<string, Loose[]>>((acc, entry) => {
+      acc[entry.key] = flattenJourneyNodes(asLoose(entry.campaign.journey));
+      return acc;
+    }, {});
+  }, [keyedCampaigns]);
+
+  const campaignCurrentNodeMap = useMemo<Record<string, Loose | null>>(() => {
+    return keyedCampaigns.reduce<Record<string, Loose | null>>((acc, entry) => {
+      const journey = asLoose(entry.campaign.journey);
+      const accountJourney = accountJourneys.find((candidate) => journeyMatchesAccountJourney(journey, candidate));
+      const currentNodeId = accountJourney?.journeyNodeIds?.length
+        ? accountJourney.journeyNodeIds[accountJourney.journeyNodeIds.length - 1]
+        : null;
+      const allNodes = flatNodesByCampaign[entry.key] ?? [];
+      acc[entry.key] = currentNodeId
+        ? (allNodes.find((n) => normalizeId(getNodeId(n)) === normalizeId(currentNodeId)) ?? null)
+        : null;
+      return acc;
+    }, {});
+  }, [accountJourneys, keyedCampaigns, flatNodesByCampaign]);
+
+  const selectedCampaignKeyResolved = useMemo(() => {
+    if (keyedCampaigns.length === 0) return "";
+    if (selectedCampaignKey && keyedCampaigns.some((entry) => entry.key === selectedCampaignKey)) {
+      return selectedCampaignKey;
+    }
+    const firstAccountJourney = accountJourneys[0];
+    const preferred =
+      keyedCampaigns.find((entry) =>
+        firstAccountJourney ? journeyMatchesAccountJourney(asLoose(entry.campaign.journey), firstAccountJourney) : false
+      ) ?? keyedCampaigns[0];
+    return preferred?.key ?? "";
+  }, [accountJourneys, keyedCampaigns, selectedCampaignKey]);
+
+  const selectedCampaignEntry = useMemo(
+    () => keyedCampaigns.find((entry) => entry.key === selectedCampaignKeyResolved) ?? null,
+    [keyedCampaigns, selectedCampaignKeyResolved]
+  );
+  const selectedCampaign = selectedCampaignEntry?.campaign ?? null;
+  const selectedCampaignEntryKey = selectedCampaignEntry?.key ?? "";
+
+  const selectedCurrentNode = selectedCampaignEntry
+    ? (campaignCurrentNodeMap[selectedCampaignEntry.key] ?? null)
+    : null;
+  const currentTierLabel = useMemo(
+    () => resolveCurrentTierLabel(selectedCampaign ? [selectedCampaign] : [], accountJourneys),
+    [selectedCampaign, accountJourneys]
+  );
+
+  const pointRows = useMemo(() => pointsQuery.data ?? [], [pointsQuery.data]);
+  const pointAccountTypeNameMap = useMemo(() => {
+    const map = new Map<string, string>();
+    for (const pat of patsQuery.data ?? []) {
+      const id = typeof pat.id === "string" ? pat.id : null;
+      if (!id) continue;
+      map.set(normalizeId(id), typeof pat.name === "string" ? pat.name : id);
+    }
+    return map;
+  }, [patsQuery.data]);
+
+  if (isLoading) {
+    return <Skeleton className={cn("h-32 w-full", className)} />;
+  }
+
+  if (campaignsQuery.error) {
+    return (
+      <Alert variant="destructive" className={className}>
+        <AlertDescription>{(campaignsQuery.error as Error).message}</AlertDescription>
+      </Alert>
+    );
+  }
+  if (accountQuery.error) {
+    return (
+      <Alert variant="destructive" className={className}>
+        <AlertDescription>{(accountQuery.error as Error).message}</AlertDescription>
+      </Alert>
+    );
+  }
+  if (pointsQuery.error) {
+    return (
+      <Alert variant="destructive" className={className}>
+        <AlertDescription>{(pointsQuery.error as Error).message}</AlertDescription>
+      </Alert>
+    );
+  }
+
+  return (
+    <Card className={className}>
+      <CardContent className="p-4 space-y-4">
+        <h3 className="text-base font-semibold">Campaign progress</h3>
+
+        {enrolledCampaigns.length === 0 && (
+          <p className="text-sm font-medium text-muted-foreground">No campaigns for this account.</p>
+        )}
+
+        {selectedCampaign && selectedCampaignEntry && (
+          <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
+            <div className="min-w-0">
+              {currentTierLabel ? (
+                <div className="flex items-center gap-3">
+                  <Award className="h-6 w-6 text-primary" />
+                  <div>
+                    <p className="text-xs uppercase tracking-wide text-muted-foreground">Current tier</p>
+                    <p className="truncate text-xl font-bold sm:text-2xl">{currentTierLabel}</p>
+                  </div>
+                </div>
+              ) : (
+                <p className="text-sm text-muted-foreground">Not currently in a journey tier.</p>
+              )}
+            </div>
+
+            <Popover open={campaignPickerOpen} onOpenChange={setCampaignPickerOpen}>
+              <PopoverTrigger asChild>
+                <Button variant="outline" className="justify-between gap-2 sm:min-w-[220px]">
+                  <span className="truncate">{selectedCampaign.name ?? selectedCampaign.id ?? "Select campaign"}</span>
+                  <ChevronDown className="h-4 w-4 opacity-70" />
+                </Button>
+              </PopoverTrigger>
+              <PopoverContent align="end" className="w-[320px] p-0">
+                <Command>
+                  <CommandInput placeholder="Search campaigns..." />
+                  <CommandList>
+                    <CommandEmpty>No campaign found.</CommandEmpty>
+                    {keyedCampaigns.map((entry) => (
+                      <CommandItem
+                        key={entry.key}
+                        onSelect={() => {
+                          setSelectedCampaignKey(entry.key);
+                          setCampaignPickerOpen(false);
+                        }}
+                      >
+                        <span className="truncate">{entry.campaign.name ?? entry.campaign.id ?? "Untitled"}</span>
+                      </CommandItem>
+                    ))}
+                  </CommandList>
+                </Command>
+              </PopoverContent>
+            </Popover>
+          </div>
+        )}
+
+        {selectedCampaign && selectedCurrentNode && isProgressVisibleForNode(selectedCurrentNode) && (
+          <>
+            <div className="flex items-center gap-3">
+              <div className="h-px flex-1 bg-border" />
+              <Button type="button" size="sm" variant="secondary" onClick={() => setIsExpanded((v) => !v)}>
+                {isExpanded ? "Collapse" : "Expand"}
+                {isExpanded ? <ChevronUp className="ml-1 h-4 w-4" /> : <ChevronDown className="ml-1 h-4 w-4" />}
+              </Button>
+            </div>
+
+            {isExpanded && (
+              <JourneyProgressNode
+                node={selectedCurrentNode}
+                level={0}
+                points={pointRows}
+                campaignId={selectedCampaignEntryKey}
+                flatNodesByCampaign={flatNodesByCampaign}
+                patNames={pointAccountTypeNameMap}
+              />
+            )}
+          </>
+        )}
+      </CardContent>
+    </Card>
+  );
+}
diff --git a/Journeys/Journeys.UX/src/components/loyalty/accounts/eventable-models-section.tsx b/Journeys/Journeys.UX/src/components/loyalty/accounts/eventable-models-section.tsx
new file mode 100644
index 0000000..2f3e3ac
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/loyalty/accounts/eventable-models-section.tsx
@@ -0,0 +1,150 @@
+"use client";
+
+/**
+ * EventableModelsSection ΓÇö for an account, list every Live eventable schema and
+ * show one collapsible table per schema with that account's events.
+ *
+ * Rows are read-only: the events are shown inline here, not on another screen.
+ */
+
+import { useMemo, useState } from "react";
+import { useQuery } from "@tanstack/react-query";
+import { ChevronDown } from "lucide-react";
+
+import { Alert, AlertDescription } from "@/components/ui/alert";
+import { Badge } from "@/components/ui/badge";
+import { Button } from "@/components/ui/button";
+import { Card, CardContent } from "@/components/ui/card";
+import { Collapsible, CollapsibleContent, CollapsibleTrigger } from "@/components/ui/collapsible";
+import { Skeleton } from "@/components/ui/skeleton";
+import { SimpleDataTable } from "@/components/loyalty/dynamic-data";
+
+import type { LoyaltySchema } from "@/lib/loyalty-schema-types";
+import { getAllSchemas, queryData } from "@/services/loyalty/actions";
+import { isEventableSchema } from "@/services/loyalty/eventable-schema";
+import { loyaltyKeys } from "@/services/loyalty/query-keys";
+
+const EVENT_PAGE_SIZE = 25;
+
+/** A schema we can actually query: the model name is the query partition. */
+type NamedSchema = LoyaltySchema & { name: string };
+
+export interface EventableModelsSectionProps {
+  loyaltyAccountId: string;
+  className?: string;
+}
+
+export function EventableModelsSection({ loyaltyAccountId, className }: EventableModelsSectionProps) {
+  const schemasQuery = useQuery({
+    queryKey: loyaltyKeys.schemas.all("loyalty"),
+    queryFn: async () => {
+      const r = await getAllSchemas();
+      if (!r.success) throw new Error(r.error ?? "Failed to load schemas");
+      return r.data ?? [];
+    },
+  });
+
+  // `getAllSchemas` already filters to loyalty models; widen each row to the
+  // schema shape the schema-driven table reads.
+  const eventableSchemas = useMemo<NamedSchema[]>(
+    () =>
+      (schemasQuery.data ?? [])
+        .map((s): LoyaltySchema => ({ ...s }))
+        .filter((s): s is NamedSchema => typeof s.name === "string" && s.name.length > 0)
+        .filter((s) => s.status === "Live" && isEventableSchema(s)),
+    [schemasQuery.data]
+  );
+
+  if (schemasQuery.isLoading) {
+    return <Skeleton className={`h-32 w-full ${className ?? ""}`} />;
+  }
+
+  if (schemasQuery.error) {
+    return (
+      <Alert variant="destructive" className={className}>
+        <AlertDescription>{(schemasQuery.error as Error).message}</AlertDescription>
+      </Alert>
+    );
+  }
+
+  if (eventableSchemas.length === 0) {
+    return null;
+  }
+
+  return (
+    <Card className={className}>
+      <CardContent className="p-4 space-y-3">
+        <div>
+          <h3 className="text-base font-semibold">Eventable models</h3>
+          <p className="text-sm text-muted-foreground">All event types attached to this loyalty account.</p>
+        </div>
+
+        <div className="space-y-2">
+          {eventableSchemas.map((schema) => (
+            <EventableSchemaPanel key={schema.id ?? schema.name} schema={schema} loyaltyAccountId={loyaltyAccountId} />
+          ))}
+        </div>
+      </CardContent>
+    </Card>
+  );
+}
+
+interface EventableSchemaPanelProps {
+  schema: NamedSchema;
+  loyaltyAccountId: string;
+}
+
+function EventableSchemaPanel({ schema, loyaltyAccountId }: EventableSchemaPanelProps) {
+  const [isOpen, setIsOpen] = useState(false);
+
+  const dataQuery = useQuery({
+    queryKey: loyaltyKeys.eventable.bySchemaAccount(schema.name, loyaltyAccountId),
+    queryFn: async () => {
+      const r = await queryData<Record<string, unknown>>({
+        schemaName: schema.name,
+        loyaltyAccountId,
+        pageSize: EVENT_PAGE_SIZE,
+      });
+      if (!r.success) throw new Error(r.error ?? "Failed to load events");
+      return r.data ?? [];
+    },
+    enabled: isOpen,
+  });
+
+  const rows = (dataQuery.data ?? []).map((item) => {
+    const event = (item as { event?: Record<string, unknown> }).event;
+    return event ?? item;
+  });
+
+  return (
+    <Collapsible open={isOpen} onOpenChange={setIsOpen}>
+      <CollapsibleTrigger asChild>
+        <Button variant="ghost" className="w-full justify-between border rounded-lg px-4 py-3 h-auto">
+          <div className="flex items-center gap-3">
+            <span className="text-sm font-medium">{schema.name}</span>
+            {dataQuery.data && (
+              <Badge variant="secondary">
+                {dataQuery.data.length}
+                {dataQuery.data.length === EVENT_PAGE_SIZE ? "+" : ""}
+              </Badge>
+            )}
+          </div>
+          <ChevronDown className="h-4 w-4 transition-transform data-[state=open]:rotate-180" aria-hidden="true" />
+        </Button>
+      </CollapsibleTrigger>
+      <CollapsibleContent className="pt-2">
+        {dataQuery.isLoading ? (
+          <Skeleton className="h-24 w-full" />
+        ) : dataQuery.error ? (
+          <Alert variant="destructive">
+            <AlertDescription>{(dataQuery.error as Error).message}</AlertDescription>
+          </Alert>
+        ) : rows.length === 0 ? (
+          <p className="text-sm text-muted-foreground p-4 text-center">No events for this account.</p>
+        ) : (
+          <SimpleDataTable schema={schema} data={rows} />
+        )}
+      </CollapsibleContent>
+    </Collapsible>
+  );
+}
diff --git a/Journeys/Journeys.UX/src/components/loyalty/accounts/eventable-models-section.source.test.ts b/Journeys/Journeys.UX/src/components/loyalty/accounts/eventable-models-section.source.test.ts
new file mode 100644
index 0000000..33e718f
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/loyalty/accounts/eventable-models-section.source.test.ts
@@ -0,0 +1,29 @@
+import { readFileSync } from "node:fs";
+import { dirname, resolve } from "node:path";
+import { fileURLToPath } from "node:url";
+
+import { describe, expect, it } from "vitest";
+
+const source = readFileSync(
+  resolve(dirname(fileURLToPath(import.meta.url)), "eventable-models-section.tsx"),
+  "utf8"
+);
+
+describe("eventable models section", () => {
+  it("does not link to data explorer routes", () => {
+    expect(source).not.toContain("data-explorer");
+  });
+
+  it("does not link to an account model builder", () => {
+    expect(source).not.toContain("builder");
+  });
+
+  it("queries events through the account-scoped queryData params", () => {
+    expect(source).toContain("loyaltyAccountId,");
+    expect(source).not.toContain("queryAdminData");
+  });
+
+  it("never sends an unknown model id", () => {
+    expect(source).not.toContain('"unknown"');
+  });
+});
diff --git a/Journeys/Journeys.UX/src/components/loyalty/dynamic-data/dynamic-entity-details.tsx b/Journeys/Journeys.UX/src/components/loyalty/dynamic-data/dynamic-entity-details.tsx
new file mode 100644
index 0000000..8889dcc
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/loyalty/dynamic-data/dynamic-entity-details.tsx
@@ -0,0 +1,324 @@
+"use client";
+
+/**
+ * DynamicEntityDetails ΓÇö schema-driven detail layout for a single entity.
+ *
+ * Reads `LoyaltySchema.attributes`, groups them by `attributeLayoutGroup`, and
+ * renders each group in the prescribed slot of the layout grid (header /
+ * top-left-split / top-right-split / middle / outcomes).
+ *
+ * Each `AttributeType` maps to the matching shared view ΓÇö `JsonTreeView` for
+ * raw/dynamic/keyvalue, `FriendlyDataView` for object/list, direct primitive
+ * rendering for primitives. No raw `JSON.stringify` fallbacks.
+ *
+ * Domain-specific panels (point balances, campaign progress, eventable models)
+ * are composed AROUND this component at the page level ΓÇö this stays generic so
+ * it works for any schema.
+ */
+
+import { useMemo, type ReactNode } from "react";
+import { ChevronDown } from "lucide-react";
+
+import { Button } from "@/components/ui/button";
+import { Card } from "@/components/ui/card";
+import { Collapsible, CollapsibleContent, CollapsibleTrigger } from "@/components/ui/collapsible";
+import { FriendlyDataView } from "@/components/shared/friendly-data-view";
+import { JsonTreeView } from "@/components/shared/json-tree-view";
+import {
+  CURATED_ATTRIBUTE_LAYOUT_GROUPS,
+  type LoyaltySchema,
+  type SchemaAttribute,
+} from "@/lib/loyalty-schema-types";
+import { cn } from "@/lib/utils";
+import { formatValueAsString, getNestedValue, getSchemaFieldValue } from "@/services/loyalty/utils/grid-columns";
+import { getSchemaDisplayName } from "@/services/loyalty/utils/display-labels";
+
+import { SimpleDataTable } from "./simple-data-table";
+
+// A renderable attribute always has a symbol ΓÇö it is the key we read from the entity.
+type Attribute = SchemaAttribute & {
+  symbol: string;
+  restrictions?: Array<{ type: string }> | null;
+};
+
+const LAYOUT_GROUPS = CURATED_ATTRIBUTE_LAYOUT_GROUPS as readonly string[];
+
+type LayoutGroupId = (typeof CURATED_ATTRIBUTE_LAYOUT_GROUPS)[number];
+
+export interface DynamicEntityDetailsProps {
+  schema: LoyaltySchema;
+  entity: Record<string, unknown>;
+  /** Show the built-in model title/description header block. */
+  showTitle?: boolean;
+  /** Map of `modelId ΓåÆ schema` for nested ModelObject / ModelList rendering. */
+  relatedSchemas?: Map<string, LoyaltySchema>;
+  /** Map of attribute symbol ΓåÆ array of related rows for ModelList. */
+  relatedData?: Map<string, unknown[]>;
+  /** Optional slot rendered beside the header group. */
+  headerSlot?: ReactNode;
+  /** Optional slot rendered after the layout groups. */
+  footerSlot?: ReactNode;
+}
+
+function PrimitiveValue({ value, dataType }: { value: unknown; dataType: string }) {
+  if (value === null || value === undefined) {
+    return <span className="text-muted-foreground italic">ΓÇö</span>;
+  }
+  if ((dataType ?? "").toLowerCase() === "boolean" || (dataType ?? "").toLowerCase() === "bool") {
+    return (
+      <span className={value ? "text-green-700 dark:text-green-400" : "text-red-600 dark:text-red-400"}>
+        {value ? "Yes" : "No"}
+      </span>
+    );
+  }
+  return <span>{formatValueAsString(value, dataType)}</span>;
+}
+
+function AttributeField({ attribute, value }: { attribute: Attribute; value: unknown }) {
+  const isRequired = attribute.restrictions?.some((r) => r.type === "Required") ?? false;
+  const isMultiLine = attribute.attributeLayoutHeight === "multi";
+
+  return (
+    <div className={cn("px-6 py-4", isMultiLine ? "space-y-2" : "grid grid-cols-1 md:grid-cols-3 gap-4")}>
+      <div className={isMultiLine ? "" : "md:col-span-1"}>
+        <label className="block text-sm font-medium text-foreground">
+          {attribute.displayName ?? attribute.symbol}
+          {isRequired && <span className="text-destructive ml-1">*</span>}
+          <span className="block text-xs text-muted-foreground mt-1">{attribute.dataType ?? ""}</span>
+        </label>
+      </div>
+      <div className={cn(isMultiLine ? "" : "md:col-span-2", "text-sm")}>
+        <PrimitiveValue value={value} dataType={attribute.dataType ?? "string"} />
+      </div>
+    </div>
+  );
+}
+
+function NestedModelGrid({
+  attribute,
+  relatedSchema,
+  rows,
+}: {
+  attribute: Attribute;
+  relatedSchema: LoyaltySchema;
+  rows: unknown[];
+}) {
+  return (
+    <div className="px-6 py-4 space-y-2">
+      <label className="block text-sm font-medium text-foreground">{attribute.displayName ?? attribute.symbol}</label>
+      <SimpleDataTable schema={relatedSchema} data={rows} />
+    </div>
+  );
+}
+
+function NestedModelObject({
+  attribute,
+  value,
+  relatedSchema,
+}: {
+  attribute: Attribute;
+  value: unknown;
+  relatedSchema: LoyaltySchema;
+}) {
+  return (
+    <Collapsible defaultOpen={false} className="border-t">
+      <CollapsibleTrigger asChild>
+        <Button variant="ghost" className="w-full justify-between px-6 py-4 rounded-none h-auto">
+          <span className="text-sm font-semibold">{attribute.displayName ?? attribute.symbol}</span>
+          <ChevronDown className="h-4 w-4 transition-transform data-[state=open]:rotate-180" aria-hidden="true" />
+        </Button>
+      </CollapsibleTrigger>
+      <CollapsibleContent>
+        <div className="divide-y">
+          {((relatedSchema.attributes ?? []) as Attribute[])
+            .filter((a) => a.status === "Live" && a.isDetailsViewable !== false)
+            .map((a) => (
+              <AttributeField key={a.symbol} attribute={a} value={getNestedValue(value, a.symbol)} />
+            ))}
+        </div>
+      </CollapsibleContent>
+    </Collapsible>
+  );
+}
+
+function LabelledValue({ attribute, children }: { attribute: Attribute; children: ReactNode }) {
+  return (
+    <div className="px-6 py-4 space-y-2">
+      <label className="block text-sm font-medium text-foreground">{attribute.displayName ?? attribute.symbol}</label>
+      {children}
+    </div>
+  );
+}
+
+function renderAttribute(
+  attr: Attribute,
+  entity: Record<string, unknown>,
+  relatedSchemas: Map<string, LoyaltySchema>,
+  relatedData: Map<string, unknown[]>
+): ReactNode {
+  const value = getSchemaFieldValue(entity, attr.symbol);
+
+  switch (attr.type) {
+    case "Primitive":
+      return <AttributeField attribute={attr} value={value} />;
+
+    case "ModelList": {
+      const related = attr.modelId ? relatedSchemas.get(attr.modelId) : undefined;
+      const inlineRows = Array.isArray(value) ? value : [];
+      const resolvedRelatedRows = relatedData.get(attr.symbol);
+      const rows =
+        Array.isArray(resolvedRelatedRows) && resolvedRelatedRows.length > 0 ? resolvedRelatedRows : inlineRows;
+      if (related) {
+        return <NestedModelGrid attribute={attr} relatedSchema={related} rows={rows} />;
+      }
+      return (
+        <LabelledValue attribute={attr}>
+          <FriendlyDataView value={value} />
+        </LabelledValue>
+      );
+    }
+
+    case "ModelObject": {
+      const related = attr.modelId ? relatedSchemas.get(attr.modelId) : undefined;
+      if (related && value && typeof value === "object") {
+        return <NestedModelObject attribute={attr} value={value} relatedSchema={related} />;
+      }
+      return (
+        <LabelledValue attribute={attr}>
+          <FriendlyDataView value={value} />
+        </LabelledValue>
+      );
+    }
+
+    case "ModelKeyValue":
+    case "ModelDynamic":
+    case "ModelRawJson":
+      return (
+        <LabelledValue attribute={attr}>
+          <JsonTreeView value={value} />
+        </LabelledValue>
+      );
+
+    default:
+      return <AttributeField attribute={attr} value={value} />;
+  }
+}
+
+function LayoutGroup({ children }: { children: ReactNode }) {
+  return (
+    <Card className="overflow-hidden">
+      <div className="divide-y">{children}</div>
+    </Card>
+  );
+}
+
+function resolveAttributeLayoutGroup(attribute: Attribute): LayoutGroupId {
+  const groupId = attribute.attributeLayoutGroup as LayoutGroupId | undefined;
+  if (groupId && LAYOUT_GROUPS.includes(groupId)) return groupId;
+  return "middle-panel";
+}
+
+export function DynamicEntityDetails({
+  schema,
+  entity,
+  showTitle = true,
+  relatedSchemas = new Map(),
+  relatedData = new Map(),
+  headerSlot,
+  footerSlot,
+}: DynamicEntityDetailsProps) {
+  const grouped = useMemo(() => {
+    const map = new Map<LayoutGroupId, Attribute[]>();
+    for (const a of (schema.attributes ?? []) as Attribute[]) {
+      if (!a.symbol || a.isDetailsViewable === false || a.status !== "Live") continue;
+      const groupId = resolveAttributeLayoutGroup(a);
+      const arr = map.get(groupId) ?? [];
+      arr.push(a);
+      map.set(groupId, arr);
+    }
+    for (const [, arr] of map) {
+      arr.sort((a, b) => (a.attributeLayoutPosition ?? 999) - (b.attributeLayoutPosition ?? 999));
+    }
+    return map;
+  }, [schema]);
+
+  const renderGroup = (groupId: LayoutGroupId): ReactNode => {
+    const attrs = grouped.get(groupId);
+    if (!attrs?.length) return null;
+    return (
+      <LayoutGroup>
+        {attrs.map((attr) => (
+          <div key={attr.symbol}>{renderAttribute(attr, entity, relatedSchemas, relatedData)}</div>
+        ))}
+      </LayoutGroup>
+    );
+  };
+
+  const headerGroup = renderGroup("header-panel");
+  const tlGroup = renderGroup("top-left-split-panel");
+  const trGroup = renderGroup("top-right-split-panel");
+  const middleGroup = renderGroup("middle-panel");
+  // Attributes with a missing or non-curated layout group fall back to
+  // `middle-panel` so payload fields stay visible when layout metadata is unset.
+  const outcomesGroup = renderGroup("outcomes");
+  const modelName = getSchemaDisplayName(schema);
+
+  return (
+    <div className="space-y-6">
+      {showTitle && (
+        <div className="border-b pb-4">
+          <h2 className="text-2xl font-bold">{modelName} details</h2>
+          <p className="text-sm text-muted-foreground mt-1">
+            View detailed information about this {modelName.toLowerCase()}.
+          </p>
+        </div>
+      )}
+
+      {(headerGroup || headerSlot) && (
+        <div className="flex flex-col lg:flex-row gap-6">
+          <div className="flex-1 min-w-0">
+            {headerGroup ?? (
+              <p className="text-sm text-muted-foreground italic">No attributes assigned to header panel.</p>
+            )}
+          </div>
+          {headerSlot && <div className="w-full lg:w-[400px] shrink-0">{headerSlot}</div>}
+        </div>
+      )}
+
+      {(tlGroup || trGroup) && (
+        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
+          {tlGroup}
+          {trGroup}
+        </div>
+      )}
+
+      {middleGroup}
+
+      {outcomesGroup}
+
+      {footerSlot}
+
+      <div className="rounded-lg border bg-muted/20 p-4">
+        <h3 className="text-sm font-semibold mb-2">Model information</h3>
+        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
+          <div>
+            <span className="text-muted-foreground">Model:</span>
+            <p className="font-medium">{modelName}</p>
+          </div>
+          <div>
+            <span className="text-muted-foreground">Model type:</span>
+            <p className="font-medium">{schema.modelType ?? "ΓÇö"}</p>
+          </div>
+          <div>
+            <span className="text-muted-foreground">Status:</span>
+            <p className="font-medium">{schema.status ?? "ΓÇö"}</p>
+          </div>
+          <div>
+            <span className="text-muted-foreground">Version:</span>
+            <p className="font-medium">{schema.modelVersion ?? "ΓÇö"}</p>
+          </div>
+        </div>
+      </div>
+    </div>
+  );
+}
diff --git a/Journeys/Journeys.UX/src/components/shared/friendly-data-view.tsx b/Journeys/Journeys.UX/src/components/shared/friendly-data-view.tsx
new file mode 100644
index 0000000..188b54c
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/shared/friendly-data-view.tsx
@@ -0,0 +1,128 @@
+"use client";
+
+import { useState } from "react";
+import { Badge } from "@/components/ui/badge";
+import { Button } from "@/components/ui/button";
+import { cn } from "@/lib/utils";
+
+const LONG_STRING_THRESHOLD = 480;
+
+/** Turn camelCase / snake_case keys into short titles for non-technical readers. */
+function formatFieldLabel(key: string): string {
+  const spaced = key
+    .replace(/_/g, " ")
+    .replace(/([a-z])([A-Z])/g, "$1 $2")
+    .trim();
+  if (!spaced) return key;
+  return spaced.replace(/\b\w/g, (c) => c.toUpperCase());
+}
+
+function tryParseJsonObjectString(s: string): unknown | null {
+  const t = s.trim();
+  if (!((t.startsWith("{") && t.endsWith("}")) || (t.startsWith("[") && t.endsWith("]")))) {
+    return null;
+  }
+  try {
+    return JSON.parse(t) as unknown;
+  } catch {
+    return null;
+  }
+}
+
+function LongString({ text }: { text: string }) {
+  const [open, setOpen] = useState(false);
+  const needsTruncate = text.length > LONG_STRING_THRESHOLD;
+  const shown = needsTruncate && !open ? `${text.slice(0, LONG_STRING_THRESHOLD)}ΓÇª` : text;
+
+  return (
+    <div className="space-y-1">
+      <p className="text-sm whitespace-pre-wrap break-words text-foreground">{shown}</p>
+      {needsTruncate ? (
+        <Button type="button" variant="ghost" size="sm" className="h-7 px-2 text-xs" onClick={() => setOpen((o) => !o)}>
+          {open ? "Show less" : "Show full text"}
+        </Button>
+      ) : null}
+    </div>
+  );
+}
+
+export interface FriendlyDataViewProps {
+  value: unknown;
+  /** Nesting depth (internal). */
+  depth?: number;
+  className?: string;
+}
+
+/**
+ * Renders structured data as labeled sections ΓÇö no JSON brackets, quotes, or colons as syntax.
+ * For objects: field labels + values; for arrays: numbered items; primitives read as plain content.
+ */
+export function FriendlyDataView({ value, depth = 0, className }: FriendlyDataViewProps) {
+  if (value === null || value === undefined) {
+    return <span className="text-sm text-muted-foreground">ΓÇö</span>;
+  }
+
+  if (typeof value === "boolean") {
+    return value ? (
+      <Badge variant="secondary" className="font-normal">
+        Yes
+      </Badge>
+    ) : (
+      <Badge variant="outline" className="font-normal text-muted-foreground">
+        No
+      </Badge>
+    );
+  }
+
+  if (typeof value === "number") {
+    return <span className="text-sm tabular-nums text-foreground">{Number.isFinite(value) ? String(value) : "ΓÇö"}</span>;
+  }
+
+  if (typeof value === "string") {
+    const parsed = tryParseJsonObjectString(value);
+    if (parsed !== null && typeof parsed === "object") {
+      return <FriendlyDataView value={parsed} depth={depth} className={className} />;
+    }
+    if (value.length > LONG_STRING_THRESHOLD) {
+      return <LongString text={value} />;
+    }
+    return <p className="text-sm whitespace-pre-wrap break-words text-foreground">{value}</p>;
+  }
+
+  if (Array.isArray(value)) {
+    if (value.length === 0) {
+      return <span className="text-sm text-muted-foreground">No items</span>;
+    }
+    return (
+      <ul className={cn("list-none space-y-3", className)}>
+        {value.map((item, i) => (
+          <li key={i} className="rounded-md border border-border/80 bg-card/40 px-3 py-2.5 shadow-sm">
+            <p className="mb-2 text-[11px] font-medium uppercase tracking-wide text-muted-foreground">Item {i + 1}</p>
+            <FriendlyDataView value={item} depth={depth + 1} />
+          </li>
+        ))}
+      </ul>
+    );
+  }
+
+  if (typeof value === "object") {
+    const entries = Object.entries(value as Record<string, unknown>);
+    if (entries.length === 0) {
+      return <span className="text-sm text-muted-foreground">No details</span>;
+    }
+    return (
+      <dl className={cn("space-y-3", depth > 0 && "border-l-2 border-primary/15 pl-3", className)}>
+        {entries.map(([k, v]) => (
+          <div key={k}>
+            <dt className="text-xs font-medium text-muted-foreground">{formatFieldLabel(k)}</dt>
+            <dd className="mt-1.5">
+              <FriendlyDataView value={v} depth={depth + 1} />
+            </dd>
+          </div>
+        ))}
+      </dl>
+    );
+  }
+
+  return <span className="text-sm text-foreground">{String(value)}</span>;
+}
diff --git a/Journeys/Journeys.UX/src/components/ui/collapsible.tsx b/Journeys/Journeys.UX/src/components/ui/collapsible.tsx
new file mode 100644
index 0000000..f296ec4
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/ui/collapsible.tsx
@@ -0,0 +1,18 @@
+"use client";
+
+import * as React from "react";
+import { Collapsible as CollapsiblePrimitive } from "radix-ui";
+
+function Collapsible({ ...props }: React.ComponentProps<typeof CollapsiblePrimitive.Root>) {
+  return <CollapsiblePrimitive.Root data-slot="collapsible" {...props} />;
+}
+
+function CollapsibleTrigger({ ...props }: React.ComponentProps<typeof CollapsiblePrimitive.CollapsibleTrigger>) {
+  return <CollapsiblePrimitive.CollapsibleTrigger data-slot="collapsible-trigger" {...props} />;
+}
+
+function CollapsibleContent({ ...props }: React.ComponentProps<typeof CollapsiblePrimitive.CollapsibleContent>) {
+  return <CollapsiblePrimitive.CollapsibleContent data-slot="collapsible-content" {...props} />;
+}
+
+export { Collapsible, CollapsibleTrigger, CollapsibleContent };
diff --git a/Journeys/Journeys.UX/src/services/loyalty/utils/display-labels.ts b/Journeys/Journeys.UX/src/services/loyalty/utils/display-labels.ts
new file mode 100644
index 0000000..dcee9be
--- /dev/null
+++ b/Journeys/Journeys.UX/src/services/loyalty/utils/display-labels.ts
@@ -0,0 +1,97 @@
+/**
+ * Human-readable labels for schema-driven surfaces.
+ *
+ * Schema and attribute names arrive as raw symbols (`orderId`,
+ * `customer_profile`), and entity rows key their human identifier under one of
+ * several historical field spellings. These helpers turn both into text an
+ * operator can read.
+ */
+
+import type { LoyaltySchema } from "@/lib/loyalty-schema-types";
+
+import { generateColumnsFromSchema, getSchemaFieldValue } from "./grid-columns";
+
+type LabelSchema = Pick<LoyaltySchema, "name" | "displayName" | "attributes">;
+
+const KNOWN_INITIALISMS = new Map([
+  ["id", "ID"],
+  ["url", "URL"],
+  ["api", "API"],
+  ["ai", "AI"],
+  ["sdk", "SDK"],
+]);
+
+const ENTITY_LABEL_FIELDS = [
+  "orderId",
+  "orderid",
+  "eventType",
+  "eventName",
+  "externalId",
+  "extAccountId",
+  "sourceRecordId",
+  "loyaltyAccountId",
+  "profileId",
+  "profileid",
+  "customerId",
+  "customerid",
+  "name",
+  "displayName",
+  "title",
+] as const;
+
+function isRecord(value: unknown): value is Record<string, unknown> {
+  return Boolean(value) && typeof value === "object" && !Array.isArray(value);
+}
+
+function formatWord(word: string): string {
+  const known = KNOWN_INITIALISMS.get(word.toLowerCase());
+  if (known) return known;
+  if (word === word.toUpperCase() && word.length > 1) return word;
+  return word.charAt(0).toUpperCase() + word.slice(1);
+}
+
+export function formatDisplayName(name: string | null | undefined): string {
+  const trimmed = name?.trim();
+  if (!trimmed) return "";
+
+  return trimmed
+    .replace(/[_-]+/g, " ")
+    .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
+    .split(/\s+/)
+    .filter(Boolean)
+    .map(formatWord)
+    .join(" ");
+}
+
+export function getSchemaDisplayName(schema: LabelSchema | null | undefined, fallback = "Model"): string {
+  return formatDisplayName(schema?.displayName ?? schema?.name) || fallback;
+}
+
+function readEntityLabel(entity: Record<string, unknown>, field: string): string | null {
+  const directValue = getSchemaFieldValue(entity, field);
+  const event = isRecord(entity["event"]) ? entity["event"] : null;
+  const eventValue = event ? getSchemaFieldValue(event, field) : undefined;
+  const value = directValue ?? eventValue;
+
+  if (typeof value === "string" && value.trim().length > 0) return value.trim();
+  if (typeof value === "number" && Number.isFinite(value)) return value.toString();
+  return null;
+}
+
+export function getEntityDisplayName(
+  schema: LabelSchema | null | undefined,
+  entity: Record<string, unknown> | null | undefined,
+  fallback: string
+): string {
+  if (!entity) return fallback;
+
+  const schemaFields = schema ? generateColumnsFromSchema(schema as LoyaltySchema).map((column) => column.key) : [];
+  const fields = Array.from(new Set([...ENTITY_LABEL_FIELDS, ...schemaFields, "id"]));
+
+  for (const field of fields) {
+    const value = readEntityLabel(entity, field);
+    if (value) return value;
+  }
+
+  return fallback;
+}
diff --git a/Journeys/Journeys.UX/src/services/loyalty/utils/display-labels.test.ts b/Journeys/Journeys.UX/src/services/loyalty/utils/display-labels.test.ts
new file mode 100644
index 0000000..9393027
--- /dev/null
+++ b/Journeys/Journeys.UX/src/services/loyalty/utils/display-labels.test.ts
@@ -0,0 +1,47 @@
+import { describe, expect, it } from "vitest";
+import type { LoyaltySchema } from "@/lib/loyalty-schema-types";
+
+import { formatDisplayName, getEntityDisplayName, getSchemaDisplayName } from "./display-labels";
+
+describe("formatDisplayName", () => {
+  it("formats schema names without losing known initialisms", () => {
+    expect(formatDisplayName("order")).toBe("Order");
+    expect(formatDisplayName("orderId")).toBe("Order ID");
+    expect(formatDisplayName("customer_profile")).toBe("Customer Profile");
+  });
+});
+
+describe("getSchemaDisplayName", () => {
+  it("prefers configured display name and formats raw names", () => {
+    expect(getSchemaDisplayName({ name: "order", displayName: undefined, attributes: [] })).toBe("Order");
+    expect(getSchemaDisplayName({ name: "order", displayName: "Sales Order", attributes: [] })).toBe("Sales Order");
+  });
+});
+
+describe("getEntityDisplayName", () => {
+  it("uses human identifiers before raw UUID ids", () => {
+    const schema = {
+      name: "order",
+      attributes: [
+        {
+          symbol: "orderId",
+          displayName: "Order ID",
+          type: "Primitive",
+          dataType: "String",
+          status: "Live",
+        },
+      ],
+    } as LoyaltySchema;
+
+    expect(
+      getEntityDisplayName(
+        schema,
+        {
+          id: "6f7610a4-78f1-46cb-8a6d-6d3a6ed26469",
+          event: { orderid: "wxyz_101" },
+        },
+        "6f7610a"
+      )
+    ).toBe("wxyz_101");
+  });
+});
diff --git a/Journeys/Journeys.UX/src/components/loyalty/points/points-accounts-card.tsx b/Journeys/Journeys.UX/src/components/loyalty/points/points-accounts-card.tsx
new file mode 100644
index 0000000..c1f8bd8
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/loyalty/points/points-accounts-card.tsx
@@ -0,0 +1,90 @@
+"use client";
+
+/**
+ * PointsAccountsCard ΓÇö read-only list of point accounts for a loyalty account.
+ *
+ * Deposit / spend / expire arrive with the Manage flow in a later change; the
+ * card stays read-only until then rather than shipping a dead control.
+ */
+
+import { Wallet } from "lucide-react";
+
+import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
+import { Skeleton } from "@/components/ui/skeleton";
+import type { AccountPointBalance, PointAccountTypeListItem } from "@/lib/api-types";
+
+export interface PointsAccountsCardProps {
+  pointAccounts: AccountPointBalance[];
+  pointAccountTypes?: PointAccountTypeListItem[];
+  isLoading?: boolean;
+}
+
+function getPointAccountKey(account: AccountPointBalance, index: number): string {
+  return `${account.accountId}:${account.pointAccountTypeId}:${index}`;
+}
+
+function readText(value: unknown): string | null {
+  return typeof value === "string" && value.trim().length > 0 ? value : null;
+}
+
+export function PointsAccountsCard({
+  pointAccounts,
+  pointAccountTypes = [],
+  isLoading = false,
+}: PointsAccountsCardProps) {
+  const renderAccountName = (account: AccountPointBalance): string => {
+    const match = pointAccountTypes.find((p) => readText(p.id) === account.pointAccountTypeId);
+    return (match ? readText(match.name) : null) ?? account.pointAccountTypeId ?? "Point account";
+  };
+
+  return (
+    <Card>
+      <CardHeader>
+        <CardTitle className="flex items-center gap-2 text-base">
+          <Wallet className="h-5 w-5 text-muted-foreground" aria-hidden="true" />
+          Point accounts
+        </CardTitle>
+        <CardDescription>Balances by point account type</CardDescription>
+      </CardHeader>
+      <CardContent>
+        {isLoading ? (
+          <div className="space-y-2">
+            {Array.from({ length: 3 }).map((_, i) => (
+              <Skeleton key={i} className="h-16 w-full" />
+            ))}
+          </div>
+        ) : pointAccounts.length === 0 ? (
+          <p className="py-6 text-center text-sm text-muted-foreground">No point accounts found.</p>
+        ) : (
+          <ul className="space-y-3">
+            {pointAccounts.map((account, index) => (
+              <li
+                key={getPointAccountKey(account, index)}
+                className="flex items-center justify-between gap-3 rounded-lg border p-3"
+              >
+                <div className="min-w-0">
+                  <p className="text-sm font-medium truncate">{renderAccountName(account)}</p>
+                  <div className="mt-1 flex flex-wrap items-center gap-3 text-xs text-muted-foreground">
+                    <span>
+                      Balance{" "}
+                      <span className="text-foreground font-semibold tabular-nums">
+                        {(account.currentBalance ?? 0).toLocaleString()}
+                      </span>
+                    </span>
+                    <span aria-hidden="true">ΓÇó</span>
+                    <span>
+                      Lifetime{" "}
+                      <span className="text-foreground font-semibold tabular-nums">
+                        {(account.lifetimeTotal ?? 0).toLocaleString()}
+                      </span>
+                    </span>
+                  </div>
+                </div>
+              </li>
+            ))}
+          </ul>
+        )}
+      </CardContent>
+    </Card>
+  );
+}
diff --git a/Journeys/Journeys.UX/src/components/loyalty/points/index.ts b/Journeys/Journeys.UX/src/components/loyalty/points/index.ts
new file mode 100644
index 0000000..d5d2c67
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/loyalty/points/index.ts
@@ -0,0 +1 @@
+export { PointsAccountsCard, type PointsAccountsCardProps } from "./points-accounts-card";
```
