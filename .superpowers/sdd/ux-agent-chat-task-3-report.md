# Task 3 Report: Proxy auth, headers, and early SSE pipe

**Task:** Proxy auth, headers, and early SSE pipe (ux-agent-chat plan, task 3)  
**Branch:** `feat/journeys-ux-loyalty-shell`  
**Working directory:** `C:\Dev\Journeys\Journeys\Journeys.UX`  
**Date:** 2026-09-17

## Status

**DONE**

## Commits

None (per task instructions; human owns git).

## Summary

Implemented campaign-agent proxy auth/header mapping and an injectable `fetch`-based early SSE pipe. Keepalive was lifted unchanged from EXP. No Next route and no chat UI.

## Files Created

| File | Purpose |
|------|---------|
| `Journeys.UX/src/lib/campaign-agent/proxy-auth.ts` | `authorizeCampaignAgentProxy`, `campaignAgentUpstreamHeaders` |
| `Journeys.UX/src/lib/campaign-agent/proxy-auth.test.ts` | Auth/header Vitest coverage (5 cases, from brief) |
| `Journeys.UX/src/lib/campaign-agent/sse-keepalive.ts` | Lifted `wrapStreamWithSseKeepAlive` (unchanged except path) |
| `Journeys.UX/src/lib/campaign-agent/sse-early-proxy.ts` | `createEarlySseProxyStream` with injectable `connect` |
| `Journeys.UX/src/lib/campaign-agent/sse-early-proxy.test.ts` | Early-proxy Vitest coverage (3 cases, from brief) |

## Files Not Modified

- Next `route.ts` (Task 4)
- Chat UI (Task 5)
- EXP `undici` / Campaign Coach error copy not copied
- No C# files, no new npm dependencies

## Interfaces delivered

- `authorizeCampaignAgentProxy(session): { ok: true; session } | { ok: false; status: 401; error }`
- `campaignAgentUpstreamHeaders(session): Record<string, string>`
- `createEarlySseProxyStream({ upstreamUrl, upstreamInit, idleMs?, signal?, connect? }): ReadableStream<Uint8Array>`
- `wrapStreamWithSseKeepAlive` from lifted keepalive

## TDD Evidence

### Auth RED — Step 2 (failing test, module missing)

**Command:** `npm test -- src/lib/campaign-agent/proxy-auth.test.ts`  
**Working directory:** `Journeys.UX`  
**Exit code:** 1

```
FAIL  src/lib/campaign-agent/proxy-auth.test.ts
Error: Cannot find module './proxy-auth' imported from '.../proxy-auth.test.ts'
Caused by: Error: Failed to load url ./proxy-auth (resolved id: ./proxy-auth)
```

Expected failure: implementation file did not exist yet.

### Auth GREEN — Step 4

**Command:** `npm test -- src/lib/campaign-agent/proxy-auth.test.ts`  
**Exit code:** 0

```
 ✓ src/lib/campaign-agent/proxy-auth.test.ts (5 tests) 2ms

 Test Files  1 passed (1)
      Tests  5 passed (5)
```

### Early-proxy RED — Step 6 (failing test, module missing)

**Command:** `npm test -- src/lib/campaign-agent/sse-early-proxy.test.ts`  
**Exit code:** 1

```
FAIL  src/lib/campaign-agent/sse-early-proxy.test.ts
Error: Cannot find module './sse-early-proxy' imported from '.../sse-early-proxy.test.ts'
Caused by: Error: Failed to load url ./sse-early-proxy (resolved id: ./sse-early-proxy)
```

Expected failure: implementation file did not exist yet.

### Early-proxy GREEN — Step 8

**Command:** `npm test -- src/lib/campaign-agent/proxy-auth.test.ts src/lib/campaign-agent/sse-early-proxy.test.ts`  
**Exit code:** 0

```
 ✓ src/lib/campaign-agent/proxy-auth.test.ts (5 tests) 3ms
 ✓ src/lib/campaign-agent/sse-early-proxy.test.ts (3 tests) 6ms

 Test Files  2 passed (2)
      Tests  8 passed (8)
```

**Regression (same folder):** `npm test -- src/lib/campaign-agent` → 14/14 passing (includes Task 1–2 tests). Output pristine.

## Test Coverage

| Test | Assertion |
|------|-----------|
| rejects missing session without calling upstream | `ok === false`, `status === 401` |
| rejects session with no credentials | `ok === false` when both `apiKey` and `accessToken` missing |
| accepts API-key session | `ok === true` |
| sends Journeys-API-KEY when there is no access token | `Journeys-API-KEY` set; `Authorization` omitted; `Accept: text/event-stream` |
| sends Bearer and omits API key when accessToken is present | `Authorization: Bearer tok`; API key omitted |
| maps 401 to an SSE error and does not throw | SSE `error` event; data matches `/not authorized\|nope\|401/` |
| maps connect failure to Cannot reach Journeys.API | text matches `Cannot reach Journeys.API`; no `Coach` |
| forwards upstream SSE bytes on 200 event-stream | contains `event: started` and `c1` |

## Implementation Notes

- Auth/header mapping matches `journeysFetch`: Bearer wins over `Journeys-API-KEY`.
- Early proxy `connect` defaults to `fetch` (injectable for tests). No undici.
- Connect throw → SSE `error` with `Cannot reach Journeys.API (${describeFetchFailure(error)})`.
- HTTP 401/403 → SSE `error` message **exactly** `not authorized / check tenant or key` (not truncated upstream body).
- Other non-2xx → truncated body (max 500 chars), or `Upstream HTTP {status}` if empty.
- SSE error encoding: `event: error\ndata: {"message":"..."}\n\n`.
- Success path pipes `response.body` through `wrapStreamWithSseKeepAlive` (whole inner stream wrapped so idle heartbeats also cover connect wait).
- Calls `allowInsecureLocalHttps(upstreamUrl)` before connect (localhost TLS relax; no-op for `example.test` in unit tests).
- Does not log request bodies or keys.
- Waiting-upstream `proxy` events are **not** emitted (brief: label only *if* emitted).

## Self-Review

### Requirements compliance

- All Task 3 files created; Task 4 route and Task 5 UI left untouched.
- Keepalive copied from EXP `sse-keepalive.ts` unchanged except file path.
- Product 401/403 string used verbatim.

### Quality

- Injectable `connect` keeps tests off the network.
- Error path never throws to the caller; failures are SSE `error` events.

### Discipline

- Did not copy EXP coach strings, undici, or `upstream-sse-stream.ts`.
- Did not add waiting-upstream / connecting proxy events (optional; tests do not require them).

## Concerns

- Waiting-upstream (`Waiting for agent…`) is not emitted. The brief treats it as optional (`if emitted`). Task 4/5 can add it later if the UI needs a connecting status.
- `docs-impact` / `graph-impact` not run (intermediate UX lib task; meaning already in the approved spec/plan).
