# Design: Journeys.UX Campaigns IA (surgical lift, Journeys.API)

**Date:** 2026-09-18  
**Status:** Draft (implementation in progress; browser pass did not confirm Duplicate, Unpublish=Pause, or Resume)  
**Scope:** Lift the remaining EXP admin-web **Campaigns** information architecture into `Journeys.UX` (list, kebab, New Journey Builder + agent rail, wizard edit, Live-only agent route, archive, versions) and wire it **HTTP-only** to `Journeys.API`. Add Core **copy** and **restore**. Keep existing Campaign Agent **tool audit**. Reuse the shipped unlabeled `AgentChat`, with the API's full SSE data plane and a collapsed-by-default Campaign JSON inspector.  
**Supersedes:** `docs/specs/2026-09-16-Journeys-ux-campaigns-agent-design.md` (never approved; AdminAudit-on-Campaign-REST dropped from this increment).  
**Depends on:** `docs/specs/2026-09-14-Journeys-ux-loyalty-shell-design.md` (shell, auth, `journeysFetch`), `docs/specs/2026-09-17-Journeys-ux-campaign-agent-chat-design.md` (**already shipped** — SSE proxy + thin chat; this spec extends it), `docs/specs/2026-09-16-Journeys-campaign-agent-ollama-design.md` (API LLM provider). Existing `CampaignController`, `CampaignAgentController` SSE (`started` / `progress` / `mcp` / `workflow` / `delta` / `done` | `error`), `CampaignAdapter` Draft/Live/Pause/Archive, `CampaignAgentToolAudit*`.  
**Placement authority:** `docs/product/` (`campaign.md`, `draft-live.md`, `journey.md`, `author-campaign.md`) and `AGENTS.md`.  
**If this spec and onion rules disagree:** `Journeys.UX` stays HTTP-only. No project-reference to Core/DAL/Infra. No BFF. Writes go through `Journeys.API` → Core.  
**If this spec and `docs/platform/security.md` disagree on Auth0 `"hayward"`:** security.md wins; never treat `"hayward"` as `TenantId`.  
**If this spec and `draft-live.md` disagree on status moves:** ontology + `CampaignAdapter` win. EXP kebab labels are remapped (see §5).

This spec **replaces** the shell NonGoal “Campaign Agent UI / Journey Builder / mutating campaigns” for everything still missing after the 2026-09-17 chat increment. It does **not** invent capability ids: `campaigns`, `journeys`, `campaign-agent`, `mcp-api` already exist.

The in-product name is **agent** (unlabeled). Do not ship “Campaign Coach”, product names, or coach copy. UI strings: “Agent”, “Open in Agent”, “Resume agent”, “Campaign JSON”.

---

## 1. Problem and goals

Journeys.UX can list campaigns (read-only cards as a plain list) and run an unlabeled Agent chat against `Journeys.API`. Operators still construct and mutate programs in deprecated EXP `admin-web`. Most Campaign REST already exists. Gaps: **copy** (EXP is a client composite that strips ids) and **restore** (Archive is immutable; cannot save-as-draft on the archive row). The shipped chat ignores `progress` / `mcp` / `workflow` and never sends `linkedCampaignId`, so the builder cannot stay in sync with the running agent.

| # | Goal | Success criterion |
|---|------|-------------------|
| G1 | **Full Campaigns IA** | Routes in §6 exist in `Journeys.UX` and behave as specified, sequenced by slices in §12 |
| G2 | **New = builder + agent** | `/loyalty/campaigns/new` is Journey Builder with the unlabeled `AgentChat` rail (EXP New shape, no Coach chrome) |
| G3 | **List kebab = EXP set, Journeys lifecycle** | Edit, Duplicate, Versions, Publish/Unpublish, Archive, Restore, Delete (Draft-only). **Confirm before Live.** Agent kebab item **Live-only** |
| G4 | **Edit = wizard** | `/loyalty/campaigns/[id]` always opens the wizard. **Agent kebab is Live-only** (Draft uses Edit / New, not `/[id]/agent`). Agent **thread** resume is the list link / `?conversationId=` |
| G5 | **HTTP-only, no BFF** | All reads/writes through `journeysFetch` → `Journeys.API`. Secrets stay on the Next server |
| G6 | **Copy is Core** | `POST api/Campaign/{tenantId}/{campaignId}/copy` creates a new Draft program (new `Id`, **new** `ExtCampaignId`, name `{name} Copy` unless overridden). UX does not strip-and-save locally |
| G7 | **Agent data plane** | Stream sends `conversationId`, `linkedCampaignId` (once known), `clientMessageId`. Client parses `progress`, `mcp`, `workflow`, `delta`, `done`, `error`. Discovered campaign id hydrates the builder. Collapsed “Campaign JSON” inspector shows GET campaign |
| G8 | **Tenant + model** | `JOURNEYS_TENANT_ID` / session tenant only. Campaigns still omit client `modelId` (`CampaignAdapter` `CAMPAIGN_MODEL_ID`). Never `modelId` `"unknown"`. Model type `loyalty` |
| G9 | **Journeys look, EXP files** | Keep EXP campaigns/builder/wizard **folder layout**. Tailwind + shadcn **primitives** with a **neutral** Journeys theme. Do not copy EXP brand/sidebar/Coach tokens |

