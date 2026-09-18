# Task 3 brief: Read-only account detail

Copy this plan section. Then follow **Journeys.UX constraints** and **inherited Task 2 notes**.

Source plan: `C:\Dev\Journeys\Journeys\docs\plans\2026-09-18-Journeys-ux-accounts-detail.md` (Task 3)
Spec: `C:\Dev\Journeys\Journeys\docs\specs\2026-09-18-Journeys-ux-accounts-detail-design.md`

Work directory: `C:\Dev\Journeys\Journeys\Journeys.UX`

**Do not git commit, push, merge, or open a PR.**

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
- **Already exists from Task 2:** `src/services/loyalty/eventable-schema.ts` — **do not recreate**; import `isEventableSchema` from there
- Modify: `Journeys.UX/src/services/loyalty/actions.ts` — GET account / ext / balances / ledgers (reads only)
- Modify: `Journeys.UX/src/components/loyalty/accounts/index.ts`

**Also required (deferred from Task 2, approved):** `DynamicEntityDetails` cannot compile without these. Create them in this task:

- Create: `src/components/loyalty/dynamic-data/dynamic-entity-details.tsx` (+ copy EXP test if it exists)
- Create: `src/components/shared/friendly-data-view.tsx` (copy EXP; it already uses `JsonTreeView` with a `value` prop — Journeys' `src/components/shared/json-tree-view.tsx` matches that)
- Create: `src/services/loyalty/utils/display-labels.ts` + copy EXP `display-labels.test.ts`
- Modify: `src/components/loyalty/dynamic-data/index.ts` — export `DynamicEntityDetails`

**Interfaces:**
- Produces:
  - `getLoyaltyAccountById(id: string)`
  - `getLoyaltyAccountByExternalId(extId: string)`
  - `getAccountPointBalances(id: string)`
  - `getAccountPoints(id: string)` (ledgers for progress)
  - Detail route renders without Actions writes yet (placeholder empty header ok). Task 5 adds Actions.

- [ ] **Step 1: Add read actions** (no `audit`). `TENANT_SLUG` is already `"session"` in `actions.ts`. Import `AccountPointBalance` from `@/lib/api-types`.

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

Normalize balances (and ledgers if wrapped) with `extractEntities` if the payload is wrapped. Never pass `audit` on these GETs.

- [ ] **Step 2: Copy `account-tier.ts` + tests. Tests PASS.** Source: `C:\Dev\Journeys\temp\exp\apps\admin-web\src\components\loyalty\accounts\account-tier.ts` + `.test.ts`. Fix imports only. Do not name a product tenant in comments.

- [ ] **Step 3: Copy `collapsible.tsx` from EXP** using `import { Collapsible as CollapsiblePrimitive } from "radix-ui"` (same as `dialog.tsx`). Add `import * as React from "react"` like `dialog.tsx` so `React.ComponentProps` typechecks.

- [ ] **Step 4: Copy detail client, campaign progress, eventable section, DynamicEntityDetails + helpers.** Rewrite imports.

**Delete** the Data Explorer `<Link>` block in `eventable-models-section.tsx` (EXP lines ~138–145). Remove the file-header comment that says row clicks go to data-explorer. Eventable query:

```ts
queryData({ schemaName: schema.name!, loyaltyAccountId, pageSize: 25 })
```

Do **not** call EXP `queryAdminData` or `getAllSchemas({ modelType, pageSize })`. Journeys `getAllSchemas()` takes no arguments and already filters loyalty models. Map `SchemaListItem` → `LoyaltySchema` the same way the list client does.

`isEventableSchema` already exists at `@/services/loyalty/eventable-schema`. Filter Live + `isEventableSchema`. Guard `schema.name` before `queryData` (never `modelId` `"unknown"`).

`SimpleDataTable` rows stay non-navigating (Task 2 default).

- [ ] **Step 5: `[id]/page.tsx`** — resolve identifiers on the server (copy EXP page logic, drop `PermissionGuard` / `PageWrapper` / `AccountActionsDropdown` until Task 5):

Call `queryData({ schemaName: LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME, queryString: LOYALTY_ACCOUNT_IDENTIFIER_QUERY, queryArgs: { "@id": id }, pageSize: 1 })` then GET by id then GET by ext, same order as EXP. Render `<LoyaltyAccountDetailClient accountId={id} />`. Heading: `Loyalty account`. A text link back to `/loyalty/accounts` is fine. Do not invent a builder or data-explorer link.

EXP page: `C:\Dev\Journeys\temp\exp\apps\admin-web\src\app\(admin)\loyalty\accounts\[id]\page.tsx`

- [ ] **Step 6: Points card** — copy `points-accounts-card.tsx` **without** Manage. Hide the Manage button. Do **not** copy `point-account-manage-modal.tsx` or the three panels (Task 4). Strip `PermissionGuard`. Types: `AccountPointBalance` from `@/lib/api-types`; point account types can be `PointAccountTypeListItem[]` from the same file. Create `src/components/loyalty/points/index.ts` exporting only `PointsAccountsCard`.

- [ ] **Step 7: `npx tsc --noEmit`.** Do **not** fix pre-existing errors in `src/auth.ts`, `src/components/loyalty/campaigns/campaigns-client.tsx`, `src/lib/loyalty-model.test.ts`. If those three remain and Task 3 files are clean, DONE_WITH_CONCERNS. New errors in Task 3 files are blockers.

  `npm test` PASS.

  Grep `eventable-models-section.tsx` for `data-explorer` — zero matches. Grep `src/components/loyalty/accounts` and `src/app/loyalty/accounts` for `/builder` — zero matches. Add a small source-text vitest on the eventable section (no `data-explorer`) like Task 2's list-client guard.

- [ ] **Step 8: Do not commit** unless asked.

---

## EXP copy sources

Root: `C:\Dev\Journeys\temp\exp\apps\admin-web\src`

- `components/loyalty/accounts/loyalty-account-detail-client.tsx`
- `components/loyalty/accounts/account-campaign-progress.tsx`
- `components/loyalty/accounts/eventable-models-section.tsx`
- `components/loyalty/accounts/account-tier.ts` + `.test.ts`
- `components/loyalty/accounts/loyalty-account-info-card.tsx` — **only if** the detail client still imports it (EXP detail client does not; skip)
- `components/loyalty/dynamic-data/dynamic-entity-details.tsx` + `.test.tsx`
- `components/shared/friendly-data-view.tsx`
- `services/loyalty/utils/display-labels.ts` + `.test.ts`
- `components/ui/collapsible.tsx`
- `components/loyalty/points/points-accounts-card.tsx` (Manage hidden)

Existing already in Journeys.UX (do not re-copy blindly):
- `getCampaigns`, `getPointAccountTypes`, `getAllSchemas`, `getSchemaByName`, `queryData`
- `loyaltyKeys` (accounts/points/eventable already appended)
- `JsonTreeView` at `@/components/shared/json-tree-view` (`value` prop)
- `Collapsible` does not exist yet — create it
- popover, command, card, badge, skeleton, button exist

---

## Rewrite rules (binding)

- No `@exp/*`, `bffFetch`, `getSlug`, `PermissionGuard`, `PageWrapper`, `X-ELP-Audit`, `queryAdminData`
- `LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME` from `@/services/loyalty/schema-names`
- `AccountPointBalance` from `@/lib/api-types`
- `LoyaltySchema` / `CURATED_ATTRIBUTE_LAYOUT_GROUPS` from `@/lib/loyalty-schema-types` (alias `LoyaltySchemaField` as `SchemaAttribute` if EXP used that name)
- No `/loyalty/accounts/builder`. No `data-explorer` hrefs. No Actions dropdown. No deposit/spend/expire modal.
- Campaign REST stays unaudited. These new actions are GETs — no `audit`.
- Do not name a product tenant. Do not discuss auth/OIDC/JWT in new comments.
- Do not log secrets, full event payloads, or raw audit JSON.
- Never `modelId` `"unknown"`.
- Do not delete existing comments without cause.
- Do not touch campaigns IA files.

`accounts/index.ts` currently exports list + selection modal. Append: `LoyaltyAccountDetailClient`, `EventableModelsSection`, `AccountCampaignProgress` (+ types). Do not export journey/tier action modals.

---

## TDD

1. Copy `account-tier.test.ts` first — FAIL (module missing).
2. Copy `account-tier.ts` — PASS.
3. Copy `display-labels.test.ts` first — FAIL then GREEN.
4. Eventable source-guard test (no `data-explorer`).

Capture RED/GREEN in the report.

---

## Out of scope

- Deposit / spend / expire / Manage modal (Task 4)
- Assign/remove journey / MoveTier (Task 5)
- Docs/graph/browser (Task 6)
- Commits
- Fixing the three pre-existing tsc errors
