# Journeys.UX Account Detail + Deposits/Tier Implementation Plan

> **Execution:** After approval, say execute and the intent name. `.agents/skills/journeys-plan-to-aidlc` starts `/aidlc classic`. Do **not** use superpowers:subagent-driven-development. Linear unit issues land before any `Journeys.*` code. Do not `--review none` or Express.

**Goal:** Lift EXP Accounts list click-through, account detail, and operator writes (deposit / spend / expire, manage tier, assign/remove journey) into `Journeys.UX` over HTTP to `Journeys.API`.

**Architecture:** Surgical copy from `temp/exp/apps/admin-web`. Extend `mapLoyaltyPath` + `journeysFetch` (`audit` → `X-Journeys-Audit`). `queryData` becomes the EXP params object and returns `meta.continuationToken`. No builder route. No Data Explorer links. No MoveTier client fallback. No deposit `expirationDate`.

**Tech Stack:** Next.js 16, React 19, TanStack Query, Vitest, existing shadcn/Radix, `journeysFetch`. No new C# endpoints.

**Spec:** `docs/specs/2026-09-18-Journeys-ux-accounts-detail-design.md`

## Global Constraints

- Do not invent capability ids. Existing: `event-models`, `campaigns`, `journeys`, `rules-engine`, `outcomes`, `campaign-agent`, `mcp-api`.
- `Journeys.UX` HTTP-only. No Core/DAL/Infra project-reference. No BFF. No `@exp/*`.
- Do not create `/loyalty/accounts/builder`. Missing schema copy: `The LoyaltyAccountDetails schema is missing or not Live.` No CTA.
- Eventable tables: no `data-explorer` hrefs. `isEventableSchema` = `tag === "eventable"` OR name `LoyaltyAccountDetails`.
- Expire = `POST accounts/session/points/withdrawal` with audit `action: "Expire"`. Do not add `/points/expire`.
- Move tier = `PreviewTierMove` then `MoveTier` only. Do not port EXP enter/exit fallback.
- Deposit form: amount + comment + confirm. No `expirationDate`.
- Campaign REST stays unaudited. Only account/journey **writes** pass `audit`.
- `adminUserId` in the audit JSON is the existing session user id. Never accept audit JSON from the browser. Do not add a free-form `headers` bag on `journeysFetch`.
- Drop `PermissionGuard`. Drop `X-ELP-Audit` (name is `X-Journeys-Audit`).
- Never `modelId` `"unknown"`.
- Do not log secrets, full event payloads, or raw audit JSON.
- Do not git commit, push, merge, or open a PR unless the user asks in that message.
- Do not delete existing comments without cause.
- Do not name a product tenant in new copy, specs, or comments. Route templates keep `{tenantId}` because that is the Journeys.API path shape. New tests reuse the existing `mapLoyaltyPath` dummy id already in `map-loyalty-path.test.ts` (`t`). Do not add new named-tenant fixtures.

## File map

| Path | Responsibility |
|------|----------------|
| `Journeys.UX/src/lib/map-loyalty-path.ts` | Account + Journey allowlist; specific paths before `{id}` |
| `Journeys.UX/src/lib/journeys-fetch.ts` | `audit?: AdminAuditHeader` → `X-Journeys-Audit` |
| `Journeys.UX/src/lib/api-types.ts` | `meta` on envelope; account/point types |
| `Journeys.UX/src/lib/loyalty-schema-types.ts` | Local schema/attribute types for lifted files |
| `Journeys.UX/src/services/loyalty/parse-list.ts` | `extractContinuationToken` |
| `Journeys.UX/src/services/loyalty/actions.ts` | Account/journey/points actions; object `queryData` |
| `Journeys.UX/src/services/loyalty/query-keys.ts` | accounts / points / eventable keys |
| `Journeys.UX/src/services/loyalty/eventable-schema.ts` | `isEventableSchema` |
| `Journeys.UX/src/services/loyalty/utils/account-identifiers.ts` | EXP identifier helpers |
| `Journeys.UX/src/services/loyalty/utils/grid-columns.ts` | Schema column helpers |
| `Journeys.UX/src/components/shared/data-table.tsx` | Lifted table primitive |
| `Journeys.UX/src/components/ui/collapsible.tsx` | Radix collapsible (same `radix-ui` import style as `dialog.tsx`) |
| `Journeys.UX/src/components/loyalty/dynamic-data/*` | List/detail/simple tables |
| `Journeys.UX/src/components/loyalty/accounts/*` | List, detail, journey/tier modals |
| `Journeys.UX/src/components/loyalty/points/*` | Points card + manage tabs |
| `Journeys.UX/src/app/loyalty/accounts/page.tsx` | Client list host |
| `Journeys.UX/src/app/loyalty/accounts/[id]/page.tsx` | Detail host |
| `docs/developer/journeys-ux.md` | Accounts screens |
| `docs/product/graph/path-map.yaml` | Add `event-models`, `outcomes` to UX prefix |
| `scripts/path-docs-map.yaml` | Add `docs/product/ontology/loyalty-account.md` |