### Non-goals (this spec)

- Other Loyalty nav (promotions, analytics, accounts mutations, model builder)
- EXP “Promotions inventory” button on the campaigns list
- AdminAudit on Campaign REST (`X-Journeys-Audit` not required on campaign save/copy/restore/delete this increment)
- Playwright e2e, EXP permission catalog, Prisma, `@exp/prisma-*`
- New capability ids or a second graph
- Per-tenant Auth0 / treating `"hayward"` as `TenantId`
- Changing LLM provider selection (Ollama spec already shipped)
- A second agent audit store in Next
- MCP tool for copy in this increment (REST + Core is enough)
- Forking the EXP monorepo into this tree as a workspace
- Tool-activity log, clear-session / clear-tenant panel, Coach chrome
- EXP `campaign_snapshot` SSE event (Journeys.API does not emit it; map `mcp` / `workflow` / `done` instead)

---

## 2. Decisions

| Topic | Choice |
|-------|--------|
| Vehicle | Refresh the 2026-09-16 Campaigns IA into this file (approach **1** — surgical lift + Journeys HTTP adapter) |
| New campaign | Journey Builder shell + `AgentChat` rail |
| Edit | Always wizard. Agent kebab **Live-only** |
| Agent UI | Reuse shipped thin transcript (`AgentChat`). Not the EXP Coach client |
| Agent protocol | Full Journeys.API SSE data plane + builder hydrate + dirty conflict (keep-local vs apply-agent) |
| Inspector | Expandable **Campaign JSON** disclosure, **collapsed by default**, GET body only |
| Styling | Tailwind + campaigns shadcn/Radix subset; **neutral** tokens on the **whole** UX shell. No EXP `--color-brand-*`, `--font-brand`, purple sidebar, Coach copy |
| List kebab | Full EXP set; **confirm dialog before Publish to Live**; Delete Draft-only |
| APIs | Journeys.API is the contract. Add **copy** and **restore**. Remap Unpublish onto **Pause**. No AdminAudit on these writes this increment |
| Lift | Surgical from `temp/exp/apps/admin-web` + remap paths, types, copy, unlabeled agent strings |
| Duplicate | New **program**: Core copy strips `Id` and `ExtCampaignId`, status `draft`, name `{source.Name} Copy` |
| Unpublish | **Pause** (`status: pause` on the **same** `Id`). Not Live → Draft |
| Publish | Draft → Live or Pause → Live via `POST .../save` with `status: live`. UI confirm first. Agent Live still uses `LivePromotionGuard` |
| Restore | Do **not** mutate Archive. Core creates a **new Draft** (`new Id`, **same** `ExtCampaignId`) if no Draft exists for that ext id |
| Edit Live journey | Wizard cannot upsert Live journey in place. Load existing Draft for that `ExtCampaignId` if any; otherwise Save Draft issues a **new Id**, same `ExtCampaignId` |
| Resume | Single list link from `GET .../campaign-agent/conversations` (latest thread) → `/loyalty/campaigns/agent?conversationId=`. Hide the link when the list is empty. No conversation-manager UI |
| Logging | Serilog / Next structured `TenantId`, `campaignId`, `conversationId`, `userId`, action. No secrets, JWT, full campaign JSON, prompts, or event payloads |
| Commit of this spec | Human only |

---

## 3. Placement

