# Task 3 report: Read-only account detail

**Status:** DONE_WITH_CONCERNS
**Commits:** none (working tree left dirty, as instructed)
**Work dir:** `C:\Dev\Journeys\Journeys\Journeys.UX`

---

## 1. What I implemented

### Route + composition

- `src/app/loyalty/accounts/[id]/page.tsx` — server component. Resolves account identifiers in the
  EXP order (`queryData` with `LOYALTY_ACCOUNT_IDENTIFIER_QUERY` → `GET accounts/session/{id}` →
  `GET accounts/session/ext/{id}`), renders a `← Accounts` text link, an `<h1>Loyalty account</h1>`,
  the resolved external reference as a sub-line, and `<LoyaltyAccountDetailClient accountId={id} />`.
  No `PageWrapper`, no `PermissionGuard`, no Actions dropdown, no builder/data-explorer links.
- `src/components/loyalty/accounts/loyalty-account-detail-client.tsx` — account summary card + point
  balances card in a 2-column grid, then `<DynamicEntityDetails showTitle={false}>` with
  `<AccountCampaignProgress>` + `<EventableModelsSection>` in the footer slot. Missing/not-Live
  schema renders the agreed one-sentence copy; missing entity renders an "Account not found" alert.

### Read actions (`src/services/loyalty/actions.ts`)

Four GETs, none of which pass `audit`:

- `getLoyaltyAccountById(id)`
- `getLoyaltyAccountByExternalId(extId)`
- `getAccountPointBalances(loyaltyAccountId)` — normalized with `extractEntities`
- `getAccountPoints(loyaltyAccountId)` — normalized with `extractEntities`

### Components / helpers created

| File | Notes |
|------|-------|
| `src/components/loyalty/accounts/account-tier.ts` + `.test.ts` | Copied from EXP, imports only fixed |
| `src/components/loyalty/accounts/account-campaign-progress.tsx` | Copied; `loyaltyKeys.points.byAccount` → `ledgersByAccount`; point-account-type names read defensively off `PointAccountTypeListItem`; "No campaigns for this dealer" → "No campaigns for this account." |
| `src/components/loyalty/accounts/eventable-models-section.tsx` | Copied; Data Explorer `<Link>` block and the file-header sentence about it **deleted**; `getAllSchemas()` (no args) + `isEventableSchema` from `@/services/loyalty/eventable-schema`; `schema.name` guarded by a `NamedSchema` type predicate so `queryData` never receives an empty/unknown model id |
| `src/components/loyalty/accounts/eventable-models-section.source.test.ts` | Source guard: no `data-explorer`, no `builder`, no `queryAdminData`, no `"unknown"` |
| `src/components/loyalty/dynamic-data/dynamic-entity-details.tsx` | Copied; `@exp/shared-types` → `@/lib/loyalty-schema-types`; `LoyaltySchemaField` → `SchemaAttribute` |
| `src/components/shared/friendly-data-view.tsx` | Copied (see deviations) |
| `src/components/ui/collapsible.tsx` | `import { Collapsible as CollapsiblePrimitive } from "radix-ui"` + `import * as React`, matching `dialog.tsx` |
| `src/services/loyalty/utils/display-labels.ts` + `.test.ts` | Copied; imports fixed |
| `src/components/loyalty/points/points-accounts-card.tsx` + `index.ts` | Copied **without** Manage; `PermissionGuard` stripped |

### Files modified

- `src/services/loyalty/actions.ts` — four read actions + `AccountPointBalance` import
- `src/components/loyalty/accounts/index.ts` — appended `LoyaltyAccountDetailClient`,
  `AccountCampaignProgress`, `EventableModelsSection` (+ prop types)
- `src/components/loyalty/dynamic-data/index.ts` — exports `DynamicEntityDetails`
- `src/lib/loyalty-schema-types.ts` — added `displayName?: string` and `modelVersion?: string` to
  `LoyaltySchema` (see §4)
- `src/services/loyalty/parse-list.ts` + `.test.ts` — `normalizeCampaignRow` now preserves the
  campaign `journey` tree (see §4 — **the most important thing for a reviewer to look at**)

`loyalty-account-info-card.tsx` was **skipped**: the EXP detail client does not import it, matching
the brief's "only if still imported" condition.

---

## 2. What I tested and results

- Focused tests during iteration (`account-tier`, `display-labels`, `eventable-models-section.source`,
  `parse-list`) — all pass.
- **Full suite:** `npm test` → **41 files, 207 tests, all passing.**
- **`npx tsc --noEmit`** → exit 1 with exactly the three pre-existing errors
  (`src/auth.ts`, `src/components/loyalty/campaigns/campaigns-client.tsx`,
  `src/lib/loyalty-model.test.ts`). Baseline captured before any edit was byte-identical. **No new
  errors in Task 3 files.**
