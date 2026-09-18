# Design: Journeys.UX unlabeled campaign agent chat

**Date:** 2026-09-17  
**Status:** Approved (human 2026-09-17)  
**Scope:** Show the Campaign Agent in `Journeys.UX` as an unlabeled chat: one route, Next SSE proxy, live turn against `Journeys.API` (Development = Ollama). HTTP-only.  
**Depends on:** `docs/specs/2026-09-14-Journeys-ux-loyalty-shell-design.md` (shell, auth, `journeysFetch` JSON helper, local HTTPS), `docs/specs/2026-09-16-Journeys-campaign-agent-ollama-design.md` (API provider; **already shipped**). Existing `CampaignAgentController` SSE and `CampaignAgentToolAudit*`.  
**Does not implement:** full Campaigns IA. That work is `docs/specs/2026-09-18-Journeys-ux-campaigns-ia-design.md` (supersedes the unapproved 2026-09-16 file). This file is a **smaller increment**; it does not cancel the IA spec.  
**Placement authority:** `docs/product/` (`author-campaign.md`, `campaign.md`, `tenant.md`) and `AGENTS.md`.  
**If this spec and onion rules disagree:** `Journeys.UX` stays HTTP-only. No Core/DAL/Infra project-reference. No BFF.  
**If this spec and `docs/platform/security.md` disagree on Auth0 `"hayward"`:** security.md wins; never treat `"hayward"` as `TenantId`.

Capability ids already exist: `campaign-agent`, `campaigns`, `mcp-api`. Do not invent new ones.

In-product name is **agent** (unlabeled). Do not ship “Campaign Coach” or other product names.

---

## 1. Problem and goals

`Journeys.API` Campaign Agent already streams SSE (`started` / `delta` / `done` | `error`) and, in Development, uses Ollama (`CampaignAgent:Provider`). Operators can hit that from Postman or anonymous-dev SSE, but **Journeys.UX has no agent UI**. The full Campaigns IA (kebab, copy, wizard, builder) is a later spec.

| # | Goal | Success criterion |
|---|------|-------------------|
| G1 | **Visible agent** | Signed-in user reaches `/loyalty/campaigns/agent` from an **Agent** link on the Campaigns list |
| G2 | **Streamed turn** | One message streams assistant text through the Next proxy to `Journeys.API` |
| G3 | **Live bar** | Same as `docs/developer/campaign-agent-llm.md`: assistant text **and** at least one **successful** MCP tool (`list_campaigns` or digest). Not required: Draft upsert or Live publish |
| G4 | **Secrets stay server-side** | Browser never sees `Journeys-API-KEY`, JWT, or LLM keys |
| G5 | **No provider UI** | Ollama vs Anthropic remains API startup config |

### Non-goals (this spec)

- Journey Builder, campaign wizard, list kebab, copy/restore, AdminAudit on Campaign REST
- Resume banner, conversation list UI, JSON inspector, clear-session panel, linked-campaign picker
- TanStack Query, EXP permission catalog, Prisma, `@exp/*` packages
- Changing `CampaignAgent:Provider` or HintPath to `Backend.Llm.OpenAICompatible`
- Browser EventSource directly to `Journeys.API`
- Playwright; CI calling live Ollama
- New capability ids

---

## 2. Decisions

| Topic | Choice |
|-------|--------|
| Increment | Chat only (brainstorming **A**) |
| UI | Thin chat + lift EXP SSE plumbing only (approach **1**) |
| Route | `/loyalty/campaigns/agent` |
| Entry | **Agent** link on `/loyalty/campaigns` |
| JSON vs SSE | `journeysFetch` unchanged (JSON). SSE is a dedicated App Router `POST` |
| Conversation | New thread until `started` returns `conversationId`; reuse on that page; reload = new thread |
| `linkedCampaignId` | Omit |
| Agent copy | Unlabeled “Agent” |
| Audit | API tool-audit only; no UX audit store |
| Commit of this spec | Human only |

---

## 3. Placement

```
Journeys.UX/
  src/app/loyalty/campaigns/page.tsx          # Agent link
  src/app/loyalty/campaigns/agent/page.tsx    # unlabeled chat
  src/app/api/loyalty/campaign-agent/messages/stream/route.ts
  src/lib/campaign-agent/sse.ts               # lift EXP pushSseBytes
  src/lib/campaign-agent/sse-early-proxy.ts   # lift + remap headers
  src/components/loyalty/agent-chat.tsx       # thin client
```

No C# changes required if `CampaignAgentController` and Ollama DI already run. Do not add UX as a csproj.

