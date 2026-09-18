# Task 1 Report: Upstream stream URL

**Task:** Upstream stream URL (ux-agent-chat plan, task 1)  
**Branch:** `feat/journeys-ux-loyalty-shell`  
**Working directory:** `C:\Dev\Journeys\Journeys\Journeys.UX`  
**Date:** 2026-09-17

## Status

**DONE**

## Commits

None (per task instructions; human owns git).

## Summary

Added `campaignAgentStreamUrl(apiBaseUrl, tenantId)` — a pure URL builder that maps a Journeys.API base URL and tenant id to the campaign-agent SSE stream endpoint per spec §4. No chat UI, proxy, or parser work was included.

## Files Created

| File | Purpose |
|------|---------|
| `Journeys.UX/src/lib/campaign-agent/stream-url.ts` | Exports `campaignAgentStreamUrl` |
| `Journeys.UX/src/lib/campaign-agent/stream-url.test.ts` | Vitest coverage (4 cases) |

## Files Not Modified

- `map-loyalty-path.ts` — untouched per brief
- No new npm dependencies (`undici`, `@exp/*`, etc.)
- No C# files

## TDD Evidence

### RED — Step 2 (failing test, module missing)

**Command:** `npm test -- src/lib/campaign-agent/stream-url.test.ts`  
**Working directory:** `Journeys.UX`  
**Exit code:** 1

```
 FAIL  src/lib/campaign-agent/stream-url.test.ts
Error: Cannot find module './stream-url' imported from '.../stream-url.test.ts'
```

Expected failure: implementation file did not exist yet.

### GREEN — Step 4 (all tests pass)

**Command:** `npm test -- src/lib/campaign-agent/stream-url.test.ts`  
**Working directory:** `Journeys.UX`  
**Exit code:** 0

```
 ✓ src/lib/campaign-agent/stream-url.test.ts (4 tests) 3ms

 Test Files  1 passed (1)
      Tests  4 passed (4)
```

## Test Coverage

| Test | Assertion |
|------|-----------|
| maps to Journeys.API campaign-agent stream | `https://127.0.0.1:7001` + `TestTenant1` → `.../api/v1/TestTenant1/campaign-agent/messages/stream` |
| trims trailing slash and encodes tenant | Trailing `/` removed; space in tenant → `%20` |
| rejects empty tenantId | Whitespace-only tenant throws `/tenant/i` |
| rejects empty base URL | Whitespace-only base throws `/JOURNEYS_API_BASE_URL\|base/i` |

## Implementation Notes

- Trims `tenantId` and `apiBaseUrl` before validation
- Strips trailing slashes from base URL via `/\/+$/`
- Uses `encodeURIComponent(tenant)` for path segment encoding
- Error messages align with existing `map-loyalty-path.ts` tenant validation pattern and brief-specified base URL error text

## Self-Review

### Requirements compliance

- TDD order followed: test first → RED → implement → GREEN
- Exact test content from brief — yes
- Exact implementation from brief — yes
- Produces `campaignAgentStreamUrl(apiBaseUrl: string, tenantId: string): string` — yes
- URL shape: `{base}/api/v1/{tenant}/campaign-agent/messages/stream` — yes

### Constraints

- No git commit/push/merge — honored
- No chat UI, proxy, or SSE parser — honored
- Linter: no issues on new files

### Concerns

None.

## Next Steps (downstream tasks)

Later tasks can import `campaignAgentStreamUrl` from `@/lib/campaign-agent/stream-url` (or relative path) when wiring the Campaign Agent chat proxy or client.