Lift root: `C:\Dev\Journeys\temp\exp\apps\admin-web\src`. Rewrite `@exp/shared-types` → `@/lib/loyalty-schema-types` (and `api-types` where listed). Rewrite `bffFetch` / `getSlug` → `journeysFetch` + slug `"session"`. Strip `PermissionGuard`, `PageWrapper`, builder CTA, Data Explorer links, `expirationDate`, `moveTierViaJourneyFallback`.

---

### Task 1: Allowlist + audit + `queryData` params

**Files:**
- Modify: `Journeys.UX/src/lib/map-loyalty-path.ts`
- Modify: `Journeys.UX/src/lib/map-loyalty-path.test.ts`
- Modify: `Journeys.UX/src/lib/journeys-fetch.ts`
- Modify: `Journeys.UX/src/lib/journeys-fetch.test.ts`
- Modify: `Journeys.UX/src/lib/api-types.ts`
- Modify: `Journeys.UX/src/services/loyalty/parse-list.ts`
- Modify: `Journeys.UX/src/services/loyalty/parse-list.test.ts`
- Modify: `Journeys.UX/src/services/loyalty/actions.ts`

**Interfaces:**
- Consumes: existing `mapLoyaltyPath(path, tenantId)`, `journeysFetch(path, options, deps?)`
- Produces:
  - Account/Journey maps in Step 3
  - `AdminAuditHeader` + `JourneysFetchOptions.audit`
  - `QueryDataParams` object `queryData`
  - `extractContinuationToken(data: unknown): string | null`

- [ ] **Step 1: Write failing allowlist tests**

Append to `Journeys.UX/src/lib/map-loyalty-path.test.ts` (keep existing `t`):

```ts
it("maps account get, ext, balances, ledgers, deposit, withdrawal", () => {
  expect(mapLoyaltyPath("accounts/session/acc-1", t)).toBe(`/api/Account/${t}/acc-1`);
  expect(mapLoyaltyPath("accounts/session/ext/xref-1", t)).toBe(`/api/Account/${t}/ext/xref-1`);
  expect(mapLoyaltyPath("accounts/session/points/balances/acc-1", t)).toBe(
    `/api/Account/${t}/points/balances/acc-1`
  );
  expect(mapLoyaltyPath("accounts/session/points/acc-1", t)).toBe(`/api/Account/${t}/points/acc-1`);
  expect(mapLoyaltyPath("accounts/session/points/deposit", t)).toBe(`/api/Account/${t}/points/deposit`);
  expect(mapLoyaltyPath("accounts/session/points/withdrawal", t)).toBe(
    `/api/Account/${t}/points/withdrawal`
  );
});

it("does not treat points/deposit as an account id", () => {
  expect(mapLoyaltyPath("accounts/session/points/deposit", t)).not.toBe(`/api/Account/${t}/points`);
});

it("maps journey enter, exit, preview, move", () => {
  expect(
    mapLoyaltyPath(
      "journey/session/ManuallyEnter/camp-1/Journey/node-1/ForAccount/xref-1",
      t
    )
  ).toBe(`/api/Journey/${t}/ManuallyEnter/camp-1/Journey/node-1/ForAccount/xref-1`);
  expect(
    mapLoyaltyPath(
      "journey/session/ManuallyExit/camp-1/Journey/node-1/ForAccount/xref-1",
      t
    )
  ).toBe(`/api/Journey/${t}/ManuallyExit/camp-1/Journey/node-1/ForAccount/xref-1`);
  expect(mapLoyaltyPath("journey/session/PreviewTierMove", t)).toBe(
    `/api/Journey/${t}/PreviewTierMove`
  );
  expect(mapLoyaltyPath("journey/session/MoveTier", t)).toBe(`/api/Journey/${t}/MoveTier`);
});

it("rejects account builder, expire path, and points admin", () => {
  expect(() => mapLoyaltyPath("accounts/session/builder", t)).toThrow(/not-allowlisted:/);
  expect(() => mapLoyaltyPath("accounts/session/points/expire", t)).toThrow(/not-allowlisted:/);
  expect(() => mapLoyaltyPath("accounts/session/points/admin", t)).toThrow(/not-allowlisted:/);
});
```

- [ ] **Step 2: Run tests — expect FAIL** on the new cases.

```powershell
cd C:\Dev\Journeys\Journeys\Journeys.UX
npm test -- src/lib/map-loyalty-path.test.ts
```

- [ ] **Step 3: Implement maps** in `map-loyalty-path.ts` **before** the final `throw`. Specific account paths first. Encode every captured segment.

