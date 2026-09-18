# Task 6 Report: Docs and graph

**Task:** Docs and graph (ux-agent-chat plan, task 6)  
**Branch:** `feat/journeys-ux-loyalty-shell`  
**Working directory:** `C:\Dev\Journeys\Journeys`  
**Date:** 2026-09-17

## Status

**DONE**

## Commits

None (per task instructions; human owns git).

## Summary

Documented the unlabeled Agent chat and Next SSE proxy, mapped `Journeys.UX` to `[campaigns, campaign-agent]`, and added architecture/overlays one-liners that UX may proxy Campaign Agent SSE and must not own campaign writes. Spec status left `Approved (human 2026-09-17)`. No `campaign-agent` `IMPLEMENTED_AS` `proj-ux` edge.

## Files Created

None.

## Files Modified

| File | Change |
|------|--------|
| `docs/product/graph/path-map.yaml` | `Journeys.UX` nodes: `[campaigns, campaign-agent]` |
| `docs/developer/journeys-ux.md` | `## Agent` section after screens table (verbatim from brief) |
| `docs/developer/campaign-agent-llm.md` | Live smoke: UX at `/loyalty/campaigns/agent` is the operator surface; provider switch is unchanged |
| `docs/platform/architecture.md` | UX sentence + UI row: may proxy Campaign Agent SSE; not write authority |
| `docs/platform/overlays.md` | UI cell: may proxy Campaign Agent SSE (`path-docs-map.yaml` requires this doc for `Journeys.UX`) |

## Files Not Modified

- `docs/specs/2026-09-17-Journeys-ux-campaign-agent-chat-design.md` — already `Status: Approved (human 2026-09-17)`
- `docs/product/graph/edges.yaml` — no `campaign-agent` `IMPLEMENTED_AS` `proj-ux`
- Product TS / C# (Tasks 1–5 code left unchanged)

## Confirmations

### path-map.yaml

```yaml
  - prefix: Journeys.UX
    nodes: [campaigns, campaign-agent]
    meaningOptional: false
```

### edges.yaml

`campaign-agent` `IMPLEMENTED_AS` targets remain `proj-api` and `proj-agent` only. No `campaign-agent` → `proj-ux` edge.

## Verify

**Working directory:** `C:\Dev\Journeys\Journeys`

### docs-impact

**Command:**

```powershell
.\scripts\docs-impact.ps1 -Files @(
  'Journeys.UX/src/lib/campaign-agent/stream-url.ts',
  'Journeys.UX/src/lib/campaign-agent/sse.ts',
  'Journeys.UX/src/lib/campaign-agent/sse-keepalive.ts',
  'Journeys.UX/src/lib/campaign-agent/proxy-auth.ts',
  'Journeys.UX/src/lib/campaign-agent/sse-early-proxy.ts',
  'Journeys.UX/src/lib/campaign-agent/stream-route.ts',
  'Journeys.UX/src/lib/campaign-agent/chat-state.ts',
  'Journeys.UX/src/components/loyalty/agent-chat.tsx',
  'Journeys.UX/src/app/api/loyalty/campaign-agent/messages/stream/route.ts',
  'Journeys.UX/src/app/loyalty/campaigns/agent/page.tsx',
  'Journeys.UX/src/app/loyalty/campaigns/page.tsx',
  'docs/developer/journeys-ux.md',
  'docs/developer/campaign-agent-llm.md',
  'docs/platform/architecture.md',
  'docs/platform/overlays.md',
  'docs/product/graph/path-map.yaml'
)
```

**Exit code:** 0

```
docs-impact: OK (11 files)
DOCS_IMPACT_EXIT=0
```

### graph-impact

**Command:**

```powershell
.\scripts\graph-impact.ps1 -Files @(
  'Journeys.UX/src/app/loyalty/campaigns/agent/page.tsx',
  'Journeys.UX/src/lib/campaign-agent/stream-route.ts',
  'docs/product/graph/path-map.yaml'
)
```

**Exit code:** 0

```
graph-impact: OK nodes=campaigns, campaign-agent productUpdate=True waiver=False
GRAPH_IMPACT_EXIT=0
```

Both scripts exited 0 on first run; no extra doc pointers were required beyond the listed files.

## Concerns

- `architecture.md` / `overlays.md` already had unrelated uncommitted work; only the required one-liners were added.
- overlays.md was touched because `scripts/path-docs-map.yaml` lists it as required for the `Journeys.UX` prefix.
- `journeys-ux.md` “This spec’s screens” still lists Overview / Accounts / Campaigns only; Agent is in the new `## Agent` section.

## Checklist

- [x] Step 1: `path-map.yaml` `Journeys.UX` nodes `[campaigns, campaign-agent]`
- [x] Step 2: `## Agent` in `journeys-ux.md` after screens table
- [x] Step 3: Live smoke one-liner in `campaign-agent-llm.md`
- [x] Step 4: Architecture one-liner (SSE proxy HTTP client; not write authority)
- [x] Step 5: docs-impact 0, graph-impact 0
- [x] Step 6: Commit skipped
