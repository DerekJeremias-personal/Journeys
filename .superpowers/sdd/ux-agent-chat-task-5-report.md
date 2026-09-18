# Task 5 Report: Chat state, page, and Campaigns link

**Task:** Chat state, page, and Campaigns link (ux-agent-chat plan, task 5)  
**Branch:** `feat/journeys-ux-loyalty-shell`  
**Working directory:** `C:\Dev\Journeys\Journeys\Journeys.UX`  
**Date:** 2026-09-17

## Status

**DONE**

## Commits

None (per task instructions; human owns git).

## Summary

Added a pure SSE → transcript reducer (`applyAgentSseEvent` / `beginUserTurn`), a thin unlabeled Agent chat client, `/loyalty/campaigns/agent` with heading **Agent**, and an **Agent** link on the Campaigns list. Browser POSTs to the existing Next SSE proxy. No TanStack Query, kebab, wizard, resume banner, or “Campaign Coach” copy.

## Files Created

| File | Purpose |
|------|---------|
| `Journeys.UX/src/lib/campaign-agent/chat-state.ts` | `ChatLine`, `AgentChatState`, `beginUserTurn`, `applyAgentSseEvent` |
| `Journeys.UX/src/lib/campaign-agent/chat-state.test.ts` | Vitest coverage (3 cases, from brief) |
| `Journeys.UX/src/components/loyalty/agent-chat.tsx` | Client transcript + fetch stream |
| `Journeys.UX/src/app/loyalty/campaigns/agent/page.tsx` | Page heading `Agent` + `<AgentChat />` |

## Files Modified

| File | Change |
|------|--------|
| `Journeys.UX/src/app/loyalty/campaigns/page.tsx` | **Agent** link to `/loyalty/campaigns/agent` above the list |
| `Journeys.UX/src/app/globals.css` | `.agent-chat` transcript + form spacing only |

## Files Not Modified

- SSE proxy / `journeysFetch` / `map-loyalty-path.ts`
- Docs / graph (Task 6)
- No C# files, no new npm dependencies (`@tanstack/react-query`, `@exp/*`, `undici`)

## Interfaces delivered

- `export type ChatLine = { role: "user" | "assistant" | "error"; text: string }`
- `export type AgentChatState = { conversationId: string | null; lines: ChatLine[]; streaming: boolean }`
- `applyAgentSseEvent(state, ev): AgentChatState`
- `beginUserTurn(state, message): AgentChatState`
- Page heading exactly `Agent`
- Placeholder exactly `Describe what you want to build or change…`

## Reducer rules (implemented)

- `started`: set `conversationId` from JSON if present
- `delta`: append JSON `text` onto the last assistant line, or push a new one
- `done`: `streaming: false`; keep `conversationId` (does not overwrite from `done` payload)
- `error`: push error line, `streaming: false`, keep `conversationId`
- `progress`, `proxy`, other events: return state unchanged
- Empty trimmed message: client does not call begin/fetch

## TDD Evidence

### RED — Step 2 (failing test, module missing)

**Command:** `npm test -- src/lib/campaign-agent/chat-state.test.ts`  
**Working directory:** `Journeys.UX`  
**Exit code:** 1

```
FAIL  src/lib/campaign-agent/chat-state.test.ts
Error: Cannot find module './chat-state' imported from '.../chat-state.test.ts'
Caused by: Error: Failed to load url ./chat-state (resolved id: ./chat-state)
```

Expected failure: implementation file did not exist yet.

### GREEN — Step 4

**Command:** `npm test -- src/lib/campaign-agent/chat-state.test.ts`  
**Exit code:** 0

```
 ✓ src/lib/campaign-agent/chat-state.test.ts (3 tests) 2ms

 Test Files  1 passed (1)
      Tests  3 passed (3)
```

### Full suite — Step 8

**Command:** `npm test`  
**Working directory:** `Journeys.UX`  
**Exit code:** 0

```
 Test Files  15 passed (15)
      Tests  63 passed (63)
```

Includes existing `journeys-fetch.test.ts` (5 passed) and new `chat-state.test.ts` (3 passed). Prior Task 4 suite was 14 files / 59–60 tests.

## Browser check

Dev server already running at `http://localhost:3000`. Signed-in tab:

- `/loyalty/campaigns` shows heading **Campaigns** and **Agent** link
- `/loyalty/campaigns/agent` shows heading **Agent**, placeholder `Describe what you want to build or change…`, Send enabled
- Empty and whitespace-only Send are no-ops (no transcript line)
- No “Campaign Coach” strings

Did not run a live Ollama turn (G3 smoke is later).

## Decisions applied (from dispatch)

- TDD: chat-state tests first, then reducer, then UI
- Placeholder uses unicode ellipsis `…`
- 401/403 HTTP (non-SSE) → error line `not authorized / check tenant or key`
- Fetch: POST `/api/loyalty/campaign-agent/messages/stream` with `Content-Type` + `Accept: text/event-stream`, `cache: "no-store"`, body `{ message, conversationId }`
- Send disabled while `streaming`; `conversationId` shown in `<small>` when set
- No git commit

## Concerns

1. If the stream closes without `done`/`error`, the client forces `streaming: false` so Send re-enables. Brief did not specify this; without it a hung-open stream would lock the form.
2. Non-401/403 JSON errors surface `{ error }` or raw body text, not a fixed string.
3. `done` does not adopt `conversationId` from its payload (only `started` sets it). Matches “keep”, but a stream that skips `started` would leave `conversationId` null.
4. Docs/graph updates are deferred to Task 6.
5. Automation click on the Campaigns **Agent** link did not change URL; direct navigation to `/loyalty/campaigns/agent` rendered correctly. Likely the browser tool vs Next client routing, not a missing `href`.

## Self-review

- HTTP-only; no Core/DAL/Infra references
- In-product name is **agent**; no coach product name
- No TanStack Query, kebab, wizard, resume banner, JSON inspector, or linked-campaign picker
- Loyalty layout session gate still covers the new route