```
C:\Dev\Journeys\Journeys\
  Journeys.UX/                  # Next.js — HTTP client
    src/app/loyalty/campaigns/  # list, new, [id], [id]/agent, agent, archived, versions
    src/app/api/loyalty/campaign-agent/messages/stream/  # already shipped; body gains linkedCampaignId
    src/components/loyalty/     # lifted campaigns + journey builder; existing agent-chat
    src/components/ui/          # shadcn primitives used by lifted trees only
    src/lib/map-loyalty-path.ts # allowlist expansion
  Journeys.API/Controllers/CampaignController.cs   # copy + restore
  Journeys.Core/                # CopyCampaign / RestoreArchivedToDraft
  Journeys.Tests/               # copy, restore
  docs/specs/                   # this file
```

Lift source: `C:\Dev\Journeys\temp\exp\apps\admin-web` (campaigns pages, wizard, journey builder, TanStack Query usage in those trees, SSE client already lifted). Rewrite `@exp/shared-types` / BFF / Prisma / permission guards to local types, session, and `journeysFetch`.

Do **not** lift: `CAMPAIGN_COACH_*` copy, clear-session panel, promotions CTA, EXP `globals.css` brand/sidebar tokens, charts, apex, cmdk, Prisma adapters.

`Journeys.UX` may depend on **HTTP** `Journeys.API` only.

---

## 4. Architecture and data flow

```
Browser (React Query + builder/wizard + AgentChat)
  → Next server actions / allowlisted App Router routes (session required)
      → journeysFetch + envelope { success, data, error }
          headers: Bearer | Journeys-API-KEY
          → Journeys.API
              Campaign REST → CampaignService → CampaignAdapter (partition = status)
              POST .../copy and POST .../restore (new, Core)
              Campaign Agent SSE → orchestrator → MCP tools → same Core
              CampaignAgentToolAudit* → blob (hashes, not secrets)
  → POST /api/loyalty/campaign-agent/messages/stream
      { message, conversationId?, linkedCampaignId?, clientMessageId? }
      ← text/event-stream
         started | progress | mcp | workflow | delta | done | error
```

**SSE:** keep the shipped Next proxy. Do not put Anthropic/Ollama keys or `Journeys-API-KEY` in the browser. Keep `conversationId` from `started`; on `error`, show the thread error and **do not** drop the id or `linkedCampaignId`. Ignore unknown event names.

**Journeys.API does not emit** `campaign_snapshot`. When `mcp`, `workflow`, `done`, or tool-shaped payloads contain a campaign id: GET that campaign (Draft first); on **New**, hydrate the Journey Builder (if `isDirty`, keep-local vs apply-agent — never silent overwrite); on **Live `/[id]/agent` and standalone `/agent`**, update `linkedCampaignId` and the inspector only (no builder store).

**Allowlist:** fail closed. Expand `mapLoyaltyPath` for every path in §5. Unknown paths do not proxy.

**Tenant:** `JOURNEYS_TENANT_ID` when set wins over a stale session (same as the shell). Never Auth0 `"hayward"`.

**Filters:** lift `CampaignFilters`; filtered list uses `POST api/Campaign/{tenantId}/getmany` (existing). Unfiltered list stays `getall`.

**Theme:** add Tailwind v4 + shadcn (neutral base, system-ui) to `Journeys.UX` including nav, Overview, Accounts, and Agent so Campaigns is not a second look. Existing `.agent-chat` layout may move to Tailwind utilities. Do not import EXP brand CSS variables.

---

## 5. API contract (Journeys.API)

Existing (UX maps EXP-shaped paths onto these):

| UX need | Journeys.API |
|---------|----------------|
| List | `POST api/Campaign/{tenantId}/getall` |
| Filtered list | `POST api/Campaign/{tenantId}/getmany` |
| Get one | `GET api/Campaign/{tenantId}/{campaignId}` (+ status query as today) |
| Save / publish / pause / archive | `POST api/Campaign/{tenantId}/save` |
| Delete | `DELETE api/Campaign/{tenantId}/{campaignId}?status=` (`CampaignDeleteGuard`: Draft never deployed only) |
| Validate | `POST api/Campaign/{tenantId}/validate` |
| Versions | `GET api/Campaign/{tenantId}/versions/{extCampaignId}` |
| Archived list | `GET api/Campaign/{tenantId}/archived` |
| Live / Draft by ext | `GET .../live/{extCampaignId}`, `GET .../draft/{extCampaignId}` |
| Event models for wizard | existing `POST api/Model/{tenantId}/GetMany` (already allowlisted as schemas/model/all) |
| Point account types (outcome editors) | `POST api/Campaign/{tenantId}/pointaccounttype/getall` |
| Agent threads | `GET api/v1/{tenantId}/campaign-agent/conversations` (allowlisted JSON via `journeysFetch` or a dedicated App Router GET — not an open HTTP tunnel) |
| Agent turn | shipped Next SSE proxy; body `{ message, conversationId?, linkedCampaignId?, clientMessageId? }` |