```ts
  const enc = (s: string) => encodeURIComponent(s);

  if (/^accounts\/[^/]+\/points\/deposit$/i.test(trimmed)) {
    return `/api/Account/${enc(tenant)}/points/deposit`;
  }
  if (/^accounts\/[^/]+\/points\/withdrawal$/i.test(trimmed)) {
    return `/api/Account/${enc(tenant)}/points/withdrawal`;
  }
  const balances = /^accounts\/[^/]+\/points\/balances\/([^/]+)$/i.exec(trimmed);
  if (balances) {
    return `/api/Account/${enc(tenant)}/points/balances/${enc(balances[1])}`;
  }
  const ledgers = /^accounts\/[^/]+\/points\/([^/]+)$/i.exec(trimmed);
  if (ledgers && !/^(deposit|withdrawal|balances|admin|expire)$/i.test(ledgers[1])) {
    return `/api/Account/${enc(tenant)}/points/${enc(ledgers[1])}`;
  }
  const ext = /^accounts\/[^/]+\/ext\/([^/]+)$/i.exec(trimmed);
  if (ext) {
    return `/api/Account/${enc(tenant)}/ext/${enc(ext[1])}`;
  }
  const accountOne = /^accounts\/[^/]+\/([^/]+)$/i.exec(trimmed);
  if (accountOne && !/^(points|ext|builder)$/i.test(accountOne[1])) {
    return `/api/Account/${enc(tenant)}/${enc(accountOne[1])}`;
  }

  const enter =
    /^journey\/[^/]+\/ManuallyEnter\/([^/]+)\/Journey\/([^/]+)\/ForAccount\/([^/]+)$/i.exec(
      trimmed
    );
  if (enter) {
    return `/api/Journey/${enc(tenant)}/ManuallyEnter/${enc(enter[1])}/Journey/${enc(enter[2])}/ForAccount/${enc(enter[3])}`;
  }
  const exit =
    /^journey\/[^/]+\/ManuallyExit\/([^/]+)\/Journey\/([^/]+)\/ForAccount\/([^/]+)$/i.exec(
      trimmed
    );
  if (exit) {
    return `/api/Journey/${enc(tenant)}/ManuallyExit/${enc(exit[1])}/Journey/${enc(exit[2])}/ForAccount/${enc(exit[3])}`;
  }
  if (/^journey\/[^/]+\/PreviewTierMove$/i.test(trimmed)) {
    return `/api/Journey/${enc(tenant)}/PreviewTierMove`;
  }
  if (/^journey\/[^/]+\/MoveTier$/i.test(trimmed)) {
    return `/api/Journey/${enc(tenant)}/MoveTier`;
  }
```

- [ ] **Step 4: Re-run allowlist tests — expect PASS.**

- [ ] **Step 5: Write failing audit + continuation tests**

Append to `journeys-fetch.test.ts` (reuse existing `session()` helper; do not add named-tenant env stubs):

```ts
  it("attaches X-Journeys-Audit on audit option and uses session user id", async () => {
    vi.stubEnv("JOURNEYS_TENANT_ID", "");
    const fetchMock = vi.fn(async () => new Response("{}", { status: 200 }));
    await journeysFetch(
      "accounts/session/points/deposit",
      {
        method: "POST",
        body: { amount: 1 },
        audit: {
          loyaltyMemberId: "acc-1",
          actionType: "Point Adjustment",
          action: "Deposit",
          comment: "ops"
        }
      },
      {
        getSession: async () => session(),
        fetch: fetchMock as unknown as typeof fetch,
        apiBaseUrl: "https://api.example"
      }
    );
    const [, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    const header = (init.headers as Record<string, string>)["X-Journeys-Audit"];
    expect(header).toBeTruthy();
    const parsed = JSON.parse(header) as { adminUserId: string; action: string; loyaltyMemberId: string };
    expect(parsed.adminUserId).toBe("u1");
    expect(parsed.action).toBe("Deposit");
    expect(parsed.loyaltyMemberId).toBe("acc-1");
  });

  it("does not attach X-Journeys-Audit on GET balances", async () => {
    vi.stubEnv("JOURNEYS_TENANT_ID", "");
    const fetchMock = vi.fn(async () => new Response("[]", { status: 200 }));
    await journeysFetch("accounts/session/points/balances/acc-1", {}, {
      getSession: async () => session(),
      fetch: fetchMock as unknown as typeof fetch,
      apiBaseUrl: "https://api.example"
    });
    const [, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    expect((init.headers as Record<string, string>)["X-Journeys-Audit"]).toBeUndefined();
  });
```

Append to `parse-list.test.ts`:

```ts
import { extractContinuationToken } from "./parse-list";

describe("extractContinuationToken", () => {
  it("reads continuationToken", () => {
    expect(extractContinuationToken({ entities: [], continuationToken: "tok" })).toBe("tok");
  });
  it("reads ContinuationToken", () => {
    expect(extractContinuationToken({ Entities: [], ContinuationToken: "tok2" })).toBe("tok2");
  });
  it("returns null when missing", () => {
    expect(extractContinuationToken({ entities: [] })).toBeNull();
  });
});
```

- [ ] **Step 6: `npm test` — expect FAIL** on audit + `extractContinuationToken`.

- [ ] **Step 7: Implement audit + types + parse + queryData**

`api-types.ts` — add `meta?: { continuationToken?: string | null; pageSize?: number }` to `ApiResponse`. Add:

```ts
export type AccountPointBalance = {
  accountId?: string;
  pointAccountTypeId?: string;
  currentBalance?: number;
  lifetimeTotal?: number;
};

export type QueryDataParams = {
  schemaName: string;
  queryString?: string;
  queryArgs?: Record<string, unknown>;
  loyaltyAccountId?: string;
  pageSize?: number;
  continuationToken?: string | null;
  sortBy?: string | null;
  sortOrder?: "ASC" | "DESC";
};
```

Expand `SchemaListItem` with optional `tag?: string` and keep attributes.

`journeys-fetch.ts`:

```ts
export type AdminAuditHeader = {
  loyaltyMemberId: string;
  actionType: string;
  action: string;
  comment?: string;
};

export type JourneysFetchOptions = {
  method?: string;
  body?: unknown;
  searchParams?: Record<string, string | undefined>;
  audit?: AdminAuditHeader;
};
```

After credential headers are set, if `options.audit`:

```ts
  if (options.audit) {
    headers["X-Journeys-Audit"] = JSON.stringify({
      loyaltyMemberId: options.audit.loyaltyMemberId,
      adminUserId: session.userId,
      actionType: options.audit.actionType,
      action: options.audit.action,
      comment: options.audit.comment ?? ""
    });
  }
```

Do **not** add `options.headers`. Export `getJourneysSession` as the existing default session reader (today’s private `defaultGetSession`) so later actions can read `userId` without duplicating it.

`parse-list.ts` — `normalizeSchema` also copy `tag` / `Tag`. Add:

```ts
export function extractContinuationToken(data: unknown): string | null {
  if (!data || typeof data !== "object" || Array.isArray(data)) return null;
  const rec = data as Record<string, unknown>;
  const token = rec.continuationToken ?? rec.ContinuationToken;
  return typeof token === "string" && token.length > 0 ? token : null;
}
```

Replace `queryData` in `actions.ts` (only caller today is the accounts page, rewritten in Task 2):

```ts
export async function queryData<T = Record<string, unknown>>(
  params: QueryDataParams
): Promise<ApiResponse<T[]>> {
  if (!isUsableModelId(params.schemaName)) {
    return {
      success: false,
      error: "A real model name is required; unknown model ids are not sent.",
      timestamp: new Date().toISOString()
    };
  }
  const pageSize = params.pageSize ?? 50;
  const res = await journeysFetch<unknown>(
    `events/${TENANT_SLUG}/${params.schemaName}/admin/query`,
    {
      method: "POST",
      body: {
        query: params.queryString ?? "",
        parameters: params.queryArgs ?? {},
        pageSize,
        continuationToken: params.continuationToken ?? null,
        sortBy: params.sortBy ?? "",
        sortOrder: params.sortOrder ?? "ASC",
        loyaltyAccountId: params.loyaltyAccountId
      }
    }
  );
  if (!res.success) return res as ApiResponse<T[]>;
  return {
    ...res,
    data: extractEntities(res.data) as T[],
    meta: {
      continuationToken: extractContinuationToken(res.data),
      pageSize
    }
  };
}
```

- [ ] **Step 8: `npm test` — PASS. `npx tsc --noEmit` — PASS** (accounts page still using old `queryData(string)` will fail typecheck until Task 2; if so, temporarily keep a deprecated overload **or** do Task 2 list page in the same change set as the signature change). Prefer changing `accounts/page.tsx` in Task 2 immediately after this step if tsc fails.

If tsc fails only on `page.tsx`, convert that file in this task to a thin host that does not call `queryData` yet:

```tsx
export default function AccountsPage() {
  return (
    <>
      <h1>Accounts</h1>
      <p className="empty">Loading accounts…</p>
    </>
  );
}
```

Task 2 replaces it. Do not leave a broken `queryData(schemaName)` call.

- [ ] **Step 9: Do not commit** unless the user asks in that message.

---

### Task 2: Identifiers + dynamic-data + accounts list

