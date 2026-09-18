# Task 2 brief: Identifiers + dynamic-data + accounts list

Copy this plan section exactly. Then follow **Journeys.UX constraints** and **surgical lift notes** below.

Source plan: `C:\Dev\Journeys\Journeys\docs\plans\2026-09-18-Journeys-ux-accounts-detail.md` (Task 2)
Spec: `C:\Dev\Journeys\Journeys\docs\specs\2026-09-18-Journeys-ux-accounts-detail-design.md`

Work directory: `C:\Dev\Journeys\Journeys\Journeys.UX`

**Do not git commit, push, merge, or open a PR.**

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

Also add the small types `data-table.tsx` needs instead of `@exp/shared-types`:

```ts
export type JsonValue = string | number | boolean | null | JsonValue[] | { [key: string]: JsonValue };

export type PaginationMeta = {
  page: number;
  limit: number;
  total: number;
  totalPages: number;
  hasNext: boolean;
  hasPrev: boolean;
};

export type PaginationParams = {
  page?: number;
  limit?: number;
  sortBy?: string;
  sortOrder?: "asc" | "desc";
};

export const CURATED_ATTRIBUTE_LAYOUT_GROUPS = [
  "header-panel",
  "top-left-split-panel",
  "top-right-split-panel",
  "middle-panel",
  "outcomes",
] as const;
```

SchemaAttribute must also carry the grid/layout fields `grid-columns.ts` and `dynamic-entity-details.tsx` read (`isInGrid`, `gridColumnNumber`, `isGridSortable`, `defaultSortDirection`, `attributeLayoutGroup`, `attributeLayoutPosition`, `attributeLayoutHeight`, `isDetailsViewable`, `modelId`). Index signature covers extras.

- [ ] **Step 4: Copy `grid-columns.ts` + test, `data-table.tsx`, `dynamic-data/*`.** Rewrite `@exp/shared-types` to `@/lib/loyalty-schema-types`. Keep `queryData` object calls (`queryString` / `queryArgs`) — they now match Task 1.

- [ ] **Step 5: Copy `loyalty-accounts-list-client.tsx`.** Rewrite:

  - `getSchemaByName` stays. Import `LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME` from `@/services/loyalty/schema-names` (already exists).
  - Empty schema: **no** Sparkles/Button/Link. Render:

```tsx
<p className="empty">The LoyaltyAccountDetails schema is missing or not Live.</p>
```

  - Keep `DynamicDataTable` props: `skipAccountSelection`, `detailRoutePath="/loyalty/accounts"`, `getDetailId` / `fallbackIdColumn` using `resolveLoyaltyAccountId`.
  - Strip `PermissionGuard`.
  - Map `SchemaListItem` → `LoyaltySchema` when passing `schema=` (cast/spread is fine; do not drop `attributes` or `tag`).

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

- [ ] **Step 8: `npm test` PASS. `npx tsc --noEmit`** — do **not** "fix" pre-existing errors in `src/auth.ts`, `src/components/loyalty/campaigns/campaigns-client.tsx`, `src/lib/loyalty-model.test.ts`. If those three remain and Task 2 files are clean, report DONE_WITH_CONCERNS. New errors in Task 2 files are blockers you must fix.

  Grep list client: no `builder`, no `data-explorer`. Add a small vitest that reads `loyalty-accounts-list-client.tsx` as text and asserts it does not contain `/builder` or `data-explorer`.

- [ ] **Step 9: Do not commit** unless the user asks.

Also create `eventable-schema.ts` now (listed in this task):

```ts
import { LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME } from "@/services/loyalty/schema-names";
import type { LoyaltySchema } from "@/lib/loyalty-schema-types";

export function isEventableSchema(schema: LoyaltySchema): boolean {
  return schema.tag === "eventable" || schema.name === LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME;
}
```

A tiny unit test for that helper is welcome.

`accounts/index.ts` for this task exports only what exists: `LoyaltyAccountsListClient`, `AccountsSelectionModal` (+ its types). Do **not** re-export detail/journey/tier components that are Task 3–5.

---

## EXP copy sources (read, then copy)

Root: `C:\Dev\Journeys\temp\exp\apps\admin-web\src`