- **Lint (IDE diagnostics)** across all new/changed paths: clean.
- **Greps** over `src/components/loyalty/{accounts,points,dynamic-data}` and
  `src/app/loyalty/accounts`: zero production-source matches for `data-explorer`, `builder`,
  `@exp/`, `bffFetch`, `getSlug`, `PermissionGuard`, `PageWrapper`, `X-ELP-Audit`, `queryAdminData`,
  `moveTierViaJourneyFallback`, `expirationDate`, or a product tenant name. The only hits are the
  assertion strings inside the two source-guard test files.

---

## 3. TDD evidence

### Cycle 1 — `account-tier`

**RED** (`npm test -- src/components/loyalty/accounts/account-tier.test.ts`):

```
 FAIL  src/components/loyalty/accounts/account-tier.test.ts
Error: Cannot find module './account-tier' imported from
'.../src/components/loyalty/accounts/account-tier.test.ts'
 Test Files  1 failed (1)
      Tests  no tests
```

**GREEN** (after creating `account-tier.ts`):

```
 ✓ src/components/loyalty/accounts/account-tier.test.ts (2 tests) 2ms
 Test Files  1 passed (1)
      Tests  2 passed (2)
```

### Cycle 2 — `display-labels`

**RED** (`npm test -- src/services/loyalty/utils/display-labels.test.ts`):

```
 FAIL  src/services/loyalty/utils/display-labels.test.ts
Error: Cannot find module './display-labels' imported from
'.../src/services/loyalty/utils/display-labels.test.ts'
 Test Files  1 failed (1)
      Tests  no tests
```

**GREEN** (after creating `display-labels.ts` + the `LoyaltySchema.displayName` field):

```
 ✓ src/services/loyalty/utils/display-labels.test.ts (3 tests) 2ms
 Test Files  1 passed (1)
      Tests  3 passed (3)
```

### Cycle 3 — eventable source guard

**RED** (`npm test -- src/components/loyalty/accounts/eventable-models-section.source.test.ts`):

```
 FAIL  src/components/loyalty/accounts/eventable-models-section.source.test.ts
Error: ENOENT: no such file or directory, open
'...\src\components\loyalty\accounts\eventable-models-section.tsx'
 Test Files  1 failed (1)
      Tests  no tests
```

**GREEN** (after creating the section with the Data Explorer link removed):

```
 ✓ src/components/loyalty/accounts/eventable-models-section.source.test.ts (4 tests) 2ms
 Test Files  1 passed (1)
      Tests  4 passed (4)
```

### Cycle 4 — campaign journey preservation

**RED** (`npm test -- src/services/loyalty/parse-list.test.ts`):

```
 × normalizeCampaignRow > keeps the journey tree so account tier progress can read it 4ms
   → expected undefined to deeply equal { id: 'journey-1', …(2) }
 × normalizeCampaignRow > reads a PascalCase journey and omits a non-object one 0ms
   → expected undefined to deeply equal { id: 'journey-1' }
 Tests  2 failed | 14 passed (16)
```

**GREEN** (after adding `journey: readRecord(rec, "journey", "Journey")`):

```
 ✓ src/services/loyalty/parse-list.test.ts (16 tests) 4ms
 Test Files  1 passed (1)
      Tests  16 passed (16)
```

---

## 4. Deviations from the brief (all deliberate)

### 4a. `normalizeCampaignRow` now preserves `journey` — please review this one

`AccountCampaignProgress` and `resolveCurrentTierLabel` both match an account's enrolled journeys
against `campaign.journey`. In Journeys.UX, `getCampaigns()` runs every row through
`normalizeCampaignRow`, which picked six scalar fields and **dropped the journey tree**. Left alone,
campaign progress would have silently rendered "No campaigns for this account." forever — a dead
section, not a visible failure.

The fix is one additive field, and `CampaignListItem.journey?: Record<string, unknown>` was already
declared in `api-types.ts`, so this populates a field the contract already advertised.
`mergeCampaignLists` keys on `id:status` only, so dedupe is unaffected, and no existing test asserted
the exact row shape.

**The trade-off a reviewer should weigh:** `/loyalty/campaigns` also calls `getCampaigns()`, so the
campaigns list now ships journey trees for up to 100 campaigns from the Next server to the browser.
The Journeys.API → Next hop is unchanged (the API always returned them; we were discarding them
after the fact). If that payload growth is unacceptable, the alternative is a dedicated
`getCampaignJourneys()` action plus a distinct query key, which costs a small refactor of the
private three-page fetch in `actions.ts` and a duplicated merge. I chose the one-line version to
keep the blast radius inside Task 3, but I would not object to switching.