**Files:**
- Create: `Journeys.UX/src/lib/loyalty-schema-types.ts`
- Create: `Journeys.UX/src/services/loyalty/utils/account-identifiers.ts`
- Create: `Journeys.UX/src/services/loyalty/utils/account-identifiers.test.ts` (copy EXP tests)
- Create: `Journeys.UX/src/services/loyalty/utils/grid-columns.ts` (copy EXP)
- Create: `Journeys.UX/src/services/loyalty/utils/grid-columns.test.ts` (copy EXP)
- Create: `Journeys.UX/src/components/shared/data-table.tsx` (copy EXP)
- Create: `Journeys.UX/src/components/loyalty/dynamic-data/*` (copy EXP folder)
- Create: `Journeys.UX/src/components/loyalty/accounts/accounts-selection-modal.tsx` (imported by the table; list uses `skipAccountSelection`)
- Create: `Journeys.UX/src/components/loyalty/accounts/loyalty-accounts-list-client.tsx`
- Create: `Journeys.UX/src/components/loyalty/accounts/index.ts`
- Create: `Journeys.UX/src/services/loyalty/eventable-schema.ts`
- Modify: `Journeys.UX/src/services/loyalty/query-keys.ts`
- Modify: `Journeys.UX/src/app/loyalty/accounts/page.tsx`
- Test: assert list client source has no `/builder`

**Interfaces:**
- Consumes: `queryData(QueryDataParams)`, `getSchemaByName`, `LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME`
- Produces: `/loyalty/accounts` → `DynamicDataTable` with `detailRoutePath="/loyalty/accounts"` and `getDetailId` from `resolveLoyaltyAccountId`

- [ ] **Step 1: Copy EXP identifier tests** to `account-identifiers.test.ts` unchanged (they do not name a product tenant). `npm test -- src/services/loyalty/utils/account-identifiers.test.ts` — FAIL (module missing).

- [ ] **Step 2: Copy `account-identifiers.ts` from EXP.** Fix imports only. Tests PASS.

- [ ] **Step 3: Create `loyalty-schema-types.ts`** with the fields the lifted table/detail read (not the whole `@exp/shared-types` package). Minimum:

```ts
export type SchemaAttribute = {
  symbol?: string;
  name?: string;
  displayName?: string;
  dataType?: string;
  type?: string;
  attributeType?: string;
  status?: string;
  isRequired?: boolean;
  [key: string]: unknown;
};

export type LoyaltySchema = {
  id?: string;
  name?: string;
  status?: string;
  tag?: string;
  modelType?: string;
  attributes?: SchemaAttribute[];
  [key: string]: unknown;
};
```

Map `SchemaListItem` → this type in the list client (`attributes` already exist; `tag` from `normalizeSchema`).

- [ ] **Step 4: Copy `grid-columns.ts` + test, `data-table.tsx`, `dynamic-data/*`.** Rewrite `@exp/shared-types` to `@/lib/loyalty-schema-types`. Keep `queryData` object calls (`queryString` / `queryArgs`) — they now match Task 1.

- [ ] **Step 5: Copy `loyalty-accounts-list-client.tsx`.** Rewrite:

  - `getSchemaByName` stays.
  - Empty schema: **no** Sparkles/Button/Link. Render:

```tsx
<p className="empty">The LoyaltyAccountDetails schema is missing or not Live.</p>
```

  - Keep `DynamicDataTable` props: `skipAccountSelection`, `detailRoutePath="/loyalty/accounts"`, `getDetailId` / `fallbackIdColumn` using `resolveLoyaltyAccountId`.
  - Strip `PermissionGuard`.

- [ ] **Step 6: `accounts/page.tsx`** as a client host (Campaigns pattern: thin page + client). Server wrapper is fine:

```tsx
import { LoyaltyAccountsListClient } from "@/components/loyalty/accounts";

export default function AccountsPage() {
  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold tracking-tight">Accounts</h1>
      <LoyaltyAccountsListClient />
    </div>
  );
}
```

- [ ] **Step 7: Add query keys** (append, do not remove campaign keys):

```ts
  accounts: {
    all: ["loyalty", "accounts"] as const,
    detail: (id: string) => ["loyalty", "accounts", "detail", id] as const,
    detailByExt: (ext: string) => ["loyalty", "accounts", "detail-ext", ext] as const,
    loyaltyDetail: (id: string) => ["loyalty", "accounts", "loyalty", id] as const
  },
  points: {
    balancesByAccount: (id: string) => ["loyalty", "points", "balances", id] as const,
    ledgersByAccount: (id: string) => ["loyalty", "points", "ledgers", id] as const
  },
  eventable: {
    bySchemaAccount: (schemaName: string, accountId: string) =>
      ["loyalty", "eventable", schemaName, accountId] as const
  }
```

- [ ] **Step 8: `npm test` PASS. `npx tsc --noEmit` PASS.** Grep list client: no `builder`, no `data-explorer`.

- [ ] **Step 9: Do not commit** unless the user asks.

---

### Task 3: Read-only account detail

