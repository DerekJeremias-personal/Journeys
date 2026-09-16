# Task 8 Report: Verify impact scripts + local smoke

**Date:** 2026-09-14  
**Workspace:** `C:\Dev\Journeys\Journeys`

## Status

**DONE_WITH_CONCERNS** — controller ran Steps 1–3 after the Task 8 implementer was Shell-blocked. Step 4 remains human-owned.

## Step 1: docs-impact + graph-impact

Controller ran from `C:\Dev\Journeys\Journeys` with the brief file list.

| Script | Exit code | Output |
|--------|-----------|--------|
| `docs-impact.ps1` | 0 | `docs-impact: OK (2 files)` |
| `graph-impact.ps1` | 0 | `graph-impact: OK nodes=campaigns productUpdate=True waiver=False` |

## Step 2: dotnet build

| Command | Exit code | Notes |
|---------|-----------|--------|
| `dotnet build .\Journeys.sln` | 1 | File-lock only: running `Journeys.API` (pid 47972) + Visual Studio locked API `bin` DLLs. MSB3027/MSB3021. No CS errors. |
| `dotnet build .\Journeys.sln -o $env:TEMP\journeys-ux-verify-build` | 0 | **0 Error(s)**. UX is not in the sln. |

## Step 3: npm test

| Command | Exit code | Output |
|---------|-----------|--------|
| `cd Journeys.UX; npm test` | 0 | 21/21 pass (5 files) |

## Step 4: Manual human smoke

**Human-owned; not executed by agent.**

1. API up (typical `https://localhost:7001`).
2. Copy `Journeys.UX/.env.example` → `.env.local`; set `JOURNEYS_UX_ALLOW_API_KEY_LOGIN=true` and tenant/key.
3. `npm run dev` in `Journeys.UX` (port 3000).
4. Sign in, then Overview, Campaigns, Accounts.

## Step 5

No commit (per instructions).

---

## Whole-branch review fixes (2026-09-14)

**Status:** DONE (Shell blocked — tests/tsc not re-run)

### Fix 1 — Accounts hides API failures

- `getSchemaByName` now returns `ApiResponse<SchemaListItem | null>`; fetch failures propagate instead of collapsing to `null`.
- Accounts page shows `formatFetchError` on schema-list failure (same auth-aware copy as Campaigns); missing-schema empty state only when the list call succeeded and `pickLiveSchema` returned null.
- `queryData` error handling unchanged.

### Fix 2 — stale taxonomy

- `docs/product/taxonomies/constraints.md` row `no-ui-in-sln` updated to reflect `Journeys.UX` as HTTP client (not a csproj in `Journeys.sln`).

### Files changed

- `Journeys.UX/src/services/loyalty/actions.ts`
- `Journeys.UX/src/app/loyalty/accounts/page.tsx`
- `docs/product/taxonomies/constraints.md`

### Verify

Shell blocked in agent session; prior Task 8 run: npm test 21/21 pass. Re-run locally: `cd Journeys.UX; npm test; npx tsc --noEmit`.

### Concerns

None for the two fixes. No commit per instructions.
