# Task 2 report — Identifiers + dynamic-data + accounts list

**Status:** DONE_WITH_CONCERNS
**Commits:** none (nothing committed, nothing staged by me)
**Work dir:** `C:\Dev\Journeys\Journeys\Journeys.UX`

---

## What I implemented

`/loyalty/accounts` is now the schema-driven list. It loads the Live
`LoyaltyAccountDetails` schema via `getSchemaByName`, builds columns from that
schema's grid config, queries rows through the Task 1 object-form
`queryData(QueryDataParams)`, pages with `meta.continuationToken`, and routes
row clicks to `/loyalty/accounts/{id}` using `resolveLoyaltyAccountId`.

### Created

| File | Responsibility |
|------|----------------|
| `src/lib/loyalty-schema-types.ts` | `SchemaAttribute`, `LoyaltySchema`, `JsonValue`, `PaginationMeta`, `PaginationParams`, `CURATED_ATTRIBUTE_LAYOUT_GROUPS` — replaces `@exp/shared-types` |
| `src/services/loyalty/utils/account-identifiers.ts` | `LOYALTY_ACCOUNT_IDENTIFIER_QUERY`, `..._SEARCH_FIELDS`, `normalizeEntityWithEvent`, `resolveLoyaltyAccountId`, `resolveLoyaltyAccountXReference` |
| `src/services/loyalty/utils/account-identifiers.test.ts` | Lifted identifier tests |
| `src/services/loyalty/utils/grid-columns.ts` | `getNestedValue`, `getSchemaFieldValue`, `formatValueAsString`, `getGridAttributes`, `generateColumnsFromSchema`, `getInitialSortFromSchema` |
| `src/services/loyalty/utils/grid-columns.test.ts` | Lifted grid-column tests |
| `src/services/loyalty/eventable-schema.ts` | `isEventableSchema` |
| `src/services/loyalty/eventable-schema.test.ts` | Unit test for the above |
| `src/components/ui/table.tsx` | shadcn table primitive (was missing) |
| `src/components/ui/pagination.tsx` | shadcn pagination primitive (was missing) |
| `src/components/shared/data-table.tsx` | Lifted custom table primitive (no `@tanstack/react-table`) |
| `src/components/loyalty/dynamic-data/dynamic-data-table.tsx` | Schema-driven grid: search, column filters, date range, sort, CSV export, continuation paging, row click |
| `src/components/loyalty/dynamic-data/simple-data-table.tsx` | Read-only nested grid |
| `src/components/loyalty/dynamic-data/date-range-filter.tsx` | Reimplemented on two `<Input type="date">` (see deviations) |
| `src/components/loyalty/dynamic-data/index.ts` | Barrel |
| `src/components/loyalty/accounts/accounts-selection-modal.tsx` | Account picker used by the gated-grid path |
| `src/components/loyalty/accounts/loyalty-accounts-list-client.tsx` | List client |
| `src/components/loyalty/accounts/loyalty-accounts-list-client.source.test.ts` | Source guard: no `/builder`, no `data-explorer`, one-sentence empty state, `detailRoutePath` present |
| `src/components/loyalty/accounts/index.ts` | Barrel — exports only `LoyaltyAccountsListClient`, `AccountsSelectionModal` + types |

### Modified

| File | Change |
|------|--------|
| `src/services/loyalty/query-keys.ts` | Appended `accounts`, `points`, `eventable` key groups. Campaign/schema/PAT/dashboard keys untouched |
| `src/app/loyalty/accounts/page.tsx` | Replaced the Task 1 stub with the thin server host + client list |

### Not changed

- `src/lib/api-types.ts` — the brief permitted widening `SchemaListItem.attributes`
  to `SchemaAttribute[]` "if it unblocks tsc". It did not need to be widened: the
  spread in the list client typechecks as-is. Left alone (YAGNI, smaller blast radius).