**Files:**
- Create: `Journeys.UX/src/app/loyalty/accounts/[id]/page.tsx`
- Create: `Journeys.UX/src/components/loyalty/accounts/loyalty-account-detail-client.tsx`
- Create: `Journeys.UX/src/components/loyalty/accounts/loyalty-account-info-card.tsx` (if still imported; otherwise skip)
- Create: `Journeys.UX/src/components/loyalty/accounts/account-tier.ts` + `account-tier.test.ts`
- Create: `Journeys.UX/src/components/loyalty/accounts/account-campaign-progress.tsx`
- Create: `Journeys.UX/src/components/loyalty/accounts/eventable-models-section.tsx`
- Create: `Journeys.UX/src/components/ui/collapsible.tsx`
- Create: `Journeys.UX/src/services/loyalty/eventable-schema.ts`
- Modify: `Journeys.UX/src/services/loyalty/actions.ts` — GET account / ext / balances / ledgers (reads only)
- Modify: `Journeys.UX/src/components/loyalty/accounts/index.ts`

**Interfaces:**
- Produces:
  - `getLoyaltyAccountById(id: string)`
  - `getLoyaltyAccountByExternalId(extId: string)`
  - `getAccountPointBalances(id: string)`
  - `getAccountPoints(id: string)` (ledgers for progress)
  - Detail route renders without Actions writes yet (placeholder empty header ok). Task 5 adds Actions.

- [ ] **Step 1: Add read actions** (no `audit`):

```ts
export async function getLoyaltyAccountById(id: string) {
  return journeysFetch<Record<string, unknown>>(`accounts/${TENANT_SLUG}/${id}`);
}
export async function getLoyaltyAccountByExternalId(extId: string) {
  return journeysFetch<Record<string, unknown>>(`accounts/${TENANT_SLUG}/ext/${extId}`);
}
export async function getAccountPointBalances(loyaltyAccountId: string) {
  return journeysFetch<AccountPointBalance[]>(
    `accounts/${TENANT_SLUG}/points/balances/${loyaltyAccountId}`
  );
}
export async function getAccountPoints(loyaltyAccountId: string) {
  return journeysFetch<unknown[]>(`accounts/${TENANT_SLUG}/points/${loyaltyAccountId}`);
}
```

Normalize balances with `extractEntities` if the payload is wrapped.

- [ ] **Step 2: Copy `account-tier.ts` + tests. Tests PASS.**

- [ ] **Step 3: Copy `collapsible.tsx` from EXP using `import { Collapsible as CollapsiblePrimitive } from "radix-ui"`** (same as `dialog.tsx`).

- [ ] **Step 4: Copy detail client, campaign progress, eventable section.** Rewrite imports. **Delete** the Data Explorer `<Link>` block in `eventable-models-section.tsx`. Eventable query:

```ts
queryData({ schemaName: schema.name!, loyaltyAccountId, pageSize: 25 })
```

`isEventableSchema`:

```ts
import { LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME } from "@/services/loyalty/schema-names";
import type { LoyaltySchema } from "@/lib/loyalty-schema-types";

export function isEventableSchema(schema: LoyaltySchema): boolean {
  return schema.tag === "eventable" || schema.name === LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME;
}
```

Filter Live + `isEventableSchema`.

- [ ] **Step 5: `[id]/page.tsx`** — resolve identifiers on the server (copy EXP page logic, drop `PermissionGuard` / `PageWrapper` / Actions until Task 5):

Call `queryData({ schemaName: LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME, queryString: LOYALTY_ACCOUNT_IDENTIFIER_QUERY, queryArgs: { "@id": id }, pageSize: 1 })` then GET by id then GET by ext, same order as EXP. Render `<LoyaltyAccountDetailClient accountId={id} />`. Title: Accounts breadcrumb can be a text heading `Loyalty account`.

- [ ] **Step 6: Points card** — for this task, copy `points-accounts-card.tsx` **without** Manage (hide the button or leave it disabled). Task 4 wires Manage. Prefer hiding Manage until Task 4 so we do not ship a dead modal.

- [ ] **Step 7: `npx tsc --noEmit` PASS. `npm test` PASS.** Grep `eventable-models-section.tsx` for `data-explorer` — zero matches. Grep `accounts` for `/builder` — zero matches.

- [ ] **Step 8: Do not commit** unless asked.

---

### Task 4: Deposit / spend / expire

**Files:**
- Create: `Journeys.UX/src/components/loyalty/points/*` (card already started; add modal + three panels + `confirm-point-adjustment.ts`)
- Modify: `Journeys.UX/src/services/loyalty/actions.ts`
- Modify: `Journeys.UX/src/components/loyalty/accounts/loyalty-account-detail-client.tsx` — enable Manage

**Interfaces:**
- Produces:
  - `depositPoints(input)`
  - `withdrawPoints(input)` (spend)
  - `expirePoints(input)` (same path as withdraw; audit `Expire`)

