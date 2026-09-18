# Task 6 report: Docs + graph + browser pass

**Status:** DONE_WITH_CONCERNS (all gates green; data-dependent browser flows unverifiable — see Concerns)

Plan: `Journeys/docs/plans/2026-09-18-Journeys-ux-accounts-detail.md` (Task 6)
Spec: `Journeys/docs/specs/2026-09-18-Journeys-ux-accounts-detail-design.md` §10

## Files changed

| File | Change |
|------|--------|
| `Journeys/docs/product/graph/path-map.yaml` | `Journeys.UX` nodes → `[campaigns, journeys, campaign-agent, event-models, outcomes]`. No `IMPLEMENTED_AS proj-ux` added. |
| `Journeys/scripts/path-docs-map.yaml` | Appended `docs/product/ontology/loyalty-account.md` to the existing `Journeys.UX` docs list (nothing removed). |
| `Journeys/docs/developer/journeys-ux.md` | Added `## Accounts` section (verbatim from the brief) after the Run/model table; screens line now `Overview, Accounts detail, Campaigns IA.`; accounts spec appended to the **Specs:** line. Campaigns/Agent sections untouched. |
| `Journeys/Journeys.UX/src/app/loyalty/accounts/[id]/page.tsx` | **Defect fix** — `notFound()` for reserved route ids (see Browser step 5). |
| `Journeys/Journeys.UX/src/services/loyalty/utils/reserved-account-route-ids.ts` | New: `isReservedAccountRouteId` (reserves `builder`, case-insensitive). |
| `Journeys/Journeys.UX/src/services/loyalty/utils/reserved-account-route-ids.test.ts` | New: 2 tests (written red first, then green). |

No named-tenant examples added. No auth discussion in the new Accounts section.

## Step 4 — Impact scripts (from `C:\Dev\Journeys\Journeys`)

Run 1 (brief's file list):

```
docs-impact: OK (3 files)
DOCS_IMPACT_EXIT=0
graph-impact: OK nodes=campaigns, journeys, campaign-agent, event-models, outcomes productUpdate=True waiver=False
GRAPH_IMPACT_EXIT=0
```

Run 2 (re-run after the `[id]/page.tsx` fix, with the two new UX source files added to the list):

```
docs-impact: OK (5 files)
DOCS_IMPACT_EXIT=0
graph-impact: OK nodes=campaigns, journeys, campaign-agent, event-models, outcomes productUpdate=True waiver=False
GRAPH_IMPACT_EXIT=0
```

No waiver needed.

## Step 5 — Tests

`cd Journeys.UX; npm test` → **45 files, 219 tests passed, 0 failed** (exit 0). Was 44/217 before this task; +1 file / +2 tests from the reserved-route-id guard.

`npx tsc --noEmit` → exit 2 with exactly the three pre-existing allowed errors, unchanged:

```
src/auth.ts(47,40): TS2345
src/components/loyalty/campaigns/campaigns-client.tsx(124,9): TS2322
src/lib/loyalty-model.test.ts(40,12): TS18048
```

## Step 6 — Browser pass

Dev server on `http://localhost:3000` (already running); `Journeys.API` port 7001 confirmed listening. The browser session was already signed in, so **sign-in did not block this pass** — no credentials were read, entered, or printed.

Verified:

1. **`/loyalty/accounts`** — renders the missing-schema empty state exactly: *The LoyaltyAccountDetails schema is missing or not Live.* No builder CTA, no Sparkles/Button/Link. Loyalty nav renders with the expected items disabled.
2. **`/loyalty/accounts/[id]`** (`acc-smoke-1`) — detail shell renders: `← Accounts` back link, `Loyalty account` heading, account label, Actions button, card skeletons. No crash on an unresolvable id (falls back to the route param as both identifiers). No Data Explorer links on the page.
3. **Actions dropdown** — opens with exactly `Assign journey`, `Remove journey`, `Manage tier`.
4. **`/loyalty/accounts/builder`** — **now 404** (`404: This page could not be found.`). See defect below.

Not verified (blocked by data, not by sign-in): list rows + search + row click-through, real summary/balances/schema-fields/campaign-progress/eventable content, deposit / spend / expire confirm copy and balance refetch, journey assign/remove against a real campaign+journey, tier preview + commit. All of these need a Live `LoyaltyAccountDetails` schema and at least one account in the tenant catalog; this environment has neither, so the UX correctly shows the missing-schema sentence everywhere. No tenant names are recorded here.

### Defect found and fixed during the browser pass

`/loyalty/accounts/builder` did **not** 404 on first check — the `[id]` dynamic segment swallowed `builder` and rendered a detail page with `accountId="builder"` (header, Actions dropdown, and all). That violates the plan's Step 6.5 acceptance criterion and the global constraint "Do not create `/loyalty/accounts/builder`". The `mapLoyaltyPath` allowlist already rejects `accounts/session/builder`, so the fetch layer was safe, but the route was reachable.

Fix (TDD: red → green): `isReservedAccountRouteId` in a new `reserved-account-route-ids.ts`, called from `[id]/page.tsx` before identifier resolution, which calls `notFound()`. Re-verified in the browser: `builder` 404s, and `acc-smoke-1` still renders normally.

## Concerns

- The operator write flows (points, journey, tier) have **never been exercised against live data** — unit tests and a rendering pass only. A human smoke test on a tenant with a Live `LoyaltyAccountDetails` schema and a real account is still owed before this is considered proven.
- The three pre-existing `tsc` errors remain out of scope for this task but are still outstanding in the branch.

## Commits

**None.** Nothing staged, nothing committed, nothing pushed. `HEAD` unchanged at `df022fb stage 1`.

## Whole-branch review fix pass

**Status:** DONE_WITH_CONCERNS. Move Tier now requires a successful, non-blocking preview; identifier query misses fall through to account GETs; unresolved accounts no longer render Actions.

**Tests:** `npm test` passed (47 files, 229 tests). `npx tsc --noEmit` reported only the three approved pre-existing errors in `auth.ts`, `campaigns-client.tsx`, and `loyalty-model.test.ts`. `docs-impact` and `graph-impact` passed (the latter with the increment's existing `docs/product/graph/path-map.yaml` update included).

**Files changed:** `manage-tier-modal.tsx`, `can-submit-tier-move.ts` + test, `loyalty-account-detail-client.tsx`, `[id]/page.tsx`, `account-identifier-resolution.ts` + test.

**Remaining concerns:** Live-data operator flows remain unverified, as recorded above. No commits, pushes, or PR changes were made.
