# Task 4 Report: Next stream route

**Task:** Next stream route (ux-agent-chat plan, task 4)  
**Branch:** `feat/journeys-ux-loyalty-shell`  
**Working directory:** `C:\Dev\Journeys\Journeys\Journeys.UX`  
**Date:** 2026-09-17

## Status

**DONE** (review fix: stream-end log on cancel)

## Commits

None (per task instructions; human owns git).

## Summary

Implemented `handleCampaignAgentStreamPost` as an injectable Next SSE proxy handler, with a thin App Router `POST` that wires real `auth` + `fetch`. Session-missing returns 401 JSON without calling upstream. Empty/invalid body and unresolved tenant return 400 JSON. Success returns `200` `text/event-stream` from `createEarlySseProxyStream`, passing `signal: request.signal`. Upstream JSON is `{ message, conversationId }` only (no `linkedCampaignId`). Stream-end `console.info` logs `{ tenantId, conversationId, userId, status }` only.

## Files Created

| File | Purpose |
|------|---------|
| `Journeys.UX/src/lib/campaign-agent/stream-route.ts` | `handleCampaignAgentStreamPost` |
| `Journeys.UX/src/lib/campaign-agent/stream-route.test.ts` | Vitest coverage (7 cases) |
| `Journeys.UX/src/app/api/loyalty/campaign-agent/messages/stream/route.ts` | Thin `POST` + `dynamic` / `maxDuration` |

## Files Not Modified

- `journeysFetch` / JSON envelope helper
- Chat UI (Task 5)
- Docs / graph (Task 6)
- No C# files, no new npm dependencies

## Interfaces delivered

- `handleCampaignAgentStreamPost(request, { getSession, apiBaseUrl, connect }): Promise<Response>`
- `POST(request: Request): Promise<Response>`
- `export const dynamic = "force-dynamic"`
- `export const maxDuration = 600`

## TDD Evidence

### RED — Step 2 (failing test, module missing)

**Command:** `npm test -- src/lib/campaign-agent/stream-route.test.ts`  
**Working directory:** `Journeys.UX`  
**Exit code:** 1

```
FAIL  src/lib/campaign-agent/stream-route.test.ts
Error: Cannot find module './stream-route' imported from '.../stream-route.test.ts'
Caused by: Error: Failed to load url ./stream-route (resolved id: ./stream-route)
```

Expected failure: implementation file did not exist yet.

### GREEN — Step 4

**Command:** `npm test -- src/lib/campaign-agent/stream-route.test.ts`  
**Exit code:** 0

```
 ✓ src/lib/campaign-agent/stream-route.test.ts (7 tests) 14ms

 Test Files  1 passed (1)
      Tests  7 passed (7)
```

**Command:** `npm test`  
**Working directory:** `Journeys.UX`  
**Exit code:** 0

```
 Test Files  14 passed (14)
      Tests  59 passed (59)
```

Includes existing `journeys-fetch.test.ts` (5 passed).

## Test coverage

- 401, no `connect`, `{ error }` matches `/not authenticated/i` when session is missing
- 400 `{ error: "message is required" }` on blank `message`
- 400 on invalid JSON, no `connect`
- 401 when session has no credentials, no `connect`
- 400 when tenant cannot be resolved (`vi.stubEnv("JOURNEYS_TENANT_ID", "")`)
- 200 SSE headers; upstream URL; trimmed message; `conversationId` forwarded; `linkedCampaignId` omitted; `init.signal === request.signal`
- `console.info` once with `{ tenantId, conversationId, userId, status }`; prompt and API key not logged

## Decisions applied (from dispatch)

- `signal: request.signal` into `createEarlySseProxyStream`
- Tests stub `JOURNEYS_TENANT_ID` to `""` (same pattern as `journeys-fetch.test.ts`)
- Never log `message` body or headers
- Omit `linkedCampaignId`; do not wrap SSE in `{ success, data, error }`
- No git commit

## Concerns

1. Empty `JOURNEYS_API_BASE_URL` throws from `campaignAgentStreamUrl` (unhandled) rather than a JSON 400. Brief did not specify that case.
2. Docs/graph updates are deferred to Task 6.

## Review fix — stream-end log on cancel (2026-09-17)

**Finding:** Stream-end `console.info` ran only from TransformStream `flush`; client abort/cancel skipped it.

**Change:** `logWhenStreamEnds` uses a once-guard invoked from both `flush` and `cancel` (never logs message or headers).

**Test added:** `logs once when the response body is cancelled before the stream ends` — reads one chunk, calls `reader.cancel()`, asserts exactly one `console.info` with `{ tenantId, conversationId, userId, status }`.

### TDD — RED (expected before fix)

Cancel test would fail: `console.info` call count 0 because `flush` never runs on `reader.cancel()`.

### TDD — GREEN (after fix)

**Command:** `npm test -- src/lib/campaign-agent/stream-route.test.ts`  
**Working directory:** `Journeys.UX`  
**Exit code:** 0

```
 ✓ src/lib/campaign-agent/stream-route.test.ts (8 tests) 15ms

 Test Files  1 passed (1)
      Tests  8 passed (8)
```

## Self-review

- Handler is HTTP-only; no Core/DAL/Infra references
- 401/400 are `{ error }` JSON; success is raw SSE
- Auth0 `"hayward"` is not used as tenant; `resolveTenantId` wins
- No coach product name