Do **not** add `getCampaignStats` unless a lifted campaigns file actually calls it (`CampaignCard` does not). Do not add point-account **upsert** (settings screen; out of scope).

**New — copy**

`POST api/Campaign/{tenantId}/{campaignId}/copy`  
Query: source `status` (required). Body optional `{ "name": "..." }`.

Core:

1. Fetch source by id + status.
2. New `Campaign`: new `Id`, **omit** `ExtCampaignId` (Core fill from name per `campaign.md`), `status: draft`, name = body name or `{source.Name} Copy`, copy journey/rules/events/dates.
3. Upsert Draft. One-Draft-per-ext still holds because this is a **new** program.
4. Return the new Draft DTO.

This is **not** “open a Draft of the same Live program.” That path is wizard Save with **same** `ExtCampaignId` and a **new** `Id`.

**New — restore (Archive is immutable)**

`POST api/Campaign/{tenantId}/{campaignId}/restore` with source status `archive`.

Core: if a Draft already exists for that `ExtCampaignId`, `APIErrorsException`. Else new Draft, **same** `ExtCampaignId`, new `Id`, copy payload from Archive. Archive row unchanged. Do not POST `status: draft` onto an Archive document.

**Kebab → save status (do not send EXP’s Live→Draft):**

| Kebab | Request |
|-------|---------|
| Publish (after confirm) | save `status: live` (Draft or Pause id) |
| Unpublish | save `status: pause` on the Live id |
| Archive | save `status: archive` on Draft or Live/Pause per adapter scenarios |
| Restore | restore endpoint above, not save-as-draft on archive id |
| Delete | DELETE Draft only; UI hides otherwise; API still guards |

Controllers: validate, call services, map HTTP. Copy/restore/lifecycle rules live in Core.

**Audit this increment:** do **not** add `IAdminAuditService` to `CampaignController`. Keep `CampaignAgentToolAudit*`. UX must still pass `conversationId` and `linkedCampaignId` so blob rows stay joinable. Next does not write a parallel audit file. Mutating `journeysFetch` does **not** need `X-Journeys-Audit` for Campaign REST in this spec (Campaign Agent SSE may still send it when using API-key sessions, as today).

---

## 6. Pages and IA

| Href | Role |
|------|------|
| `/loyalty/campaigns` | Cards + filters + New + Archived + **resume agent** link. Kebab per §2 |
| `/loyalty/campaigns/new` | Journey Builder + unlabeled `AgentChat` rail + Campaign JSON disclosure when linked |
| `/loyalty/campaigns/agent` | Agent-only. Optional `?conversationId=` to resume. Inspector after a campaign id is discovered |
| `/loyalty/campaigns/[id]` | **Wizard** (Edit). `?campaignStatus=` required to fetch the partition |
| `/loyalty/campaigns/[id]/agent` | `AgentChat` linked to that campaign (`linkedCampaignId`). **Route + kebab only for Live**. The agent still cannot upsert Live journey in place: it authors a new Draft (same `ExtCampaignId`) or uses `LivePromotionGuard` for promote |
| `/loyalty/campaigns/archived` | Archived list + restore |
| `/loyalty/campaigns/versions/[extCampaignId]` | Version history |

Card title → wizard (`[id]?campaignStatus=`). New CTA → `/loyalty/campaigns/new`.

Kebab visibility:

- **Edit** — all statuses that can be fetched
- **Agent** — Live only
- **Duplicate** — all (copy API)
- **Versions** — when `extCampaignId` present
- **Publish** — Draft or Pause; **AlertDialog confirm** (“Publish to Live?”). Cancel = no-op
- **Unpublish** — Live → Pause
- **Archive** — not already Archive/Draft-only as in EXP card (`!archived && !draft`)
- **Restore** — Archive
- **Delete** — Draft only + confirm (existing EXP delete dialog)

Status badge must show **pause** (EXP treated some live as `active`; map `active` → `live` on parse if a lifted type still uses it). Persist lowercase `live` / `draft` / `archive` / `pause`.

