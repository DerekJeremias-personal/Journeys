# Task 2 Report: Allowlist path map + envelope (TDD)

## Status

**DONE_WITH_CONCERNS** — all brief files created verbatim; `npm test` could not be executed (Shell blocked by preToolUse hook).

## Commits

None (per instructions).

## Files created

| File | Role |
|------|------|
| `Journeys.UX/src/lib/api-types.ts` | `ApiResponse`, `CampaignListItem`, `SchemaListItem` |
| `Journeys.UX/src/lib/map-loyalty-path.ts` | Allowlist path → backend URL mapper |
| `Journeys.UX/src/lib/wrap-api-envelope.ts` | HTTP status + body → `ApiResponse` envelope |
| `Journeys.UX/src/lib/map-loyalty-path.test.ts` | 5 vitest cases (brief verbatim) |
| `Journeys.UX/src/lib/wrap-api-envelope.test.ts` | 5 vitest cases (brief verbatim) |

## TDD cycle

### Step 1 — Types + failing tests (RED, intended)

Created `api-types.ts`, `map-loyalty-path.test.ts`, and `wrap-api-envelope.test.ts` before implementation modules.

**Expected RED output** (not captured — shell denied):

```
FAIL — Cannot find module './map-loyalty-path' / './wrap-api-envelope'
```

### Step 2 — Implementation (GREEN, intended)

Added `map-loyalty-path.ts` and `wrap-api-envelope.ts` exactly as specified in the brief.

**Expected GREEN output** (not captured — shell denied):

```
Test Files  2 passed (2)
     Tests  10 passed (10)
```

**Manual verification:** Implementation matches brief regexes and envelope logic; test assertions align with spec.

## Self-review

| Check | Result |
|-------|--------|
| Brief values verbatim | Yes — types, tests, and implementations copied exactly |
| `mapLoyaltyPath` uses session `tenantId`, not path slug | Yes — slug segments ignored; tenant encoded in output |
| Unknown paths throw `not-allowlisted:` | Yes |
| Empty tenant throws `/tenant/i` match | Yes — `"tenantId is required"` |
| `wrapApiEnvelope` success 2xx, JSON parse, error field, non-JSON, empty 200 | Yes |
| No Core/DAL/Infra references | Yes — UX lib only |
| No invented capability ids | Yes |
| No EXP monorepo copy | Yes |

## Concerns

1. **Tests not run** — parent/human should run `cd C:\Dev\Journeys\Journeys\Journeys.UX && npm test` to confirm 10/10 pass.
2. **TDD RED log missing** — RED phase was structurally followed (tests before impl) but terminal output unavailable.

## Next step for human

```powershell
cd C:\Dev\Journeys\Journeys\Journeys.UX
npm test
```

Expect: 2 test files, 10 tests, all pass.
