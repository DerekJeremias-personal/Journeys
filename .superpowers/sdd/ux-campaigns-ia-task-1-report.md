# Task 1 Report: Neutral Tailwind + allowlist + campaign actions

## What I implemented

- Added Tailwind v4 and PostCSS with a neutral oklch theme and system font stack.
- Added the approved TanStack Query, toast, icon, class utility, Radix, and Tailwind dependencies.
- Added `cn`, the seven requested EXP UI primitives, and a root client provider for Query Client and Toaster.
- Wrapped the app in the provider and restyled the Loyalty shell/nav with neutral Tailwind zinc utilities (`w-60 bg-zinc-100`).
- Preserved the existing `.agent-chat` and legacy content CSS needed by pages that have not migrated yet.
- Expanded the fail-closed Loyalty path mapping for Campaign getmany/save/validate/get/delete/copy/restore/versions/archive/live/draft/PAT and Campaign Agent conversations.
- Kept stats and point-account-type upsert forbidden.
- Added encoded `searchParams` support to `journeysFetch`, skipping empty and undefined values without sending `Cookie` or adding Campaign REST audit headers.
- Expanded Campaign API types and added the requested Campaign server actions, retaining the existing Accounts/schema/data actions.
- Kept action tenant slug `"session"` and left tenant resolution in `journeysFetch`.
- Kept `copyCampaign` as a POST to the copy endpoint without client-side id stripping.

## What I tested and results

- Focused allowlist RED/GREEN cycle: passed after implementation.
- Focused `journeysFetch` query-string RED/GREEN cycle: passed after implementation.
- Full UX suite: `npm test` passed, 17 files and 75 tests.
- IDE lint diagnostics on all Task 1 source files: no errors.
- `git diff --check -- Journeys/Journeys.UX`: passed.
- `scripts/docs-impact.ps1` with the Task 1 file list: passed (`docs-impact: OK (19 files)`).
- `scripts/graph-impact.ps1` with the Task 1 file list: failed because `campaigns` and `campaign-agent` require the product docs/waiver scheduled for Task 8.
- Supplemental `npx tsc --noEmit`: failed on three pre-existing, out-of-scope diagnostics in `src/auth.ts`, `src/lib/campaign-agent/stream-route.ts`, and `src/lib/loyalty-model.test.ts`; no Task 1 file was named.
- `npm audit --json`: reports 2 moderate and 2 critical vulnerabilities through the unchanged `next-auth` and `vitest` versions.

## TDD Evidence

### Allowlist RED

Command:

`npx vitest run src/lib/map-loyalty-path.test.ts`

Result: exit 1, 4 failed / 6 passed. Expected failures were `not-allowlisted` for `campaigns/session/getmany`, campaign get-by-id, versions, and campaign-agent conversations.

### Allowlist GREEN

Command:

`npx vitest run src/lib/map-loyalty-path.test.ts`

Result: exit 0, 10 passed.

### Search parameters RED

Command:

`npx vitest run src/lib/journeys-fetch.test.ts`

Result: exit 1, 1 failed / 5 passed. Expected URL included `?campaignStatus=draft+version`; received URL had no query.

### Search parameters GREEN

Command:

`npx vitest run src/lib/journeys-fetch.test.ts src/lib/map-loyalty-path.test.ts`

Result: exit 0, 2 files and 16 tests passed.

### Full suite

Command:

`npm test`

Result: exit 0, 17 files and 75 tests passed.

## Files changed

- `Journeys.UX/package.json`
- `Journeys.UX/package-lock.json`
- `Journeys.UX/postcss.config.mjs`
- `Journeys.UX/src/app/globals.css`
- `Journeys.UX/src/app/layout.tsx`
- `Journeys.UX/src/app/loyalty/layout.tsx`
- `Journeys.UX/src/components/loyalty-nav.tsx`
- `Journeys.UX/src/components/providers.tsx`
- `Journeys.UX/src/components/ui/alert-dialog.tsx`
- `Journeys.UX/src/components/ui/alert.tsx`
- `Journeys.UX/src/components/ui/badge.tsx`
- `Journeys.UX/src/components/ui/button.tsx`
- `Journeys.UX/src/components/ui/card.tsx`
- `Journeys.UX/src/components/ui/dropdown-menu.tsx`
- `Journeys.UX/src/components/ui/skeleton.tsx`
- `Journeys.UX/src/lib/utils.ts`
- `Journeys.UX/src/lib/map-loyalty-path.ts`
- `Journeys.UX/src/lib/map-loyalty-path.test.ts`
- `Journeys.UX/src/lib/journeys-fetch.ts`
- `Journeys.UX/src/lib/journeys-fetch.test.ts`
- `Journeys.UX/src/lib/api-types.ts`
- `Journeys.UX/src/services/loyalty/actions.ts`

## Self-review findings

- Completeness: all Task 1 files and interfaces from the brief are present; existing schema and Accounts-related actions remain intact.
- Quality: paths remain fail-closed and are ordered with literal suffixes before generic campaign ids; path components and query values are encoded.
- Security: no `Cookie`, `X-Journeys-Audit`, EXP imports, Prisma, charts, brand tokens, or sidebar tokens were added.
- YAGNI: no kebab, builder, wizard, C# copy/restore, or SSE behavior was implemented.
- TDD: required allowlist behavior and added query-string behavior were observed failing before implementation and passing afterward.

## Concerns

- Graph impact remains intentionally pending because product docs/graph synchronization is explicitly Task 8; Task 1 alone therefore does not satisfy the repository-wide graph gate.
- The repository-wide TypeScript check has three out-of-scope existing diagnostics listed above, although the required full Vitest suite passes.
- The current unchanged `next-auth` and `vitest` versions have published vulnerabilities; updating them was outside this task.
- No commit, push, merge, or PR was performed.