Drop `PermissionGuard` and EXP `PageWrapper` branding. Session on `/loyalty/*` is the gate. A thin title + breadcrumb is enough.

---

## 7. Components (isolation)

| Unit | Does | Used how | Depends on |
|------|------|----------|------------|
| `CampaignsClient` + `CampaignCard` + filters | List, kebab, confirms | `/loyalty/campaigns` | campaign actions |
| Resume link | Latest `conversationId` → `/agent?conversationId=` | List | `GET .../conversations` |
| Journey Builder shell | Graph authoring on New | `/new` | save/validate + `AgentChat` + hydrate hook |
| `AgentChat` | Thin transcript + Send + one-line `progress.label`; accepts `linkedCampaignId`, `conversationId` | New, `/agent`, Live `/[id]/agent` | SSE proxy |
| Agent SSE reducer | `started`/`progress`/`mcp`/`workflow`/`delta`/`done`/`error`; extract campaign id | `AgentChat` + hydrate | lifted extract-id helper |
| Builder hydrate hook | GET Draft, hydrate store, dirty conflict | New | `getCampaign`, builder store |
| Campaign JSON disclosure | Collapsed `<details>`; pretty GET JSON + copy | New, `/agent`, Live agent when id known | `getCampaign` |
| `CampaignWizard` | Step edit | `/[id]` | get/save/validate, PAT getall, schemas |
| Archived + versions clients | Lists | those routes | archived/versions/restore |
| `mapLoyaltyPath` / `journeysFetch` | Allowlist + envelope | all loyalty actions | session, env |
| `CampaignController` copy/restore | HTTP | UX | Core |
| `CampaignService` copy/restore | Lifecycle | controller | adapter, one-Draft-per-ext, delete guard |
| Tool audit hosted service | Blob MCP audit | agent turns | existing DI |

Each unit is testable without the others’ internals. Changing agent panel copy must not require a new capability id. Changing copy internals must not require the wizard to strip ids.

Bring in TanStack Query, toast, lucide, zustand, React Flow, and the shadcn primitives **only as imported by these trees**. Local Campaign zod/types — not `@exp/shared-types`. React Flow is allowed **only** for the journey canvas.

`AgentChat` remains unlabeled. Extend it with optional props; do not replace it with `campaign-agent-client.tsx`.

---

## 8. Errors, logging, audit

**UX errors**

| Failure | Response |
|---------|----------|
| 401/403 | Existing “not authorized / check tenant or key” |
| `APIErrorsException` (one Draft per ext, cannot delete Live, Live immutable journey, cannot restore when Draft exists) | Toast field messages; refetch list |
| SSE `error` | Message in thread; keep `conversationId` and `linkedCampaignId` |
| Publish/Delete confirm cancel | No-op |
| Network / timeout on getall | Visible error; **no** fake empty list |
| Path not allowlisted | Fail closed |
| Live Agent URL for a non-Live campaign | Redirect or toast; do not open the Live agent route |
| Inspector GET fail | Error inside the disclosure; chat still works |
| Dirty builder vs agent hydrate | Keep-local / apply-agent; no silent overwrite |

**Logs** (`docs/developer/logging.md`): structured `TenantId`, `campaignId`, `conversationId`, `userId`, action. Next SSE proxy: status + duration only. **Never** API keys, JWT, full campaign JSON, event payloads, prompt text, or raw SSE bodies.

**Agent audit:** existing blob compact + preview ndjson (tenant, user, conversation, tool name, arg/result hashes, outcome). Unmatched tool calls stay `LogWarning`.

**Operator audit:** **out of this spec** for Campaign REST. Account/journey AdminAudit unchanged.

**Live gate:** list Publish confirm is the human approval for kebab. Agent Live still `LivePromotionGuard`.

---

## 9. Testing

**UX (Vitest)**

- Path map: getall, getmany, get, save, delete, validate, versions, archived, live/draft by ext, copy, restore, pointaccounttype/getall, campaign-agent conversations/stream.
- Kebab rules: Delete Draft-only; Agent Live-only; Publish confirm required before Live save; Unpublish sends `pause` not `draft`.
- Envelope parse matches Accounts. No `modelId` `"unknown"`. Tenant from env/session.
- SSE reducer: `progress` / `mcp` / `workflow` do not drop `conversationId`; campaign id extracted from mcp-shaped JSON; unknown events ignored.
- Inspector: rendered collapsed when a campaign id exists; expand shows GET JSON (mock).
- Hydrate: dirty store does not auto-apply.

