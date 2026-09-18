diff --git a/Journeys/Journeys.UX/src/app/loyalty/accounts/page.tsx b/Journeys/Journeys.UX/src/app/loyalty/accounts/page.tsx
index 39bc828..032d485 100644
--- a/Journeys/Journeys.UX/src/app/loyalty/accounts/page.tsx
+++ b/Journeys/Journeys.UX/src/app/loyalty/accounts/page.tsx
@@ -1,51 +1,8 @@
-import { queryData } from "@/services/loyalty/actions";
-import { loyaltyScreenModel } from "@/lib/loyalty-model";
-import { attributeNamesFromRows } from "@/services/loyalty/parse-list";
-
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
-
+export default function AccountsPage() {
   return (
     <>
       <h1>Accounts</h1>
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
+      <p className="empty">Loading accountsΓÇª</p>
     </>
   );
 }
diff --git a/Journeys/Journeys.UX/src/lib/api-types.ts b/Journeys/Journeys.UX/src/lib/api-types.ts
index 25bd769..e2e699b 100644
--- a/Journeys/Journeys.UX/src/lib/api-types.ts
+++ b/Journeys/Journeys.UX/src/lib/api-types.ts
@@ -1,21 +1,52 @@
 export type ApiResponse<T> = {
   success: boolean;
   data?: T;
   error?: string;
   timestamp: string;
+  meta?: { continuationToken?: string | null; pageSize?: number };
 };
 
 export type CampaignListItem = {
   id?: string;
   name?: string;
   status?: string;
   extCampaignId?: string;
+  startDate?: string;
+  endDate?: string;
+  events?: string[];
+  journey?: Record<string, unknown>;
+  [key: string]: unknown;
 };
 
+export type AgentConversationListItem = {
+  conversationId: string;
+};
+
+export type PointAccountTypeListItem = Record<string, unknown>;
+
 export type SchemaListItem = {
   id?: string;
   name?: string;
   status?: string;
   modelType?: string;
+  tag?: string;
   attributes?: { name?: string; displayName?: string }[];
 };