### 4b. `LoyaltySchema` gained `displayName?: string` and `modelVersion?: string`

`loyalty-schema-types.ts` is a Task 2 file. Its `[key: string]: unknown` index signature types both
of these as `unknown`, which `getSchemaDisplayName` (needs `string | undefined`) and the
`DynamicEntityDetails` "Model information" block (renders `modelVersion` into JSX) cannot consume.
Two optional string fields is the smallest correct change. **Note:** `normalizeSchema` does not copy
either field off the API payload today, so `modelVersion` renders "—" and the model title falls back
to `schema.name`. That is cosmetic and I left `normalizeSchema` alone.

### 4c. `friendly-data-view.tsx` — dropped `DataView`, unexported `formatFieldLabel`

EXP's file exports `FriendlyDataView`, `DataView`, and `formatFieldLabel`. Only `FriendlyDataView` is
imported anywhere in this task, so I dropped `DataView` (a raw-JSON collapsible wrapper) rather than
ship a dead component, and kept `formatFieldLabel` file-local. Both are trivially restorable.

### 4d. `PointsAccountsCard` props narrowed

Since Manage is out of scope for Task 3, I dropped the `loyaltyAccountId` and `onUpdated` props —
they existed solely to feed `PointAccountManageModal`. **Task 4 will need to re-add both** when it
wires Manage; its plan step already says "Wire Manage on `PointsAccountsCard`", so this should be
expected rather than surprising.

### 4e. `DynamicEntityDetails` title is an `<h2>`, not an `<h1>`

The page owns the `<h1>`. The account detail passes `showTitle={false}`, so this only matters for
future callers. I also lowercased "Details" to "details" to match the repo's sentence-case headings.

---

## 5. Self-review findings (fixed before reporting)

- **Guarded `schema.name` properly.** First pass filtered eventable schemas and then non-null
  asserted `schema.name!` at the `queryData` call. Replaced with a `NamedSchema` type predicate in
  the filter chain, so the panel component's prop type makes an empty model id unrepresentable
  rather than merely unlikely.
- **Split the detail client's "no schema" and "no entity" branches.** EXP collapsed both into
  "Account not found", which would have blamed the account for a missing schema. Missing schema now
  renders the same one-sentence copy the list uses.
- **Removed the EXP header comment** in `eventable-models-section.tsx` that said row clicks navigate
  to `/loyalty/data-explorer/...`, not just the `<Link>` — the source guard test would have caught
  the comment, which is why I wrote the test before the component.
- **Replaced "No campaigns for this dealer"** with "No campaigns for this account." — EXP's wording
  carried a domain term that is not Journeys vocabulary.
- **Point-account-type lookups read defensively.** `PointAccountTypeListItem` is
  `Record<string, unknown>` in Journeys (EXP had a typed `PointAccountType`), so `id`/`name` are
  narrowed with `typeof … === "string"` instead of cast.
- **Extracted a `LabelledValue` helper** in `dynamic-entity-details.tsx`; EXP repeated the same
  label + wrapper div four times.

---

## 6. Concerns

1. **`normalizeCampaignRow` payload growth** — §4a. The one item I would most like a second opinion
   on.
2. **Three pre-existing tsc errors remain** (`src/auth.ts`, `campaigns-client.tsx`,
   `loyalty-model.test.ts`), untouched per the brief. This is why the status is DONE_WITH_CONCERNS.
3. **No render tests.** Vitest runs with `environment: "node"` and the project has neither `jsdom`
   nor `@testing-library/react`, so EXP's `dynamic-entity-details.test.tsx` and
   `points-accounts-card.test.tsx` could not be ported. Coverage for the new components is limited
   to the source-text guard plus the two pure-helper suites, matching the Task 2 precedent. Adding
   the DOM test stack is a larger decision than this task should make unilaterally.
4. **Duplicated identifier resolution.** The server page resolves identifiers (up to three calls),
   and the client then resolves them again through its own query chain. Today the server result only
   feeds a heading sub-line, so it is nearly pure overhead; Task 5 consumes it for the Actions
   dropdown. I kept it because the brief prescribed the EXP order explicitly, but if Task 5 slips,
   this is worth trimming.
5. **`getAccountPointBalances` / `getAccountPoints` assume an array or a wrapped `entities`/`items`
   envelope.** `extractEntities` returns `[]` for any other object shape, which would surface as an
   empty card rather than an error. I have not seen a live payload from these two endpoints.
6. **Docs and graph impact not run.** `docs-impact.ps1` / `graph-impact.ps1` and the browser pass are
   Task 6 per the plan's out-of-scope list.