- `src/services/loyalty/actions.ts` — Task 1's `queryData` already matches the
  object-call shape the lifted files use. No `audit` is passed from any list or
  query path.
- No campaigns files touched.

---

## Rewrite rules — compliance

- No `@exp/*`, `bffFetch`, `getSlug`, `PermissionGuard`, `PageWrapper`,
  `X-ELP-Audit`, `data-explorer`, or `moveTierViaJourneyFallback` anywhere in the
  new files (verified by `rg` across `dynamic-data/`, `accounts/`, `shared/`,
  the two new `ui/` files, `services/loyalty/`, `lib/loyalty-schema-types.ts`,
  and `app/loyalty/accounts/` — the only hits are inside the guard test's own
  assertion strings).
- No `audit` usage in any Task 2 file; campaign REST stays unaudited.
- Slug stays `"session"` (inherited from `TENANT_SLUG` in `actions.ts`).
- `LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME` imported from `@/services/loyalty/schema-names`.
- `queryData` object shape (`queryString` / `queryArgs` / `pageSize` /
  `continuationToken` / `sortBy` / `sortOrder`) preserved; paging reads
  `response.meta?.continuationToken`.
- Row navigation no longer defaults to `/loyalty/data-explorer/{schemaId}`. Rows
  are inert unless `detailRoutePath` is passed. The accounts list passes
  `detailRoutePath="/loyalty/accounts"`.
- Empty state is the single sentence with no Sparkles / Button / Link.
- No `/loyalty/accounts/builder` route created.
- No product tenant named in new code, copy, or tests. No auth/OIDC/JWT discussion
  in new comments. No secret, event-payload, or audit-JSON logging added.
- No existing comments deleted.

---

## TDD evidence

### RED

```
> vitest run src/services/loyalty/utils/account-identifiers.test.ts src/services/loyalty/utils/grid-columns.test.ts src/services/loyalty/eventable-schema.test.ts

⎯⎯⎯⎯⎯⎯ Failed Suites 3 ⎯⎯⎯⎯⎯⎯⎯

 FAIL  src/services/loyalty/eventable-schema.test.ts
Error: Cannot find module './eventable-schema' imported from '.../eventable-schema.test.ts'

 FAIL  src/services/loyalty/utils/account-identifiers.test.ts
Error: Cannot find module './account-identifiers' imported from '.../account-identifiers.test.ts'

 FAIL  src/services/loyalty/utils/grid-columns.test.ts
Error: Cannot find module './grid-columns' imported from '.../grid-columns.test.ts'

 Test Files  3 failed (3)
      Tests  no tests
```

Expected: the three test files were written before their modules existed, so
collection fails at import. This is the intended first failure — it proves the
tests are actually bound to the modules under test and not silently passing.

### GREEN

```
> vitest run src/services/loyalty/utils/account-identifiers.test.ts src/services/loyalty/utils/grid-columns.test.ts src/services/loyalty/eventable-schema.test.ts

 ✓ src/services/loyalty/utils/account-identifiers.test.ts (2 tests) 3ms
 ✓ src/services/loyalty/eventable-schema.test.ts (3 tests) 2ms
 ✓ src/services/loyalty/utils/grid-columns.test.ts (4 tests) 3ms

 Test Files  3 passed (3)
      Tests  9 passed (9)
```

### RED for the source-guard test

The guard asserts an *absence* (`not.toContain("/builder")`), so it passes the
moment the ported file is correct. To prove it is not vacuous I ran the exact
same assertions against the unported source in a throwaway spec
(`__red-guard-check.test.ts`, deleted afterwards):

```
 ❯ src/components/loyalty/accounts/__red-guard-check.test.ts (2 tests | 2 failed)
   × does not link to an account model builder
     → expected '"use client";\r\n\r\n/**\r\n * Loyalt…' not to contain '/builder'
   × states the missing-schema case in one sentence with no call to action
     → expected '"use client";\r\n\r\n/**\r\n * Loyalt…' to contain 'The LoyaltyAccountDetails schema is m…'

 Test Files  1 failed (1)
      Tests  2 failed (2)
```

