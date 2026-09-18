### Task 6: Docs and graph

**Files:**
- Modify: `docs/developer/journeys-ux.md`
- Modify: `docs/developer/campaign-agent-llm.md`
- Modify: `docs/product/graph/path-map.yaml` (`Journeys.UX` nodes: `[campaigns, campaign-agent]`)
- Modify: `docs/platform/architecture.md` â€” UI row / Journeys.UX sentence: SSE proxy is an HTTP client of Campaign Agent; UX is not the write authority
- Modify: `docs/specs/2026-09-17-Journeys-ux-campaign-agent-chat-design.md` â€” `Status: Approved (human 2026-09-17)`
- Touch `docs/platform/overlays.md` only if `docs-impact` requires it in the change set; prefer a one-line pointer that UX may proxy Campaign Agent SSE. Do **not** add `campaign-agent` `IMPLEMENTED_AS` `proj-ux` in `edges.yaml`.

**Interfaces:**
- Consumes: spec Â§8
- Produces: `docs-impact` and `graph-impact` exit 0 on this change set

- [ ] **Step 1: Update `path-map.yaml`**

```yaml
  - prefix: Journeys.UX
    nodes: [campaigns, campaign-agent]
    meaningOptional: false
```

- [ ] **Step 2: Add to `journeys-ux.md` after the screens table**

```
## Agent

Unlabeled chat at `/loyalty/campaigns/agent` (link from Campaigns). Browser POSTs to Next `/api/loyalty/campaign-agent/messages/stream`; the server forwards to `POST /api/v1/{tenantId}/campaign-agent/messages/stream`. Secrets stay on the Next server. LLM provider is API startup config (`docs/developer/campaign-agent-llm.md`), not a UX control.

Live smoke: API + Ollama tray â†’ sign in â†’ Campaigns â†’ Agent â†’ one turn with streamed assistant text and at least one successful MCP tool. Not required: Draft upsert or Live publish.
```

- [ ] **Step 3: Add one line to `campaign-agent-llm.md` Live smoke:** UX at `/loyalty/campaigns/agent` is the operator surface; provider switch is unchanged.

- [ ] **Step 4: Architecture one-liner** that `Journeys.UX` may proxy Campaign Agent SSE and still must not own campaign writes.

- [ ] **Step 5: Run impact scripts from `Journeys/` with the changed file list**

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
.\scripts\graph-impact.ps1 -Files @(
  'Journeys.UX/src/app/loyalty/campaigns/agent/page.tsx',
  'Journeys.UX/src/lib/campaign-agent/stream-route.ts',
  'docs/product/graph/path-map.yaml'
)
```

Expected: both exit 0. If docs-impact names a missing doc, add the pointer in that file and re-run.

- [ ] **Step 6: Commit** â€” skip unless the user asks.

---