- `services/loyalty/utils/account-identifiers.ts` + `.test.ts`
- `services/loyalty/utils/grid-columns.ts` + `.test.ts`
- `components/shared/data-table.tsx`
- `components/loyalty/dynamic-data/*` (table, simple-data-table, entity-details, date-range-filter, index, tests)
- `components/loyalty/accounts/accounts-selection-modal.tsx`
- `components/loyalty/accounts/loyalty-accounts-list-client.tsx`
- `components/ui/table.tsx` and `components/ui/pagination.tsx` — **Journeys.UX does not have these yet.** Copy them into `src/components/ui/` so `data-table.tsx` compiles. Match existing Journeys `dialog.tsx` / `button.tsx` style (`cn` from `@/lib/utils`, `buttonVariants` already exported from `button.tsx`).

Do **not** copy `builder/page.tsx`. Do **not** create `src/app/loyalty/accounts/builder`.

---

## Rewrite rules (binding)

- No `@exp/*` imports. No `bffFetch`. No `getSlug`. No `PermissionGuard`. No `PageWrapper`. No `X-ELP-Audit`.
- Slug in lifted paths stays `"session"` (already used by Journeys `TENANT_SLUG` / `mapLoyaltyPath`).
- `LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME` lives at `@/services/loyalty/schema-names` (`"LoyaltyAccountDetails"`).
- `queryData` already takes `QueryDataParams` (Task 1). Keep EXP object-call shape (`queryString`, `queryArgs`, `pageSize`, `continuationToken`, `sortBy`, `sortOrder`).
- `queryData` returns `{ data, meta: { continuationToken, pageSize } }`. Wire DynamicDataTable continuation paging to `res.meta?.continuationToken`.
- **Do not default row navigation to `/loyalty/data-explorer/...`.** EXP `dynamic-data-table.tsx` does `detailRoutePath ?? `/loyalty/data-explorer/${schema.id}``. Change that default to **no navigation** (`null`) unless `detailRoutePath` is passed. Accounts list passes `detailRoutePath="/loyalty/accounts"`.
- **DateRangeFilter:** EXP wraps `@/components/shared/date-time-range-picker` and `@/lib/date-time-range`, which do **not** exist in Journeys.UX. Do **not** port that whole picker stack. Reimplement `DateRangeFilter` with two existing `<Input type="date">` fields that emit the same ISO date strings (`fromDate` / `toDate`). Keep the exported props interface.
- Drop unused Sparkles / builder Button / Link from the list empty state.
- Do not name a product tenant in comments, copy, or tests. Reuse existing dummy ids from EXP identifier tests (they do not name a tenant).
- Do not discuss auth/OIDC/JWT in new comments.
- Do not log secrets, full event payloads, or raw audit JSON.
- Never send `modelId` `"unknown"`.
- Do not delete existing comments in files you did not copy, without cause.
- Campaign REST in `actions.ts` stays unaudited. Do not pass `audit` from list/query.
- TanStack Query is already provided by `src/components/providers.tsx`. Do not add `@tanstack/react-table`. EXP `data-table.tsx` is a custom table, not react-table.

---

## Existing Journeys.UX facts

- Current `src/app/loyalty/accounts/page.tsx` is a Task 1 stub (`Loading accounts…`). Replace it per Step 6.
- `getSchemaByName` returns `ApiResponse<SchemaListItem | null>` via `pickLiveSchema` (Live only). Empty/null → the one-sentence empty state.
- `SchemaListItem.attributes` is typed narrowly in `api-types.ts`; runtime objects keep extra fields. Map/spread to `LoyaltySchema`. You may widen `SchemaListItem.attributes` to `SchemaAttribute[]` if it unblocks `tsc`; do not strip attributes in `normalizeSchema`.
- `loyaltyKeys` already has campaigns/schemas/pointAccountTypes/dashboard. **Append** accounts/points/eventable.
- Existing shadcn: alert, badge, button, card, checkbox, dialog, input, select, skeleton, etc. Missing: `table`, `pagination`, `collapsible` (collapsible is Task 3).
- Pre-existing `npx tsc --noEmit` errors (do not fix as part of this task):
  - `src/auth.ts(47,40)`
  - `src/components/loyalty/campaigns/campaigns-client.tsx(124,9)`
  - `src/lib/loyalty-model.test.ts(40,12)`

---

## TDD

Follow the plan: identifier tests first (RED then copy impl GREEN), then grid-columns tests, then the list-client source grep test.

Capture RED/GREEN command output in the report.

---

## Out of scope (do not build)

- `/loyalty/accounts/[id]` (Task 3)
- Deposit / spend / expire (Task 4)
- Journey/tier Actions (Task 5)
- Docs/graph (Task 6)
- Account builder
- Data Explorer routes
- New C# endpoints
- Commits
