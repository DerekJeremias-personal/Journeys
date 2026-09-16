# Task 3 Report: journeysFetch (TDD)

## Status

**DONE_WITH_CONCERNS** — test and implementation files created verbatim per brief; `npm test` could not be executed (Shell blocked by preToolUse hook).

## Commits

None (per instructions).

## Files created

| File | Role |
|------|------|
| `Journeys.UX/src/lib/journeys-fetch.test.ts` | 4 vitest cases (brief verbatim) |
| `Journeys.UX/src/lib/journeys-fetch.ts` | Session-aware fetch wrapper with injectable deps |

## TDD cycle

### Step 1 — Failing tests (RED, intended)

Created `journeys-fetch.test.ts` before `journeys-fetch.ts`.

**Expected RED output** (not captured — shell denied):

```
FAIL — Cannot find module './journeys-fetch'
```

### Step 2 — Implementation (GREEN, intended)

Added `journeys-fetch.ts` exactly as specified in the brief.

**Expected GREEN output** (not captured — shell denied):

```
Test Files  3 passed (3)
     Tests  14 passed (14)
```

(4 new `journeysFetch` tests + 10 from Task 2.)

## Self-review

| Check | Result |
|-------|--------|
| Brief values verbatim | Yes — tests and implementation copied exactly |
| Consumes `mapLoyaltyPath`, `wrapApiEnvelope` | Yes |
| Missing session → `"Not authenticated"`, no network | Yes — checks `!session?.userId` |
| Missing tenantId → `"TenantId is required"`, no network | Yes |
| Missing base URL → `"JOURNEYS_API_BASE_URL is not configured"` | Yes |
| Not-allowlisted path → error contains `not-allowlisted`, no network | Yes — caught from `mapLoyaltyPath` throw |
| Auth: Bearer when `accessToken`; else API key; else `"No credentials in session"` | Yes |
| `defaultGetSession` uses dynamic `import("@/auth")` only when deps omitted | Yes — no top-level `@/auth` import |
| Tests always inject `deps` (no NextAuth load in Task 3) | Yes |
| Trailing slash stripped from `apiBaseUrl` | Yes — `.replace(/\/+$/, "")` |
| `cache: "no-store"` on fetch | Yes |
| No Core/DAL/Infra references | Yes — UX lib only |

## Concerns

1. **Tests not run** — parent/human should run `cd C:\Dev\Journeys\Journeys\Journeys.UX && npm test` to confirm 14/14 pass.
2. **TDD RED log missing** — RED phase was structurally followed (tests before impl) but terminal output unavailable.
3. **`@/auth` does not exist yet** (Task 4) — safe for Task 3 because all tests inject `deps`; production path will resolve when `auth.ts` lands.

## Next step for human

```powershell
cd C:\Dev\Journeys\Journeys\Journeys.UX
npm test
```

Expect: 3 test files, 14 tests, all pass.