The unported source carries `<Link href="/loyalty/accounts/builder">Open account
model builder</Link>` and the multi-paragraph "Use the model builder to create
it" copy. Both assertions catch exactly that. The temporary file was deleted
before the final run.

### Final verification

```
> vitest run

 Test Files  38 passed (38)
      Tests  196 passed (196)
```

```
> npx tsc --noEmit

src/auth.ts(47,40): error TS2345: Argument of type 'unknown' is not assignable to parameter of type 'string | undefined'.
src/components/loyalty/campaigns/campaigns-client.tsx(124,9): error TS2322: Type '(data: Campaign | CampaignListItem) => Promise<ApiResponse<CampaignListItem>>' is not assignable to type '(c: unknown) => Promise<unknown>'.
src/lib/loyalty-model.test.ts(40,12): error TS18048: 'model.schemaName' is possibly 'undefined'.
```

Exactly the three pre-existing errors the brief listed. Zero errors in Task 2
files. Not fixed, per the brief.

---

## Deliberate deviations from the brief

### 1. `dynamic-entity-details.tsx` deferred to Task 3 — the one real judgement call

The brief's copy-source list includes `entity-details`, but the same brief says
`collapsible` is Task 3, and `DynamicEntityDetails` cannot compile without it.
Its full dependency closure is four files that are **not** in the Task 2 file list:

- `@/components/ui/collapsible` — explicitly assigned to Task 3 (plan Task 3 file
  map and Step 3)
- `@/components/shared/friendly-data-view` — not mentioned in the plan or brief
  at all, and it *also* needs `collapsible`
- `@/services/loyalty/utils/display-labels` (`getSchemaDisplayName`) — not
  mentioned anywhere
- Journeys' `JsonTreeView` is a different, much smaller component than the one
  the lifted `FriendlyDataView` expects, so that port needs adaptation, not a copy

`DynamicEntityDetails` has no consumer in Task 2 — the accounts list uses
`DynamicDataTable` only. Task 3 ("Read-only account detail") is the first thing
that renders it, and Task 3 already owns `collapsible.tsx`.

I made the type work available now so Task 3 does not have to reopen it:
`SchemaAttribute` carries `attributeLayoutGroup`, `attributeLayoutPosition`,
`attributeLayoutHeight`, `isDetailsViewable`, and `modelId`, and
`loyalty-schema-types.ts` exports `CURATED_ATTRIBUTE_LAYOUT_GROUPS` — exactly as
Step 3 of the brief specified.

**Task 3 must add:** `dynamic-data/dynamic-entity-details.tsx` (+ its test),
`ui/collapsible.tsx`, `shared/friendly-data-view.tsx`, and
`services/loyalty/utils/display-labels.ts` (+ test), then add
`DynamicEntityDetails` to the `dynamic-data/index.ts` barrel.

I flagged this rather than silently expanding scope into a file the plan assigned
to another task. If you would rather Task 2 own the whole folder, say so and I
will pull those four files in.

### 2. `<Suspense>` around the list in `page.tsx`

`DynamicDataTable` calls `useSearchParams` (it reads `?account=` for the gated
path). Next's bundled docs
(`node_modules/next/dist/docs/01-app/03-api-reference/04-functions/use-search-params.md`,
line 181) state that a static page calling `useSearchParams` from a Client
Component **must** be wrapped in a Suspense boundary or `next build` fails with
"Missing Suspense boundary with useSearchParams". The brief's `page.tsx` snippet
has no boundary, so I added one with a `<Skeleton>` fallback. Everything else in
the snippet is as written. This only shows up in a production build, not in
`npm test` / `tsc`, which is why I am calling it out.

### 3. `DateRangeFilter` reimplemented (as the brief directed)

