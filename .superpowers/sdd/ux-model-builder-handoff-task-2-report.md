# Task 2 report: Nav + handoff page + docs

**Status:** DONE  
**Commits:** none

## TDD evidence

### RED (tests first, implementation absent)

Command:

```powershell
cd C:\Dev\Journeys\Journeys\Journeys.UX
npm test -- src/lib/model-builder-handoff.test.ts
```

Result: FAIL (expected)

- `src/lib/model-builder-handoff.test.ts` — `Cannot find module './model-builder-handoff'`
- Test Files 1 failed (1); Tests no tests

### GREEN (exact handoff helpers)

Same focused command.

Result: PASS

```
✓ src/lib/model-builder-handoff.test.ts (2 tests)
Test Files  1 passed (1)
     Tests  2 passed (2)
```

### Full suite (once)

```powershell
cd C:\Dev\Journeys\Journeys\Journeys.UX
npm test
```

Result: PASS — 49 files, 237 tests.

## Docs / graph

```powershell
cd C:\Dev\Journeys\Journeys
.\scripts\docs-impact.ps1 -Files @(
  "Journeys.UX/src/lib/model-builder-brief.ts",
  "Journeys.UX/src/app/loyalty/models/handoff/page.tsx",
  "docs/developer/journeys-ux.md"
)
.\scripts\graph-impact.ps1 -Files @(
  "Journeys.UX/src/lib/model-builder-brief.ts",
  "docs/developer/journeys-ux.md",
  "docs/product/graph/waivers/2026-09-18-model-builder-handoff.md"
)
```

- `docs-impact: OK (2 files)`
- `graph-impact: OK` — `event-models` covered by waiver; no `IMPLEMENTED_AS` `proj-ux`

## Files changed

| Path | Action |
|------|--------|
| `Journeys.UX/src/lib/model-builder-handoff.ts` | Created — `modelBuilderUxBaseUrl`, `modelBuilderSeedActionUrl` |
| `Journeys.UX/src/lib/model-builder-handoff.test.ts` | Created — empty base + trailing-slash seed URL |
| `Journeys.UX/src/app/loyalty/models/handoff/page.tsx` | Created — form POST `tenantId` / `brief` / `source=journeys` to seed URL |
| `Journeys.UX/src/components/loyalty-nav.tsx` | Modified — **Model Builder** links to `/loyalty/models/handoff` when env is set |
| `Journeys.UX/.env.example` | Modified — `BACKEND_MODEL_UX_BASE_URL=` |
| `docs/developer/journeys-ux.md` | Modified — spec line + Model Builder subsection |
| `docs/specs/2026-09-14-Journeys-ux-loyalty-shell-design.md` | Modified — NonGoals model builder now points at handoff spec |
| `docs/product/graph/waivers/2026-09-18-model-builder-handoff.md` | Created |

Reused Task 1 `buildModelBuilderBrief`. No Model save APIs. No Backend.Model.UX. Brief is not in the query string and is not logged.
