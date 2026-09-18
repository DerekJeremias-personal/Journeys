# Task 7 report — live smoke

**Status:** DONE_WITH_CONCERNS (UX path works; API turn fails before MCP)

## Environment
- Journeys.API swagger https://127.0.0.1:7001 → 200
- Journeys.UX http://localhost:3000 → 200 (dev server already running)
- MSI Ollama http://192.168.1.191:11434 → 200
- Vitest: 15 files, 63 passed

## Browser
- `/loyalty/campaigns` shows **Agent** link
- `/loyalty/campaigns/agent` heading is exactly `Agent` (no Coach)
- Send "List campaigns for this tenant." → Send disabled, user line shown
- `started` delivered conversationId `9fa2531c87034811bda7c295b40d5beb`
- Stream ended with SSE `error` message `An unexpected error occurred`
- Send re-enabled; conversationId kept

## Direct API (same payload)
Same failure after `progress: load_history`:

```
event: error
data: {"code":"turn_failed","message":"An unexpected error occurred"}
```

This is `CampaignAgentOrchestrator` at history load/append, not the Next proxy. G3 (assistant text + successful MCP tool) is **not** met until that API exception is fixed.

## Pass/fail vs spec G3
- G1 visible agent: PASS
- G2 streamed turn (started through proxy): PASS
- G3 live MCP bar: FAIL (API turn_failed)
- G4 secrets server-side: PASS (browser posts to Next only)
- G5 no provider UI: PASS