```ts
export type PointMutationInput = {
  loyaltyAccountId: string;
  pointAccountTypeId: string;
  amount: number;
  comment: string;
};

export async function depositPoints(input: PointMutationInput) {
  const eventId = crypto.randomUUID();
  return journeysFetch(`accounts/${TENANT_SLUG}/points/deposit`, {
    method: "POST",
    body: {
      loyaltyAccountId: input.loyaltyAccountId,
      pointAccountTypeId: input.pointAccountTypeId,
      amount: input.amount,
      depositDate: new Date().toISOString(),
      eventType: "admin",
      eventId,
      userId: undefined
    },
    audit: {
      loyaltyMemberId: input.loyaltyAccountId,
      actionType: "Point Adjustment",
      action: "Deposit",
      comment: input.comment
    }
  });
}
```

`adminUserId` in the audit JSON is `session.userId` inside `journeysFetch`. Also set body `userId` from the same session: export `getJourneysSession` from `journeys-fetch.ts` (today’s private `defaultGetSession`). Do not import `@/auth` from actions.

```ts
import { getJourneysSession } from "@/lib/journeys-fetch";

async function requireUserId(): Promise<string | null> {
  const s = await getJourneysSession();
  return s?.userId ?? null;
}
```

If `requireUserId()` is null, return `{ success: false, error: "Session user id is required", timestamp }` **without** calling fetch. Include `userId` on the deposit/withdrawal body when present.

Spend/expire: `points/withdrawal`, body `withdrawalDate`, `status: "shipped"`, `eventType: "admin"`, `eventId`. Expire: if `isPercent`, compute amount in the action (`(amount/100)*currentBalance`) before POST. Audit action `"Spend"` vs `"Expire"`.

- [ ] **Step 1: Write a small unit test** `src/services/loyalty/point-expire-amount.test.ts`:

```ts
import { describe, expect, it } from "vitest";
import { resolveExpireAmount } from "./point-expire-amount";

describe("resolveExpireAmount", () => {
  it("uses absolute amount", () => {
    expect(resolveExpireAmount({ amount: 5, isPercent: false })).toBe(5);
  });
  it("uses percent of balance", () => {
    expect(resolveExpireAmount({ amount: 50, isPercent: true, currentBalance: 200 })).toBe(100);
  });
  it("rejects percent without balance", () => {
    expect(() => resolveExpireAmount({ amount: 10, isPercent: true })).toThrow(/currentBalance/);
  });
});
```

- [ ] **Step 2: Implement `point-expire-amount.ts`. Tests PASS.**

- [ ] **Step 3: Copy points panels.** Remove `expirationDate` from deposit schema/form/defaultValues. Confirm copy: `window.confirm(\`${action} ${amount}? This will update the member balance.\`)` (drop EXP “Action Log”). Comment required (zod min 1). `adminUserId` from `useSession()?.user?.id` is **not** sent as audit JSON from the client; the **server action** builds `audit`. Panels call `depositPoints` / `withdrawPoints` / `expirePoints` with account id, PAT id, amount, comment only.

- [ ] **Step 4: Wire Manage on `PointsAccountsCard`.** Invalidate `loyaltyKeys.points.balancesByAccount` on success. Toast on success/error (`react-hot-toast` already in the app).

- [ ] **Step 5: `npm test` PASS. `npx tsc --noEmit` PASS.**

- [ ] **Step 6: Do not commit** unless asked.

---

### Task 5: Assign / remove journey + manage tier

**Files:**
- Create: `account-actions-dropdown.tsx`, `account-journey-modal.tsx`, `manage-tier-modal.tsx`
- Modify: `[id]/page.tsx` — pass identifiers into Actions
- Modify: `actions.ts` — enter, exit, preview, move

**Interfaces:**

```ts
export async function enterJourney(params: {
  campaignId: string;
  journeyId: string;
  loyaltyAccountId: string;
  loyaltyAccountXReference: string;
  comment: string;
}) {
  return journeysFetch(
    `journey/${TENANT_SLUG}/ManuallyEnter/${params.campaignId}/Journey/${params.journeyId}/ForAccount/${params.loyaltyAccountXReference}`,
    {
      method: "GET",
      audit: {
        loyaltyMemberId: params.loyaltyAccountId,
        actionType: "Journey Movement",
        action: "Add",
        comment: params.comment
      }
    }
  );
}
```

Same for `ManuallyExit` / action `"Remove"`.