Lift source (SSE only): `temp/exp/apps/admin-web/src/lib/campaign-agent/sse.ts`, `sse-early-proxy.ts`, `upstream-sse-stream.ts`, `sse-keepalive.ts`. Rewrite EXP BFF/permission headers to session Bearer or `Journeys-API-KEY`. Drop coach copy files.

---

## 4. Architecture and data flow

```
Browser (Agent page)
  → POST /api/loyalty/campaign-agent/messages/stream   { message, conversationId? }
      session required; tenant = JOURNEYS_TENANT_ID when set
      → POST {JOURNEYS_API_BASE_URL}/api/v1/{tenantId}/campaign-agent/messages/stream
          headers: Bearer or Journeys-API-KEY
          local HTTPS: same NODE_TLS_REJECT_UNAUTHORIZED=0 localhost rule as journeysFetch
      ← text/event-stream  started | delta | done | error
  ← transcript
```

Allowlist fail-closed: this stream path only for the proxy. Do not turn the SSE route into an open HTTP tunnel.

`started` → store `conversationId` on the page. Later POSTs include it. `error` after `started` **keeps** the id. Send disabled while a turn is open.

Ollama connect-retry stays in `Journeys.Infra.Llm` (`ConnectRetrySeconds`). UX does not retry the LLM itself.

---

## 5. Components

| Unit | Does | Depends on |
|------|------|------------|
| Campaigns **Agent** link | Navigate to the chat | Next `Link` |
| Agent page | Heading “Agent”, transcript, input | `/loyalty` session gate |
| Chat client | User lines, delta append, conversationId | Stream route |
| Stream route | Auth, tenant, pipe upstream | Session, env, local HTTPS helper |
| SSE parser | Parse `event:` / `data:` blocks | Lifted `sse.ts` |
| Early proxy | Connect, keepalive, SSE `error` on connect fail | Lifted proxy (no EXP types) |
| `Journeys.API` Campaign Agent | Orchestrator, MCP, Ollama, tool-audit | Unchanged this spec |

---

## 6. Errors, logging, audit

| Failure | Response |
|---------|----------|
| No session | Redirect `/signin` |
| Proxy 401/403 | “not authorized / check tenant or key” |
| Empty message | Client no-op |
| Upstream down / TLS / timeout | SSE `error` (or one page error if the stream never opens) |
| Mid-turn SSE `error` | Line in transcript; re-enable Send; keep `conversationId` |
| MSI Ollama down past connect-retry | SSE `error`, not a silent hang |

Logs: `TenantId`, `conversationId`, `userId`, status/duration. **Never** keys, JWT, prompts, or full SSE bodies (`docs/developer/logging.md`).

Audit: existing `CampaignAgentToolAudit*` blob records. Next does not write a parallel file and must not disable API audit.

---

## 7. Testing

1. Vitest: stream URL maps to `/api/v1/{tenantId}/campaign-agent/messages/stream`.
2. Vitest: `pushSseBytes` yields `started` / `delta` / `done` / `error`.
3. Vitest: proxy without session → 401 (no upstream).
4. Existing `journeysFetch` tests still pass.
5. Human/browser: API + Ollama tray → sign-in → Campaigns → Agent → one turn with assistant text **and** a successful MCP tool.

No Playwright. No `Journeys.Tests` change unless an API bug is found (then a separate fix, not this spec’s scope).

---

## 8. Graph and docs

- `docs/product/graph/path-map.yaml`: `Journeys.UX` prefix nodes become `[campaigns, campaign-agent]` (`meaningOptional: false`). Do **not** add `campaign-agent` `IMPLEMENTED_AS` `proj-ux`.
- `docs/developer/journeys-ux.md`: document the Agent link, SSE proxy, unlabeled name, live smoke.
- `docs/developer/campaign-agent-llm.md`: one line that UX hosts the chat; provider switch unchanged.
- `docs-impact` / `graph-impact` on the implementation change set.

---

## 9. Implementation order

A plan under `docs/plans/` is written only after this spec is approved. Construction: say `execute` plus an intent name; `journeys-plan-to-aidlc` starts `/aidlc classic`. Linear unit issues land before any `Journeys.*` code.

1. Allowlist / URL builder + Vitest for the stream path.
2. Next SSE proxy (lift EXP early-proxy, Journeys headers, local HTTPS).
3. Thin chat page + Campaigns **Agent** link; unlabeled copy.
4. Docs/graph as §8.
5. Live smoke against Ollama (G3).

---

## 10. Out of scope later (not forgotten)

Covered by `docs/specs/2026-09-16-Journeys-ux-campaigns-agent-design.md` when that spec is approved: New = builder + agent, kebab, wizard, copy/restore, AdminAudit on Campaign writes, resume banner, Live-only `[id]/agent`.
