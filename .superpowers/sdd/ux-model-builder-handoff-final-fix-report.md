# Final-review fix report — Model Builder handoff

**Status:** DONE  
**Commits:** none

## What changed

Critical CSRF-style seed POST: Origin allowlist via `MODEL_BUILDER_SEED_ORIGINS` (empty allowlist or missing/mismatched Origin → 403). Brief over 32000 characters → 400. Auth seed allowlist in `lib/auth.config.ts` left in place.

Important: `shouldSendJourneysSeed` trims `queryTenantId` before compare. Spec helpers: `modelBuilderNavHref`, `readTenantIdFromSearch`. Seed route handler is importable in Vitest (no Next harness).

`?tenantId=` localStorage persist via `setTenantId` is unchanged. No Journeys.API/Core/DAL. No capability ids.

## Files changed

| Path | Action |
|------|--------|
| `Backend.Model.UX/lib/journeys-seed.ts` | Added `isAllowedSeedOrigin`, `isSeedBriefTooLarge`, `parseAllowedSeedOrigins`, `readTenantIdFromSearch`; trim `queryTenantId` in `shouldSendJourneysSeed` |
| `Backend.Model.UX/lib/journeys-seed.test.ts` | Origin / brief-size / tenant-search / padded-query-tenant tests |
| `Backend.Model.UX/app/api/model-builder/seed/route.ts` | Origin 403 + oversize brief 400 before handshake HTML |
| `Backend.Model.UX/app/api/model-builder/seed/route.test.ts` | Created — missing Origin 403; wrong source / empty brief / oversize 400 |
| `Backend.Model.UX/components/layout/tenant-context.tsx` | Uses `readTenantIdFromSearch`; persist path unchanged |
| `Backend.Model.UX/.env.example` | Documented `MODEL_BUILDER_SEED_ORIGINS` |
| `Journeys.UX/src/lib/model-builder-handoff.ts` | Added `modelBuilderNavHref` |
| `Journeys.UX/src/lib/model-builder-handoff.test.ts` | URL set → `/loyalty/models/handoff`; unset → `null` |
| `Journeys.UX/src/components/loyalty-nav.tsx` | Href vs disabled span from `modelBuilderNavHref` |
| `Journeys.UX/.env.example` | Note that Model.UX must allow this UX origin |

Unchanged: `Backend.Model.UX/lib/auth.config.ts` (`/api/model-builder/seed` still authorized without a session).

## Covering tests

- `isAllowedSeedOrigin`: empty allowlist → false; mismatch → false; trailing-slash strip → true
- `isSeedBriefTooLarge`: 32000 → false; 32001 → true
- `shouldSendJourneysSeed`: padded query tenant vs trimmed context tenant → true
- `readTenantIdFromSearch`: `?tenantId=acme` present; empty/missing → null
- `modelBuilderNavHref`: set → handoff href; unset → null (disabled span)
- Seed `POST`: missing Origin 403; wrong source / empty brief / oversize 400 — route imported in Node Vitest

## Commands and exact output

### `C:\Dev\Backend\Backend.Model.UX` — focused

```powershell
cd C:\Dev\Backend\Backend.Model.UX
npm test -- lib/journeys-seed.test.ts
```

```
> backend-model-ux@0.1.0 test
> vitest run lib/journeys-seed.test.ts


 RUN  v3.2.7 C:/Dev/Backend/Backend.Model.UX

 ✓ lib/journeys-seed.test.ts (16 tests) 8ms

 Test Files  1 passed (1)
      Tests  16 passed (16)
   Start at  18:13:04
   Duration  570ms (transform 76ms, setup 0ms, collect 82ms, tests 8ms, environment 0ms, prepare 164ms)
```

### `C:\Dev\Backend\Backend.Model.UX` — full

```powershell
cd C:\Dev\Backend\Backend.Model.UX
npm test
```

```
> backend-model-ux@0.1.0 test
> vitest run


 RUN  v3.2.7 C:/Dev/Backend/Backend.Model.UX

 ✓ lib/journeys-seed.test.ts (16 tests) 8ms
 ✓ lib/sse.test.ts (6 tests) 6ms
 ✓ app/api/model-builder/seed/route.test.ts (4 tests) 18ms

 Test Files  3 passed (3)
      Tests  26 passed (26)
   Start at  18:13:07
   Duration  800ms (transform 234ms, setup 0ms, collect 413ms, tests 32ms, environment 1ms, prepare 599ms)
```

### `C:\Dev\Journeys\Journeys\Journeys.UX` — focused

```powershell
cd C:\Dev\Journeys\Journeys\Journeys.UX
npm test -- src/lib/model-builder-handoff.test.ts
```

```
> journeys-ux@0.0.0 test
> vitest run src/lib/model-builder-handoff.test.ts


 RUN  v3.2.7 C:/Dev/Journeys/Journeys/Journeys.UX

 ✓ src/lib/model-builder-handoff.test.ts (4 tests) 2ms

 Test Files  1 passed (1)
      Tests  4 passed (4)
   Start at  18:13:04
   Duration  633ms (transform 33ms, setup 0ms, collect 40ms, tests 2ms, environment 0ms, prepare 152ms)
```

### `C:\Dev\Journeys\Journeys\Journeys.UX` — full

```powershell
cd C:\Dev\Journeys\Journeys\Journeys.UX
npm test
```

```
> journeys-ux@0.0.0 test
> vitest run

 Test Files  49 passed (49)
      Tests  239 passed (239)
   Start at  18:13:07
   Duration  3.09s (transform 2.45s, setup 0ms, collect 5.59s, tests 354ms, environment 14ms, prepare 7.94s)
```

(Full Journeys.UX file list omitted; all 49 files passed including `src/lib/model-builder-handoff.test.ts` and `src/lib/model-builder-brief.test.ts`.)