```ts
export async function previewTierMove(params: {
  loyaltyAccountId: string;
  loyaltyAccountXReference?: string;
  targetCampaignId: string;
  targetJourneyId: string;
  comment?: string;
}) {
  return journeysFetch(`journey/${TENANT_SLUG}/PreviewTierMove`, {
    method: "POST",
    body: {
      loyaltyAccountId: params.loyaltyAccountId,
      loyaltyAccountXReference: params.loyaltyAccountXReference,
      targetCampaignId: params.targetCampaignId,
      targetJourneyId: params.targetJourneyId,
      comment: params.comment ?? "",
      adminUserId: (await requireUserId()) ?? ""
    }
  });
}

export async function moveTier(params: { ...; comment: string }) {
  const adminUserId = await requireUserId();
  if (!adminUserId) {
    return { success: false, error: "Session user id is required", timestamp: new Date().toISOString() };
  }
  return journeysFetch(`journey/${TENANT_SLUG}/MoveTier`, {
    method: "POST",
    body: { ...params, adminUserId, comment: params.comment },
    audit: {
      loyaltyMemberId: params.loyaltyAccountId,
      actionType: "Journey Movement",
      action: "Move Tier",
      comment: params.comment
    }
  });
}
```

**Do not** implement `moveTierViaJourneyFallback`. On MoveTier failure, return the API error.

- [ ] **Step 1: Copy the three EXP components.** Strip `PermissionGuard` (always show Actions on this screen). Tier comment min 3. Preview then commit.

- [ ] **Step 2: Put Actions in the detail page header** (flex row with heading), passing `loyaltyAccountId` + `loyaltyAccountXReference` from the same identifier resolution as EXP.

- [ ] **Step 3: `npx tsc --noEmit` PASS. `npm test` PASS.** Grep `actions.ts` for `moveTierViaJourneyFallback` — zero matches.

- [ ] **Step 4: Do not commit** unless asked.

---

### Task 6: Docs + graph + browser pass

**Files:**
- Modify: `docs/developer/journeys-ux.md`
- Modify: `docs/product/graph/path-map.yaml`
- Modify: `scripts/path-docs-map.yaml`

**Interfaces:** spec §10.

- [ ] **Step 1: Path-map** — `Journeys.UX` nodes:

```yaml
  - prefix: Journeys.UX
    nodes: [campaigns, journeys, campaign-agent, event-models, outcomes]
    meaningOptional: false
```

Do **not** add `IMPLEMENTED_AS` `proj-ux`.

- [ ] **Step 2: `scripts/path-docs-map.yaml`** — under Journeys.UX docs, add `docs/product/ontology/loyalty-account.md`.

- [ ] **Step 3: `docs/developer/journeys-ux.md`** — replace “Accounts table” with:

```markdown
## Accounts

`/loyalty/accounts` is the schema-driven list (`DynamicDataTable`, search, row click). Missing/not-Live `LoyaltyAccountDetails`: *The LoyaltyAccountDetails schema is missing or not Live.* No builder.

`/loyalty/accounts/[id]` is detail: profile, point balances, schema fields, campaign progress, inline eventable models (no Data Explorer).

Writes: deposit / spend / expire (`POST .../points/deposit` and `.../points/withdrawal`; expire is withdrawal + audit action Expire), assign/remove journey, preview + MoveTier. Mutating calls send `X-Journeys-Audit` built on the Next server. `/loyalty/accounts/builder` is not shipped.
```

Do not add named-tenant examples. Do not rewrite Campaigns sections except the “This spec’s screens” line: Overview, Accounts detail, Campaigns IA.

- [ ] **Step 4: Impact scripts** from `C:\Dev\Journeys\Journeys`:

```powershell
$files = @(
  "Journeys.UX/src/lib/map-loyalty-path.ts",
  "Journeys.UX/src/app/loyalty/accounts/page.tsx",
  "docs/developer/journeys-ux.md",
  "docs/product/graph/path-map.yaml",
  "scripts/path-docs-map.yaml"
)
.\scripts\docs-impact.ps1 -Files $files
.\scripts\graph-impact.ps1 -Files $files
```

Expected: exit 0.

- [ ] **Step 5: `cd Journeys.UX; npm test; npx tsc --noEmit`** — PASS.

- [ ] **Step 6: Browser** (human or agent with browser tools): list search + row click; detail sections; deposit/spend/expire confirm; manage tier preview+commit; assign/remove journey; missing-schema sentence; `/loyalty/accounts/builder` 404.

- [ ] **Step 7: Do not commit** unless asked.

---

## Spec coverage (self-review)

| Spec | Task |
|------|------|
| G1 DynamicDataTable list + click-through | 2 |
| G2 detail composition, eventable inline | 3 |
| G3 writes | 4, 5 |
| G4 HTTP-only allowlist | 1 |
| G5 `X-Journeys-Audit` on writes only | 1, 4, 5 |
| G6 surgical lift, no Prisma/PermissionGuard | 2–5 |
| No builder / one-sentence empty | 2 |
| No Data Explorer | 3 |
| Expire = withdrawal | 4 |
| No MoveTier fallback | 5 |
| No expirationDate | 4 |
| Docs/graph | 6 |