Two `<Input type="date">` fields plus a "Clear dates" button; same exported
`DateRangeFilterProps` and same ISO `fromDate`/`toDate` emission. The
`placeholder` prop is now used for the two `aria-label`s so it is not a dead
prop. The `date-time-range-picker` / `date-time-range` stack was not ported.

### 4. Row-click id is URL-encoded

The lifted `handleRowClick` interpolated the account id straight into the path.
I wrapped it in `encodeURIComponent`. This is a one-line correctness fix for ids
containing `#` or `?`, which `new URL()` does not escape on its own.

### 5. Two drops in `accounts-selection-modal.tsx`

- The fixed fallback column set had a `dealercompanyname` / "Account" column —
  a field name from the source product's own data model, not a Journeys concept.
  Removed; `customerid` and `region` remain.
- The `searchField` prop was already `@deprecated` and void-ed in the source.
  Removed it and its `void _searchField;` line rather than carry dead surface.

### 6. Identifier test description reworded

The brief said to copy the identifier tests "unchanged". I changed one
`it(...)` description from "used by EXP account pickers" to "used by the account
pickers". The assertions are byte-identical. Naming the source product inside
Journeys source felt wrong given the repo's neutrality rules. Trivial to revert
if you disagree.

---

## Self-review findings (and what I did about them)

- **Barrel cycle risk.** `dynamic-data-table.tsx` imports
  `AccountsSelectionModal`, and `loyalty-accounts-list-client.tsx` imports
  `DynamicDataTable`. I kept the modal import pointed at the concrete file
  (`@/components/loyalty/accounts/accounts-selection-modal`) rather than the
  `accounts/index.ts` barrel, so there is no barrel-to-barrel cycle.
- **`schema.name` is optional** on the new `LoyaltySchema` (per the plan), but
  the lifted table did `schema.name.toLowerCase()` and passed it where a
  `string` was required. Introduced `schemaName` / `schemaLabel` locals at the
  top of the component instead of sprinkling `?? ""` at each use.
- **`attribute.symbol` is optional** too. `filterableColumns` now filters on
  `Boolean(a.symbol)` before mapping, so a symbol-less attribute cannot produce
  an unkeyed filter column.
- **`.empty` class exists** — confirmed at `src/app/globals.css:33`, so the
  empty-state sentence is styled, not raw.
- **No new dependencies.** No `@tanstack/react-table`; the lifted `data-table.tsx`
  is the custom implementation. TanStack Query is already provided by
  `src/components/providers.tsx`.
- **`accounts/index.ts` scope.** Exports only what exists after this task. No
  detail/journey/tier re-exports (Tasks 3–5).
- **State-in-`queryFn`.** The lifted `DynamicDataTable` calls `setAllRows` /
  `setContinuationToken` inside its TanStack `queryFn`. This is an anti-pattern,
  but it is the source behaviour and rewriting the paging model is outside this
  task. Left as lifted; noting it as a known wart.

---

## Concerns

1. **`dynamic-entity-details.tsx` is not in this change set.** See deviation 1.
   Task 3 inherits four extra files. This is the item most worth a second
   opinion.
2. **Three pre-existing `tsc` errors remain** (`src/auth.ts`,
   `campaigns-client.tsx`, `loyalty-model.test.ts`), untouched per the brief.
   `npx tsc --noEmit` therefore still exits non-zero.
3. **No component-level tests.** `vitest.config.ts` sets `environment: "node"`
   and the app has no `jsdom` / `@testing-library/react` installed, so nothing
   renders `DynamicDataTable` or the list client in CI. Coverage for this slice is
   pure-unit (identifiers, grid columns, eventable filter) plus the source-text
   guard. The rendering paths are only exercised by the Task 6 browser pass.
4. **`next build` not run.** I verified `npm test` and `npx tsc --noEmit` as the
   brief asked. The Suspense boundary in deviation 2 is my reading of the bundled
   Next docs, not something I confirmed against a real build.