+
+export type AccountPointBalance = {
+  accountId?: string;
+  pointAccountTypeId?: string;
+  currentBalance?: number;
+  lifetimeTotal?: number;
+};
+
+export type QueryDataParams = {
+  schemaName: string;
+  queryString?: string;
+  queryArgs?: Record<string, unknown>;
+  loyaltyAccountId?: string;
+  pageSize?: number;
+  continuationToken?: string | null;
+  sortBy?: string | null;
+  sortOrder?: "ASC" | "DESC";
+};
diff --git a/Journeys/Journeys.UX/src/lib/journeys-fetch.test.ts b/Journeys/Journeys.UX/src/lib/journeys-fetch.test.ts
index 7670e61..1ee57f5 100644
--- a/Journeys/Journeys.UX/src/lib/journeys-fetch.test.ts
+++ b/Journeys/Journeys.UX/src/lib/journeys-fetch.test.ts
@@ -15,21 +15,21 @@ describe("journeysFetch", () => {
       apiBaseUrl: "https://api.example"
     });
     expect(fetchMock).not.toHaveBeenCalled();
     expect(r.success).toBe(false);
     expect(r.error).toMatch(/Not authenticated/i);
   });
 
   it("does not call fetch when path is not allowlisted", async () => {
     vi.stubEnv("JOURNEYS_TENANT_ID", "");
     const fetchMock = vi.fn();
-    const r = await journeysFetch("campaigns/x/save", {}, {
+    const r = await journeysFetch("campaigns/session/pointaccounttype/upsert", {}, {
       getSession: async () => session(),
       fetch: fetchMock as unknown as typeof fetch,
       apiBaseUrl: "https://api.example"
     });
     expect(fetchMock).not.toHaveBeenCalled();
     expect(r.error).toMatch(/not-allowlisted/);
   });
 
   it("POSTs getall with API key header and wraps entities", async () => {
     vi.stubEnv("JOURNEYS_TENANT_ID", "");
@@ -66,11 +66,78 @@ describe("journeysFetch", () => {
     vi.stubEnv("JOURNEYS_TENANT_ID", "TestTenant1");
     const fetchMock = vi.fn(async () => new Response("[]", { status: 200 }));
     await journeysFetch("campaigns/x/getall", { method: "POST", body: {} }, {
       getSession: async () => session({ tenantId: "stale-session-tenant" }),
       fetch: fetchMock as unknown as typeof fetch,
       apiBaseUrl: "https://api.example"
     });
     const [url] = fetchMock.mock.calls[0] as unknown as [string];
     expect(url).toBe("https://api.example/api/Campaign/TestTenant1/getall");
   });
+
+  it("appends encoded non-empty search params without sending Cookie", async () => {
+    vi.stubEnv("JOURNEYS_TENANT_ID", "");
+    const fetchMock = vi.fn(async () => new Response("{}", { status: 200 }));
+    await journeysFetch(
+      "campaigns/session/cid-1",
+      {
+        searchParams: {
+          campaignStatus: "draft version",
+          empty: "",
+          omitted: undefined
+        }
+      },
+      {
+        getSession: async () => session(),
+        fetch: fetchMock as unknown as typeof fetch,
+        apiBaseUrl: "https://api.example"
+      }
+    );
+    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
+    expect(url).toBe(
+      "https://api.example/api/Campaign/acme/cid-1?campaignStatus=draft+version"
+    );
+    expect((init.headers as Record<string, string>).Cookie).toBeUndefined();
+  });
+
+  it("attaches X-Journeys-Audit on audit option and uses session user id", async () => {
+    vi.stubEnv("JOURNEYS_TENANT_ID", "");
+    const fetchMock = vi.fn(async () => new Response("{}", { status: 200 }));
+    await journeysFetch(
+      "accounts/session/points/deposit",
+      {
+        method: "POST",
+        body: { amount: 1 },
+        audit: {
+          loyaltyMemberId: "acc-1",
+          actionType: "Point Adjustment",
+          action: "Deposit",
+          comment: "ops"
+        }
+      },
+      {
+        getSession: async () => session(),
+        fetch: fetchMock as unknown as typeof fetch,
+        apiBaseUrl: "https://api.example"
+      }
+    );
+    const [, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
+    const header = (init.headers as Record<string, string>)["X-Journeys-Audit"];
+    expect(header).toBeTruthy();
+    const parsed = JSON.parse(header) as { adminUserId: string; action: string; loyaltyMemberId: string };
+    expect(parsed.adminUserId).toBe("u1");
+    expect(parsed.action).toBe("Deposit");
+    expect(parsed.loyaltyMemberId).toBe("acc-1");
+  });
+
+  it("does not attach X-Journeys-Audit on GET balances", async () => {
+    vi.stubEnv("JOURNEYS_TENANT_ID", "");
+    const fetchMock = vi.fn(async () => new Response("[]", { status: 200 }));
+    await journeysFetch("accounts/session/points/balances/acc-1", {}, {
+      getSession: async () => session(),
+      fetch: fetchMock as unknown as typeof fetch,
+      apiBaseUrl: "https://api.example"
+    });
+    const [, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
+    expect((init.headers as Record<string, string>)["X-Journeys-Audit"]).toBeUndefined();
+  });
 });
diff --git a/Journeys/Journeys.UX/src/lib/journeys-fetch.ts b/Journeys/Journeys.UX/src/lib/journeys-fetch.ts
index 27d3f10..b362df6 100644
--- a/Journeys/Journeys.UX/src/lib/journeys-fetch.ts
+++ b/Journeys/Journeys.UX/src/lib/journeys-fetch.ts
@@ -4,39 +4,48 @@ import { mapLoyaltyPath } from "./map-loyalty-path";
 import { resolveTenantId } from "./resolve-tenant-id";
 import { wrapApiEnvelope } from "./wrap-api-envelope";
 
 export type JourneysSession = {
   userId: string;
   tenantId: string;
   accessToken?: string;
   apiKey?: string;
 };
 
+export type AdminAuditHeader = {
+  loyaltyMemberId: string;
+  actionType: string;
+  action: string;
+  comment?: string;
+};
+
 export type JourneysFetchOptions = {
   method?: string;
   body?: unknown;
+  searchParams?: Record<string, string | undefined>;
+  audit?: AdminAuditHeader;
 };
 
 export type JourneysFetchDeps = {
   getSession: () => Promise<JourneysSession | null>;
   fetch: typeof fetch;
   apiBaseUrl: string;
 };
 
 export async function journeysFetch<T>(
   path: string,
   options: JourneysFetchOptions = {},
   deps?: JourneysFetchDeps
 ): Promise<ApiResponse<T>> {
   const timestamp = new Date().toISOString();
   const resolved: JourneysFetchDeps = deps ?? {
-    getSession: defaultGetSession,
+    getSession: getJourneysSession,
     fetch,
     apiBaseUrl: process.env.JOURNEYS_API_BASE_URL ?? ""
   };
   const session = await resolved.getSession();
   if (!session?.userId) {
     return { success: false, error: "Not authenticated", timestamp };
   }
   const tenantId = resolveTenantId(session.tenantId);
   if (!tenantId) {
     return { success: false, error: "TenantId is required", timestamp };
@@ -46,47 +55,63 @@ export async function journeysFetch<T>(
     return { success: false, error: "JOURNEYS_API_BASE_URL is not configured", timestamp };
   }
 
   let apiPath: string;
   try {
     apiPath = mapLoyaltyPath(path, tenantId);
   } catch (e) {
     const message = e instanceof Error ? e.message : String(e);
     return { success: false, error: message, timestamp };
   }
+  const searchParams = new URLSearchParams();
+  for (const [key, value] of Object.entries(options.searchParams ?? {})) {
+    if (value) searchParams.set(key, value);
+  }
+  const query = searchParams.toString();
+  if (query) apiPath += `?${query}`;
 
   const headers: Record<string, string> = { Accept: "application/json", "Content-Type": "application/json" };
   if (session.accessToken) {
     headers.Authorization = `Bearer ${session.accessToken}`;
   } else if (session.apiKey) {
     headers["Journeys-API-KEY"] = session.apiKey;
   } else {
     return { success: false, error: "No credentials in session", timestamp };
   }
 
+  if (options.audit) {
+    headers["X-Journeys-Audit"] = JSON.stringify({
+      loyaltyMemberId: options.audit.loyaltyMemberId,
+      adminUserId: session.userId,
+      actionType: options.audit.actionType,
+      action: options.audit.action,
+      comment: options.audit.comment ?? ""
+    });
+  }
+
   const method = options.method ?? "GET";
   if (!deps) allowInsecureLocalHttps(base);
   try {
     const res = await resolved.fetch(`${base}${apiPath}`, {
       method,
       headers,
       body: method === "GET" || options.body === undefined ? undefined : JSON.stringify(options.body),
       cache: "no-store"
     });
     const text = await res.text();
     return wrapApiEnvelope(res.status, text) as ApiResponse<T>;
   } catch (e) {
     return { success: false, error: `Cannot reach Journeys.API (${describeFetchFailure(e)})`, timestamp };
   }
 }
 
-async function defaultGetSession(): Promise<JourneysSession | null> {
+export async function getJourneysSession(): Promise<JourneysSession | null> {
   const { auth } = await import("@/auth");
   const s = await auth();
   if (!s?.user?.id) return null;
   return {
     userId: s.user.id,
     tenantId: (s as { tenantId?: string }).tenantId ?? "",
     accessToken: (s as { accessToken?: string }).accessToken,
     apiKey: (s as { apiKey?: string }).apiKey
   };
 }
diff --git a/Journeys/Journeys.UX/src/lib/map-loyalty-path.test.ts b/Journeys/Journeys.UX/src/lib/map-loyalty-path.test.ts
index 71b4d6f..931e8da 100644
--- a/Journeys/Journeys.UX/src/lib/map-loyalty-path.test.ts
+++ b/Journeys/Journeys.UX/src/lib/map-loyalty-path.test.ts
@@ -14,17 +14,105 @@ describe("mapLoyaltyPath", () => {
     );
   });
 
   it("maps events admin query and preserves schema name", () => {
     expect(mapLoyaltyPath("events/slug/LoyaltyAccountDetails/admin/query", "acme")).toBe(
       "/api/Events/acme/LoyaltyAccountDetails/admin/query"
     );
   });
 
   it("rejects unknown paths", () => {
-    expect(() => mapLoyaltyPath("campaigns/slug/save", "acme")).toThrow(/not-allowlisted:/);
+    expect(() => mapLoyaltyPath("campaigns/slug/cid-1/stats", "acme")).toThrow(/not-allowlisted:/);
   });
 
   it("rejects empty tenantId", () => {
     expect(() => mapLoyaltyPath("campaigns/x/getall", "")).toThrow(/tenant/i);
   });
+
+  const t = "acme";
+
+  it("maps getmany, save, validate, query", () => {
+    expect(mapLoyaltyPath("campaigns/session/getmany", t)).toBe("/api/Campaign/acme/getmany");
+    expect(mapLoyaltyPath("campaigns/session/save", t)).toBe("/api/Campaign/acme/save");
+    expect(mapLoyaltyPath("campaigns/session/validate", t)).toBe("/api/Campaign/acme/validate");
+    expect(mapLoyaltyPath("campaigns/session/query", t)).toBe("/api/Campaign/acme");
+  });
+
+  it("maps get-by-id, delete, copy, restore", () => {
+    expect(mapLoyaltyPath("campaigns/session/cid-1", t)).toBe("/api/Campaign/acme/cid-1");
+    expect(mapLoyaltyPath("campaigns/session/cid-1/copy", t)).toBe("/api/Campaign/acme/cid-1/copy");
+    expect(mapLoyaltyPath("campaigns/session/cid-1/restore", t)).toBe(
+      "/api/Campaign/acme/cid-1/restore"
+    );
+  });
+
+  it("maps versions, archived, live/draft by ext, PAT getall", () => {
+    expect(mapLoyaltyPath("campaigns/session/versions/ext-1", t)).toBe(
+      "/api/Campaign/acme/versions/ext-1"
+    );
+    expect(mapLoyaltyPath("campaigns/session/archived", t)).toBe("/api/Campaign/acme/archived");
+    expect(mapLoyaltyPath("campaigns/session/live/ext-1", t)).toBe(
+      "/api/Campaign/acme/live/ext-1"
+    );
+    expect(mapLoyaltyPath("campaigns/session/draft/ext-1", t)).toBe(
+      "/api/Campaign/acme/draft/ext-1"
+    );
+    expect(mapLoyaltyPath("campaigns/session/pointaccounttype/getall", t)).toBe(
+      "/api/Campaign/acme/pointaccounttype/getall"
+    );
+  });
+
+  it("maps campaign-agent conversations JSON", () => {
+    expect(mapLoyaltyPath("campaign-agent/conversations", t)).toBe(
+      "/api/v1/acme/campaign-agent/conversations"
+    );
+  });
+
+  it("still rejects unknown paths", () => {
+    expect(() => mapLoyaltyPath("campaigns/session/pointaccounttype/upsert", t)).toThrow(
+      /not-allowlisted:/
+    );
+    expect(() => mapLoyaltyPath("campaigns/session/cid-1/stats", t)).toThrow(/not-allowlisted:/);
+  });
+
+  it("maps account get, ext, balances, ledgers, deposit, withdrawal", () => {
+    expect(mapLoyaltyPath("accounts/session/acc-1", t)).toBe(`/api/Account/${t}/acc-1`);
+    expect(mapLoyaltyPath("accounts/session/ext/xref-1", t)).toBe(`/api/Account/${t}/ext/xref-1`);
+    expect(mapLoyaltyPath("accounts/session/points/balances/acc-1", t)).toBe(
+      `/api/Account/${t}/points/balances/acc-1`
+    );
+    expect(mapLoyaltyPath("accounts/session/points/acc-1", t)).toBe(`/api/Account/${t}/points/acc-1`);
+    expect(mapLoyaltyPath("accounts/session/points/deposit", t)).toBe(`/api/Account/${t}/points/deposit`);
+    expect(mapLoyaltyPath("accounts/session/points/withdrawal", t)).toBe(
+      `/api/Account/${t}/points/withdrawal`
+    );
+  });
+
+  it("does not treat points/deposit as an account id", () => {
+    expect(mapLoyaltyPath("accounts/session/points/deposit", t)).not.toBe(`/api/Account/${t}/points`);
+  });
+
+  it("maps journey enter, exit, preview, move", () => {
+    expect(
+      mapLoyaltyPath(
+        "journey/session/ManuallyEnter/camp-1/Journey/node-1/ForAccount/xref-1",
+        t
+      )
+    ).toBe(`/api/Journey/${t}/ManuallyEnter/camp-1/Journey/node-1/ForAccount/xref-1`);
+    expect(
+      mapLoyaltyPath(
+        "journey/session/ManuallyExit/camp-1/Journey/node-1/ForAccount/xref-1",
+        t
+      )
+    ).toBe(`/api/Journey/${t}/ManuallyExit/camp-1/Journey/node-1/ForAccount/xref-1`);
+    expect(mapLoyaltyPath("journey/session/PreviewTierMove", t)).toBe(
+      `/api/Journey/${t}/PreviewTierMove`
+    );
+    expect(mapLoyaltyPath("journey/session/MoveTier", t)).toBe(`/api/Journey/${t}/MoveTier`);
+  });
+
+  it("rejects account builder, expire path, and points admin", () => {
+    expect(() => mapLoyaltyPath("accounts/session/builder", t)).toThrow(/not-allowlisted:/);
+    expect(() => mapLoyaltyPath("accounts/session/points/expire", t)).toThrow(/not-allowlisted:/);
+    expect(() => mapLoyaltyPath("accounts/session/points/admin", t)).toThrow(/not-allowlisted:/);
+  });
 });
diff --git a/Journeys/Journeys.UX/src/lib/map-loyalty-path.ts b/Journeys/Journeys.UX/src/lib/map-loyalty-path.ts
index c2b1c45..6033ab7 100644
--- a/Journeys/Journeys.UX/src/lib/map-loyalty-path.ts
+++ b/Journeys/Journeys.UX/src/lib/map-loyalty-path.ts
@@ -1,17 +1,109 @@
 export function mapLoyaltyPath(path: string, tenantId: string): string {
   const tenant = tenantId.trim();
   if (!tenant) throw new Error("tenantId is required");
   const trimmed = path.replace(/^\/+/, "");
 
   if (/^campaigns\/[^/]+\/getall$/i.test(trimmed)) {
     return `/api/Campaign/${encodeURIComponent(tenant)}/getall`;
   }
+  if (/^campaigns\/[^/]+\/getmany$/i.test(trimmed)) {
+    return `/api/Campaign/${encodeURIComponent(tenant)}/getmany`;
+  }
+  if (/^campaigns\/[^/]+\/save$/i.test(trimmed)) {
+    return `/api/Campaign/${encodeURIComponent(tenant)}/save`;
+  }
+  if (/^campaigns\/[^/]+\/validate$/i.test(trimmed)) {
+    return `/api/Campaign/${encodeURIComponent(tenant)}/validate`;
+  }
+  if (/^campaigns\/[^/]+\/query$/i.test(trimmed)) {
+    return `/api/Campaign/${encodeURIComponent(tenant)}`;
+  }
+  if (/^campaigns\/[^/]+\/archived$/i.test(trimmed)) {
+    return `/api/Campaign/${encodeURIComponent(tenant)}/archived`;
+  }
+  if (/^campaigns\/[^/]+\/pointaccounttype\/getall$/i.test(trimmed)) {
+    return `/api/Campaign/${encodeURIComponent(tenant)}/pointaccounttype/getall`;
+  }
+  const versions = /^campaigns\/[^/]+\/versions\/([^/]+)$/i.exec(trimmed);
+  if (versions) {
+    return `/api/Campaign/${encodeURIComponent(tenant)}/versions/${encodeURIComponent(versions[1])}`;
+  }
+  const live = /^campaigns\/[^/]+\/live\/([^/]+)$/i.exec(trimmed);
+  if (live) {
+    return `/api/Campaign/${encodeURIComponent(tenant)}/live/${encodeURIComponent(live[1])}`;
+  }
+  const draft = /^campaigns\/[^/]+\/draft\/([^/]+)$/i.exec(trimmed);
+  if (draft) {
+    return `/api/Campaign/${encodeURIComponent(tenant)}/draft/${encodeURIComponent(draft[1])}`;
+  }
+  const copy = /^campaigns\/[^/]+\/([^/]+)\/copy$/i.exec(trimmed);
+  if (copy) {
+    return `/api/Campaign/${encodeURIComponent(tenant)}/${encodeURIComponent(copy[1])}/copy`;
+  }
+  const restore = /^campaigns\/[^/]+\/([^/]+)\/restore$/i.exec(trimmed);
+  if (restore) {
+    return `/api/Campaign/${encodeURIComponent(tenant)}/${encodeURIComponent(restore[1])}/restore`;
+  }
+  const one = /^campaigns\/[^/]+\/([^/]+)$/i.exec(trimmed);
+  if (one && !/^(getall|getmany|save|validate|archived|query)$/i.test(one[1])) {
+    return `/api/Campaign/${encodeURIComponent(tenant)}/${encodeURIComponent(one[1])}`;
+  }
+  if (/^campaign-agent\/conversations$/i.test(trimmed)) {
+    return `/api/v1/${encodeURIComponent(tenant)}/campaign-agent/conversations`;
+  }
   if (/^schemas\/[^/]+\/model\/all$/i.test(trimmed)) {
     return `/api/Model/${encodeURIComponent(tenant)}/GetMany`;
   }
   const events = /^events\/[^/]+\/([^/]+)\/admin\/query$/i.exec(trimmed);
   if (events) {
     return `/api/Events/${encodeURIComponent(tenant)}/${encodeURIComponent(events[1])}/admin/query`;
   }
+
+  const enc = (s: string) => encodeURIComponent(s);
+
+  if (/^accounts\/[^/]+\/points\/deposit$/i.test(trimmed)) {
+    return `/api/Account/${enc(tenant)}/points/deposit`;
+  }
+  if (/^accounts\/[^/]+\/points\/withdrawal$/i.test(trimmed)) {
+    return `/api/Account/${enc(tenant)}/points/withdrawal`;
+  }
+  const balances = /^accounts\/[^/]+\/points\/balances\/([^/]+)$/i.exec(trimmed);
+  if (balances) {
+    return `/api/Account/${enc(tenant)}/points/balances/${enc(balances[1])}`;
+  }
+  const ledgers = /^accounts\/[^/]+\/points\/([^/]+)$/i.exec(trimmed);
+  if (ledgers && !/^(deposit|withdrawal|balances|admin|expire)$/i.test(ledgers[1])) {
+    return `/api/Account/${enc(tenant)}/points/${enc(ledgers[1])}`;
+  }
+  const ext = /^accounts\/[^/]+\/ext\/([^/]+)$/i.exec(trimmed);
+  if (ext) {
+    return `/api/Account/${enc(tenant)}/ext/${enc(ext[1])}`;
+  }
+  const accountOne = /^accounts\/[^/]+\/([^/]+)$/i.exec(trimmed);
+  if (accountOne && !/^(points|ext|builder)$/i.test(accountOne[1])) {
+    return `/api/Account/${enc(tenant)}/${enc(accountOne[1])}`;
+  }
+
+  const enter =
+    /^journey\/[^/]+\/ManuallyEnter\/([^/]+)\/Journey\/([^/]+)\/ForAccount\/([^/]+)$/i.exec(
+      trimmed
+    );
+  if (enter) {
+    return `/api/Journey/${enc(tenant)}/ManuallyEnter/${enc(enter[1])}/Journey/${enc(enter[2])}/ForAccount/${enc(enter[3])}`;
+  }
+  const exit =
+    /^journey\/[^/]+\/ManuallyExit\/([^/]+)\/Journey\/([^/]+)\/ForAccount\/([^/]+)$/i.exec(
+      trimmed
+    );
+  if (exit) {
+    return `/api/Journey/${enc(tenant)}/ManuallyExit/${enc(exit[1])}/Journey/${enc(exit[2])}/ForAccount/${enc(exit[3])}`;
+  }
+  if (/^journey\/[^/]+\/PreviewTierMove$/i.test(trimmed)) {
+    return `/api/Journey/${enc(tenant)}/PreviewTierMove`;
+  }
+  if (/^journey\/[^/]+\/MoveTier$/i.test(trimmed)) {
+    return `/api/Journey/${enc(tenant)}/MoveTier`;
+  }
+
   throw new Error(`not-allowlisted: ${trimmed}`);
 }
diff --git a/Journeys/Journeys.UX/src/services/loyalty/actions.ts b/Journeys/Journeys.UX/src/services/loyalty/actions.ts
index af313d2..c4ff170 100644
--- a/Journeys/Journeys.UX/src/services/loyalty/actions.ts
+++ b/Journeys/Journeys.UX/src/services/loyalty/actions.ts
@@ -1,28 +1,199 @@
 "use server";
 
-import type { ApiResponse, CampaignListItem, SchemaListItem } from "@/lib/api-types";
+import type {
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
+  if (!res.success) return res as ApiResponse<CampaignListItem[]>;
+  return { ...res, data: extractEntities(res.data) as CampaignListItem[] };
+}
+
+export async function getArchivedCampaigns(): Promise<ApiResponse<CampaignListItem[]>> {
+  const res = await journeysFetch<unknown>(`campaigns/${TENANT_SLUG}/archived`);
   if (!res.success) return res as ApiResponse<CampaignListItem[]>;
   return { ...res, data: extractEntities(res.data) as CampaignListItem[] };
 }
 
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
   if (!res.success) return res as ApiResponse<SchemaListItem[]>;
   const schemas = extractEntities(res.data)
     .map(normalizeSchema)
     .filter((s): s is SchemaListItem => s !== null)
     .filter((s) => !s.modelType || s.modelType.toLowerCase() === LOYALTY_MODEL_TYPE);
@@ -30,32 +201,46 @@ export async function getAllSchemas(): Promise<ApiResponse<SchemaListItem[]>> {
 }
 
 export async function getSchemaByName(
   name: string
 ): Promise<ApiResponse<SchemaListItem | null>> {
   const res = await getAllSchemas();
   if (!res.success) return res as ApiResponse<SchemaListItem | null>;
   return { ...res, data: pickLiveSchema(res.data ?? [], name) };
 }
 
-export async function queryData(schemaName: string): Promise<ApiResponse<Record<string, unknown>[]>> {
-  if (!isUsableModelId(schemaName)) {
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
index c9cd727..cf43f66 100644
--- a/Journeys/Journeys.UX/src/services/loyalty/parse-list.test.ts
+++ b/Journeys/Journeys.UX/src/services/loyalty/parse-list.test.ts
@@ -1,25 +1,44 @@
 import { describe, expect, it } from "vitest";
-import { attributeNamesFromRows, extractEntities, normalizeSchema, pickLiveSchema } from "./parse-list";
+import {
+  attributeNamesFromRows,
+  extractContinuationToken,
+  extractConversationIds,
+  extractEntities,
+  normalizeSchema,
+  pickLiveSchema
+} from "./parse-list";
 
 describe("extractEntities", () => {
   it("returns arrays as-is", () => {
     expect(extractEntities([{ a: 1 }])).toEqual([{ a: 1 }]);
   });
   it("reads entities", () => {
     expect(extractEntities({ entities: [{ id: "1" }] })).toEqual([{ id: "1" }]);
   });
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
 describe("normalizeSchema", () => {
   it("reads PascalCase Name/Status/ModelType", () => {
     const s = normalizeSchema({
       ID: "guid-1",
       Name: "LoyaltyAccountDetails",
       Status: "Live",
       ModelType: "loyalty"
     });
     expect(s).toEqual({
       id: "guid-1",
@@ -48,20 +67,32 @@ describe("pickLiveSchema", () => {
   it("skips non-loyalty modelType", () => {
     expect(
       pickLiveSchema(
         [{ name: "LoyaltyAccountDetails", status: "Live", modelType: "event" }],
         "LoyaltyAccountDetails"
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
     ]);
   });
   it("falls back when there are no rows", () => {
     expect(attributeNamesFromRows([])).toEqual([{ name: "id" }, { name: "name" }]);
   });
diff --git a/Journeys/Journeys.UX/src/services/loyalty/parse-list.ts b/Journeys/Journeys.UX/src/services/loyalty/parse-list.ts
index 26e3d06..74fb3bb 100644
--- a/Journeys/Journeys.UX/src/services/loyalty/parse-list.ts
+++ b/Journeys/Journeys.UX/src/services/loyalty/parse-list.ts
@@ -1,49 +1,80 @@
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
+export function normalizeCampaignRow(row: unknown): CampaignListItem | null {
+  if (!row || typeof row !== "object") return null;
+  const rec = row as Record<string, unknown>;
+  return {
+    id: readString(rec, "id", "Id", "ID"),
+    name: readString(rec, "name", "Name"),
+    status: readString(rec, "status", "Status"),
+    extCampaignId: readString(rec, "extCampaignId", "ExtCampaignId"),
+    startDate: readString(rec, "startDate", "StartDate"),
+    endDate: readString(rec, "endDate", "EndDate")
+  };
+}
+
 export function extractEntities(data: unknown): unknown[] {
   if (Array.isArray(data)) return data;
   if (data && typeof data === "object") {
     const obj = data as Record<string, unknown>;
     for (const key of ["entities", "Entities", "items", "Items"]) {
       const value = obj[key];
       if (Array.isArray(value)) return value;
     }
   }
   return [];
 }
 
 export function normalizeSchema(row: unknown): SchemaListItem | null {
   if (!row || typeof row !== "object") return null;
   const rec = row as Record<string, unknown>;
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
 }
 
 export function pickLiveSchema(schemas: SchemaListItem[], name: string): SchemaListItem | null {
   const target = name.toLowerCase();
   for (const schema of schemas) {
