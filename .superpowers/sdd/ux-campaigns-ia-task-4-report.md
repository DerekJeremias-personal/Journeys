# Task 4 report: Agent data plane + inspector + resume

## Status

Implemented without commits.

## Changes

- Added recursive UUID campaign-id extraction for `id`, `campaignId`, and `linkedCampaignId`, including PascalCase keys.
- Extended `AgentChatState` with progress, linked campaign, and discovered campaign fields.
- Reduced `progress`, campaign-bearing MCP/workflow/tool/done events, and preserved conversation/campaign context on errors.
- Forwarded non-null `linkedCampaignId` and `clientMessageId` through the SSE proxy.
- Extended `AgentChat` props, seeded resume/link context, generated a UUID per send, displayed progress, and rendered the campaign inspector.
- Added the collapsed `Campaign JSON` disclosure with Draft-first GET fallback, pretty JSON, copy, loading, and contained error states.
- Added standalone conversation resume, list-level latest-thread resume, and the Live-only campaign Agent route with normalized status redirects.
- Exported the Task 5 `hydrateDecision` policy without adding builder wiring.

## TDD evidence

- RED: focused run failed for missing extract/hydrate modules, ignored progress/MCP events, and omitted proxy fields.
- GREEN: focused Task 4 suite passed: 4 files, 19 tests.
- Full `npm test`: 20 files, 90 tests passed.
- IDE lint diagnostics: no errors in Task 4 files.
- `docs-impact`: passed for 7 code files.
- `graph-impact`: passed for `campaigns, campaign-agent`.

## Concern

`npx tsc --noEmit` remains blocked by three pre-existing diagnostics in `src/auth.ts`, `src/lib/campaign-agent/stream-route.ts` (`Transformer.cancel`), and `src/lib/loyalty-model.test.ts`. The Task 4 additions introduced no additional diagnostics.

## Fix pass

### Changes

- `stream-route.ts`: removed invalid `TransformStream` transformer `cancel`; wrapped piped stream in outer `ReadableStream` with `cancel` calling `logOnce()`.
- `chat-state.ts`: `started` event now sets `linkedCampaignId` / `LinkedCampaignId` when present without clearing omitted values.
- `chat-state.test.ts`: added test for started with both ids and error retention.

### Commands

```
npx vitest run src/lib/campaign-agent/chat-state.test.ts src/lib/campaign-agent/stream-route.test.ts
```

```
 RUN  v3.2.7 C:/Dev/Journeys/Journeys/Journeys.UX

 ✓ src/lib/campaign-agent/chat-state.test.ts (6 tests) 5ms
 ✓ src/lib/campaign-agent/stream-route.test.ts (9 tests) 18ms

 Test Files  2 passed (2)
      Tests  15 passed (15)
   Start at  12:48:26
   Duration  499ms (transform 107ms, setup 0ms, collect 158ms, tests 22ms, environment 0ms, prepare 216ms)
```

```
npm test
```

```
> journeys-ux@0.0.0 test
> vitest run

 RUN  v3.2.7 C:/Dev/Journeys/Journeys/Journeys.UX

 ✓ src/lib/map-loyalty-path.test.ts (10 tests) 10ms
 ✓ src/lib/campaign-kebab.test.ts (7 tests) 6ms
 ✓ src/lib/local-https.test.ts (3 tests) 4ms
 ✓ src/lib/wrap-api-envelope.test.ts (5 tests) 5ms
 ✓ src/lib/campaign-agent/hydrate-policy.test.ts (2 tests) 4ms
 ✓ src/lib/campaign-agent/copy-text.test.ts (2 tests) 7ms
 ✓ src/lib/journeys-fetch.test.ts (6 tests) 10ms
 ✓ src/lib/loyalty-model.test.ts (5 tests) 6ms
 ✓ src/services/loyalty/parse-list.test.ts (9 tests) 6ms
 ✓ src/lib/campaign-agent/sse-early-proxy.test.ts (3 tests) 11ms
 ✓ src/lib/campaign-agent/stream-route.test.ts (9 tests) 36ms
 ✓ src/lib/campaign-agent/sse.test.ts (2 tests) 5ms
 ✓ src/lib/campaign-agent/extract-campaign-id.test.ts (3 tests) 5ms
 ✓ src/lib/campaign-agent/chat-state.test.ts (6 tests) 6ms
 ✓ src/lib/campaign-agent/proxy-auth.test.ts (5 tests) 5ms
 ✓ src/lib/campaign-agent/stream-url.test.ts (4 tests) 6ms
 ✓ src/lib/resolve-tenant-id.test.ts (2 tests) 4ms
 ✓ src/lib/campaign-agent/chat-keys.test.ts (4 tests) 4ms
 ✓ src/lib/api-key-login-enabled.test.ts (2 tests) 4ms
 ✓ src/lib/auth0-login-enabled.test.ts (2 tests) 5ms

 Test Files  20 passed (20)
      Tests  91 passed (91)
   Start at  12:48:29
   Duration  1.21s (transform 931ms, setup 0ms, collect 1.93s, tests 149ms, environment 5ms, prepare 3.19s)
```
