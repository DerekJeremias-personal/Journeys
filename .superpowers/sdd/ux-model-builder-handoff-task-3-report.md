# Task 3 report: Backend.Model.UX tenant query + seed + first turn

**Status:** DONE_WITH_CONCERNS  
**Commits:** none

## TDD evidence

### RED (tests first, implementation absent)

Command:

```powershell
cd C:\Dev\Backend\Backend.Model.UX
npm test -- lib/journeys-seed.test.ts
```

Result: FAIL (expected)

- `lib/journeys-seed.test.ts` — `Cannot find module './journeys-seed'`
- Test Files 1 failed (1); Tests no tests

### GREEN (seed HTML + consume)

Same focused command after implementation.

First GREEN run failed one brief-literal assertion: `toContain("tenantId=acme")` cannot pass when the mandated script builds `?tenantId=` + `encodeURIComponent(p.tenantId)` (no interpolated `tenantQ`). Assertion updated to `tenantId=` + `encodeURIComponent(p.tenantId)`.

Result: PASS

```
✓ lib/journeys-seed.test.ts (5 tests)
Test Files  1 passed (1)
     Tests  5 passed (5)
```

### Full suites (once each)

```powershell
cd C:\Dev\Backend\Backend.Model.UX
npm test
```

Result: PASS — 2 files, 11 tests (`sse` 6 + `journeys-seed` 5).

```powershell
cd C:\Dev\Journeys\Journeys\Journeys.UX
npm test
```

Result: PASS — 49 files, 237 tests. Journeys.UX not modified.

## Files changed

| Path | Action |
|------|--------|
| `Backend.Model.UX/lib/journeys-seed.ts` | Created — `JOURNEYS_SEED_STORAGE_KEY`, `takeJourneysSeedBrief` (`globalThis.sessionStorage`), `writeJourneysSeedBriefScript` (`<` → `\u003c`) |
| `Backend.Model.UX/lib/journeys-seed.test.ts` | Created — redirect query + `</script>` escape + consume-once |
| `Backend.Model.UX/app/api/model-builder/seed/route.ts` | Created — form POST `tenantId`/`brief`/`source=journeys` → `text/html` handshake |
| `Backend.Model.UX/components/layout/tenant-context.tsx` | Modified — `?tenantId=` wins over localStorage; persist via `setTenantId` |
| `Backend.Model.UX/components/model-coach/model-coach-client.tsx` | Modified — `sendMessage`; first turn only when `source=journeys` |
| `Backend.Model.UX/lib/auth.config.ts` | Modified — allow unauthenticated `POST /api/model-builder/seed` so the handshake HTML can run before coach auth |

No Journeys.UX / API / Core / DAL. No capability ids. Never `modelId` `"unknown"`. Brief not in the query string and not logged. Unused `tenantQ` omitted.

## Concerns

1. Brief test expected literal `tenantId=acme`; mandated `location.replace` uses `p.tenantId` only. Test aligned to the script.
2. `auth.config.ts` was not in the brief file list. Without it, NextAuth redirects the Journeys form POST to `/signin` and the sessionStorage handshake never runs (unless `AUTH_DEV_BYPASS`).
3. Spec §7 tenant-win / inject-once coach behavior is implemented; no React/jsdom tests (Vitest is Node-only). Seed consume-once is covered.
4. `takeJourneysSeedBrief` uses `globalThis.sessionStorage` (brief interface + Node import) instead of a free `sessionStorage` identifier.

## Fix

Important review finding: first seeded coach send could use the SSR default tenant (`mericantires`) then never retry, because `takeJourneysSeedBrief` is destructive and `seededRef` blocked retry after `TenantProvider` applied `?tenantId=` on mount.

**Change:** do not consume the brief until `useTenant()` matches `?tenantId=` (when that query param is present). `source=journeys` remains the only inject trigger. `seededRef` + consume-once still apply only after a successful take. TenantProvider still applies `?tenantId=` on mount only — no `window` lazy-init during render. `auth.config.ts` seed allowlist left in place.

### Files changed

| Path | Action |
|------|--------|
| `Backend.Model.UX/lib/journeys-seed.ts` | Added `shouldSendJourneysSeed` — false unless `source=journeys`; false while query tenant is present and context tenant differs |
| `Backend.Model.UX/lib/journeys-seed.test.ts` | Added four Node-only cases for that gate |
| `Backend.Model.UX/components/model-coach/model-coach-client.tsx` | Seed effect reads `source` + `tenantId` from `window.location.search`, gates with `shouldSendJourneysSeed`, then take + `seededRef` + `sendMessage`; deps `[sendMessage, tenantId]` |

Unchanged: `components/layout/tenant-context.tsx` (on-mount `?tenantId=` apply kept), `lib/auth.config.ts` (seed allowlist kept).

### Covering tests

- `shouldSendJourneysSeed` — source not journeys → false
- `shouldSendJourneysSeed` — query tenant present and context tenant differs → false
- `shouldSendJourneysSeed` — query tenant present and context tenant matches → true
- `shouldSendJourneysSeed` — no query tenant, source=journeys → true

### Exact command and output (focused)

```powershell
cd C:\Dev\Backend\Backend.Model.UX
npm test -- lib/journeys-seed.test.ts
```

```
> backend-model-ux@0.1.0 test
> vitest run lib/journeys-seed.test.ts


 RUN  v3.2.7 C:/Dev/Backend/Backend.Model.UX

 ✓ lib/journeys-seed.test.ts (9 tests) 4ms

 Test Files  1 passed (1)
      Tests  9 passed (9)
   Start at  18:05:57
   Duration  383ms (transform 35ms, setup 0ms, collect 40ms, tests 4ms, environment 0ms, prepare 141ms)
```

### Exact command and output (full suite)

```powershell
cd C:\Dev\Backend\Backend.Model.UX
npm test
```

```
> backend-model-ux@0.1.0 test
> vitest run


 RUN  v3.2.7 C:/Dev/Backend/Backend.Model.UX

 ✓ lib/sse.test.ts (6 tests) 3ms
 ✓ lib/journeys-seed.test.ts (9 tests) 3ms

 Test Files  2 passed (2)
      Tests  15 passed (15)
   Start at  18:06:00
   Duration  415ms (transform 50ms, setup 0ms, collect 78ms, tests 6ms, environment 0ms, prepare 230ms)
```

**Commits:** none