**API (`Journeys.Tests`)**

- Copy: new Draft, new id, new ext id, name suffix, journey copied; never two Drafts for one ext id because copy **changes** ext id.
- Restore: new Draft, **same** ext id; Archive unchanged; second restore fails if Draft exists.
- Keep `CampaignDeleteGuard` and `LivePromotionGuard` tests.
- Do **not** add Campaign AdminAudit assertions this spec.

**CI vs browser**

- `scripts/agent-verify.ps1` (or `aidlc-agent-verify-sensor.ps1`) on the C# blast radius + UX unit tests per slice.
- Browser (human or agent with browser tools): list kebab, New builder + SSE hydrate + collapsed inspector, wizard edit, Live confirm, Draft delete refusal, Pause unpublish, restore.
- LLM happy-path remains `docs/developer/campaign-agent-llm.md` (local). Not a required CI job.

**Out of scope for this spec’s tests:** live Anthropic/Ollama in pipeline; Neo4j; a second audit store; Playwright.

---

## 10. Graph and docs (extend)

Do **not** invent capability ids.

- `docs/product/graph/path-map.yaml`: `Journeys.UX` prefix nodes become `[campaigns, journeys, campaign-agent]` (`meaningOptional: false`).
- Keep `campaigns` `IMPLEMENTED_AS` `proj-core`; `campaign-agent` `IMPLEMENTED_AS` `proj-api` / `proj-agent`. Do **not** add `IMPLEMENTED_AS` `proj-ux`.
- `docs/developer/journeys-ux.md`: Campaigns are no longer read-only; list the routes, copy/restore, Pause-for-Unpublish, unlabeled agent data plane, inspector, no Campaign AdminAudit this increment.
- Pointer from `docs/developer/campaign-agent-llm.md` that UX hosts the agent rail and hydrates from SSE (API provider switch unchanged).
- Shell spec §12 “Campaign Agent / Journey Builder” is fulfilled by this file plus the 2026-09-17 chat spec (do not rewrite that historical spec’s NonGoals except a one-line pointer if docs-impact requires it).
- `docs-impact` / `graph-impact` must pass on the **implementation** change set (not waived). Writing this spec file alone does not require a graph change.

---

## 11. Error-handling and security invariants

- Session required on `/loyalty/*`.
- Allowlist fail-closed.
- `CampaignDeleteGuard` remains the delete authority.
- Anonymous Campaign Agent SSE stays Development-only (`CampaignAgent:AllowAnonymousUnauthenticatedAccess`); production UX always sends JWT or API key.
- Do not log or persist secrets in UX.
- Inspector shows the campaign DTO from GET only — never stream frames or LLM text.

---

## 12. Implementation order (slices)

A separate plan under `docs/plans/` is written only after **this spec** is approved. Construction: say `execute` plus an intent name; `journeys-plan-to-aidlc` starts `/aidlc classic`. Linear unit issues land before any `Journeys.*` code.

1. **Neutral Tailwind on the shell + allowlist + campaign actions** — map all §5 paths; no Campaign REST audit header.
2. **API copy + restore** — Core + controller + tests; no UX yet beyond what slice 3 needs.
3. **List + kebab** — lift cards/filters; Publish confirm; Unpublish=Pause; Duplicate=copy API; Delete Draft-only; omit promotions CTA.
4. **Agent data plane** — `linkedCampaignId` / `clientMessageId`; parse `progress`/`mcp`/`workflow`; hydrate hook; collapsed inspector; resume query param + list link. Keep thin transcript.
5. **New = Journey Builder + AgentChat rail**.
6. **Wizard edit** — `/loyalty/campaigns/[id]`; Live journey → new Draft same ext id.
7. **Archive + versions** — archived page + restore; versions page.
8. **Docs/graph** as §10. Browser pass on the flows in §9.

Do not skip slice 2 before kebab Duplicate/Restore. Do not ship Coach-branded strings. Do not skip slice 4 before treating New as “done” (builder without hydrate is an incomplete agent).

---

## 13. Out of scope later (not forgotten)

- AdminAudit on Campaign save/copy/restore/delete
- Enable other Loyalty nav items (one spec each)
- MCP `copy_campaign` tool wrapping the same Core method
- Tool-activity stream UI; clear-session panel
- Playwright after the builder is back
- Account detail / deposits (already have AdminAudit on points)
- Per-tenant Auth0
