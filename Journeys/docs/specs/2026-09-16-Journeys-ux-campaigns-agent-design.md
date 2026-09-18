# Design: Journeys.UX Campaigns IA + unlabeled agent

**Date:** 2026-09-16  
**Status:** Superseded  
**Superseded by:** `docs/specs/2026-09-18-Journeys-ux-campaigns-ia-design.md`  
**Scope:** Lift the EXP admin-web **Campaigns** information architecture into `Journeys.UX` (list, kebab mutations, New Journey Builder + agent panel, wizard edit, Live-only agent item, archive, versions) and wire it **HTTP-only** to `Journeys.API`. Fill the one first-class API gap (**copy**). Add **AdminAudit** on campaign writes. Keep existing Campaign Agent **tool audit**.  
**Depends on:** `docs/specs/2026-09-14-Journeys-ux-loyalty-shell-design.md` (shell, auth, `journeysFetch`), `docs/specs/2026-09-16-Journeys-campaign-agent-ollama-design.md` (LLM provider; API-only — this spec adds the UI). Existing `CampaignController`, `CampaignAgentController` SSE, `CampaignAdapter` Draft/Live/Pause/Archive, `IAdminAuditService`, `CampaignAgentToolAudit*`.  
**Placement authority:** `docs/product/` (`campaign.md`, `draft-live.md`, `journey.md`, `author-campaign.md`) and `AGENTS.md`.  
**If this spec and onion rules disagree:** `Journeys.UX` stays HTTP-only. No project-reference to Core/DAL/Infra. No BFF. Writes go through `Journeys.API` → Core.  
**If this spec and `docs/platform/security.md` disagree on Auth0 `"hayward"`:** security.md wins; never treat `"hayward"` as `TenantId`.  
**If this spec and `draft-live.md` disagree on status moves:** ontology + `CampaignAdapter` win. EXP kebab labels are remapped (see §5).

This spec **replaces** the shell NonGoal “Campaign Agent UI / Journey Builder / mutating campaigns.” It does **not** invent capability ids: `campaigns`, `journeys`, `campaign-agent`, `mcp-api` already exist.

The in-product name is **agent** (unlabeled). Do not ship “Campaign Coach”, product names, or coach copy. UI strings: “Agent”, “Open in Agent”, “Resume agent”.

---

## 1. Problem and goals

Journeys.UX can list campaigns (read-only). Operators still construct and mutate programs in deprecated EXP `admin-web`. The API already has Campaign REST, Campaign Agent SSE (`started` / `delta` / `done` | `error`), MCP tools on the same Core path, and blob tool-audit. Duplicate in EXP is a **client composite** (strip ids, save Draft); Journeys has no copy endpoint. Campaign REST save/delete is **not** on `IAdminAuditService` today (account point moves are).

| # | Goal | Success criterion |
|---|------|-------------------|
| G1 | **Full Campaigns IA** | Routes in §6 exist in `Journeys.UX` and behave as specified, sequenced by slices in §12 |
| G2 | **New = builder + agent** | `/loyalty/campaigns/new` is Journey Builder with the unlabeled agent panel (EXP New shape). No separate branded coach product |
| G3 | **List kebab = EXP set, Journeys lifecycle** | Edit, Duplicate, Versions, Publish/Unpublish, Archive, Restore, Delete (Draft-only). **Confirm before Live.** Agent kebab item **Live-only** |
| G4 | **Edit = wizard** | `/loyalty/campaigns/[id]` always opens the wizard. Drafts resume via New or the list resume banner, not an Agent kebab on Draft |
| G5 | **HTTP-only, no BFF** | All reads/writes through `journeysFetch` → `Journeys.API`. Secrets stay on the Next server |
| G6 | **Copy is Core** | `POST api/Campaign/{tenantId}/{campaignId}/copy` creates a new Draft program (new `Id`, **new** `ExtCampaignId`, name `{name} Copy` unless overridden). UX does not strip-and-save locally |
| G7 | **Audit** | Agent MCP calls keep blob **tool audit**. Campaign kebab/wizard/builder/copy/delete persist **AdminAudit** via `X-Journeys-Audit` |
| G8 | **Tenant + model** | `JOURNEYS_TENANT_ID` / session tenant only. Campaigns still omit client `modelId` (`CampaignAdapter` `CAMPAIGN_MODEL_ID`). Never `modelId` `"unknown"`. Model type `loyalty` |

### Non-goals (this spec)

- Other Loyalty nav (promotions, analytics, accounts mutations, model builder)
- EXP “Promotions inventory” button on the campaigns list (promotions screen is still disabled)
- Playwright e2e, EXP permission catalog, Prisma, `@exp/prisma-*`
- New capability ids or a second graph
- Per-tenant Auth0 / treating `"hayward"` as `TenantId`
- Changing LLM provider selection (Ollama spec already shipped)
- A second agent audit store in Next
- MCP tool for copy in this increment (REST + Core is enough; MCP can call the same Core later)
- Forking the EXP monorepo into this tree as a workspace

---

## 2. Decisions

| Topic | Choice |
|-------|--------|
| IA | Entire EXP `/loyalty/campaigns` surface, planned as sequenced slices (not a single dump) |
| New campaign | **A** — agent: Journey Builder shell + agent panel |
| List kebab | **A** — full EXP set; **confirm dialog before Publish to Live**; Delete Draft-only |
| APIs | Journeys.API is the contract. Add **copy**. Remap Unpublish/Restore onto existing partitions (no BFF composites that violate `draft-live.md`) |
| Edit | **B** — always wizard. Agent kebab **Live-only**. Draft threads continue via New / resume banner / `/loyalty/campaigns/agent` |
| Lift | **1** — surgical lift from `temp/exp/apps/admin-web` + remap paths, types, copy, and unlabeled agent strings |
| Agent name | Unlabeled **agent**. Strip EXP `CAMPAIGN_COACH_*` product copy on lift |
| Duplicate | New **program**: Core copy strips `Id` and `ExtCampaignId`, status `draft`, name `{source.Name} Copy` |
| Unpublish | **Pause** (`status: pause` on the **same** `Id`). Not Live → Draft (adapter forbids that) |
| Publish | Draft → Live or Pause → Live via `POST .../save` with `status: live`. UI confirm first. Agent Live still uses `LivePromotionGuard` |
| Restore | Do **not** mutate Archive. Core creates a **new Draft** (`new Id`, **same** `ExtCampaignId`) if no Draft exists for that ext id |
| Edit Live journey | Wizard cannot upsert Live journey in place. Load existing Draft for that `ExtCampaignId` if any; otherwise Save Draft issues a **new Id**, same `ExtCampaignId` |
| Logging | Serilog structured `TenantId`, `campaignId`, `conversationId`, `userId`, action. No secrets, JWT, full campaign JSON, prompts, or event payloads |
| Commit of this spec | Human only |

---

## 3. Placement

```
C:\Dev\Journeys\Journeys\
  Journeys.UX/                  # Next.js — HTTP client
    src/app/loyalty/campaigns/  # list, new, [id], [id]/agent, agent, archived, versions
    src/components/loyalty/     # lifted campaigns + journey builder + agent panel
    src/lib/map-loyalty-path.ts # allowlist expansion
  Journeys.API/Controllers/CampaignController.cs   # copy + AdminAudit on writes
  Journeys.Core/                # CopyCampaign / RestoreArchivedToDraft
  Journeys.Tests/               # copy, restore, admin audit
  docs/specs/                   # this file
```

Lift source: `C:\Dev\Journeys\temp\exp\apps\admin-web` (campaigns pages, wizard, journey builder, agent panel, TanStack Query, SSE client, UI kit subset). Rewrite `@exp/shared-types` / BFF / Prisma / permission guards to local types, session, and `journeysFetch`.

`Journeys.UX` may depend on **HTTP** `Journeys.API` only.

---

## 4. Architecture and data flow

```
Browser (React Query + builder/wizard/agent panel)
  → Next server actions / allowlisted App Router routes (session required)
      → journeysFetch + envelope { success, data, error }
          headers: Bearer | Journeys-API-KEY
          mutating: X-Journeys-Audit JSON (adminUserId from session; never empty on write)
          → Journeys.API
              Campaign REST → CampaignService → CampaignAdapter (partition = status)
              Campaign Agent SSE → orchestrator → MCP tools → same Core
              CampaignAgentToolAudit* → blob (hashes, not secrets)
              IAdminAuditService on Campaign save/copy/delete/restore
  ← page / SSE (started, delta, done | error)
```

**SSE:** Next proxies `POST api/v1/{tenantId}/campaign-agent/messages/stream` as `text/event-stream`. Do not put Anthropic/Ollama keys or `Journeys-API-KEY` in the browser. Keep `conversationId` from `started`; on `error`, show the thread error and **do not** drop the id.

**Allowlist:** fail closed. Expand `mapLoyaltyPath` (and the fetch helper) for every path in §5. Unknown paths do not proxy.

**Tenant:** `JOURNEYS_TENANT_ID` when set wins over a stale session (same as the shell). Never Auth0 `"hayward"`.

**Filters:** lift `CampaignFilters`; filtered list uses `POST api/Campaign/{tenantId}/getmany` (existing). Unfiltered list stays `getall`.

---

## 5. API contract (Journeys.API)

Existing (UX maps EXP-shaped paths onto these):

| UX need | Journeys.API |
|---------|----------------|
| List | `POST api/Campaign/{tenantId}/getall` |
| Filtered list | `POST api/Campaign/{tenantId}/getmany` |
| Get one | `GET api/Campaign/{tenantId}/{campaignId}` (+ status query as today) |
| Save / publish / pause / archive Draft | `POST api/Campaign/{tenantId}/save` |
| Delete | `DELETE api/Campaign/{tenantId}/{campaignId}?status=` (`CampaignDeleteGuard`: Draft never deployed only) |
| Versions | `GET api/Campaign/{tenantId}/versions/{extCampaignId}` |
| Archived list | `GET api/Campaign/{tenantId}/archived` |
| Live / Draft by ext | `GET .../live/{extCampaignId}`, `GET .../draft/{extCampaignId}` |
| Validate | `POST api/Campaign/{tenantId}/validate` |
| Agent threads | `GET api/v1/{tenantId}/campaign-agent/conversations` |
| Agent turn | `POST api/v1/{tenantId}/campaign-agent/messages/stream` body `{ message, conversationId?, linkedCampaignId?, clientMessageId? }` |
| Agent clear | `POST .../clear/session`, `POST .../clear/tenant` |

**New — copy**

`POST api/Campaign/{tenantId}/{campaignId}/copy`  
Query: source `status` (required). Body optional `{ "name": "..." }`.

Core:

1. Fetch source by id + status.
2. New `Campaign`: new `Id`, **omit** `ExtCampaignId` (Core fill from name per `campaign.md`), `status: draft`, name = body name or `{source.Name} Copy`, copy journey/rules/events/dates.
3. Upsert Draft. One-Draft-per-ext still holds because this is a **new** program.
4. AdminAudit, then return the new Draft DTO.

This is **not** “open a Draft of the same Live program.” That path is wizard Save with **same** `ExtCampaignId` and a **new** `Id`.

**New — restore (Archive is immutable)**

`POST api/Campaign/{tenantId}/{campaignId}/restore` with source status `archive` (or Core method invoked from save if a dedicated route is cleaner — pick **dedicated restore**; do not POST `status: draft` onto an Archive document).

Core: if a Draft already exists for that `ExtCampaignId`, `APIErrorsException`. Else new Draft, **same** `ExtCampaignId`, new `Id`, copy payload from Archive. Archive row unchanged.

**Kebab → save status (do not send EXP’s Live→Draft):**

| Kebab | Request |
|-------|---------|
| Publish (after confirm) | save `status: live` (Draft or Pause id) |
| Unpublish | save `status: pause` on the Live id |
| Archive | save `status: archive` on Draft or Live/Pause per adapter scenarios |
| Restore | restore endpoint above, not save-as-draft on archive id |
| Delete | DELETE Draft only; UI hides otherwise; API still guards |

Controllers: validate, call services, map HTTP. Copy/restore/lifecycle rules live in Core.

**AdminAudit (add):** `CampaignController` save, copy, restore, delete call `IAdminAuditService.AuditOperation` like `AccountController` deposit. Pretty-print includes campaign id, ext id, from-status → to-status. Rollback the audit row if the write throws. Require `X-Journeys-Audit` on those actions (`auditRequired: true`). UX mutating `journeysFetch` always sends it from the session user.

**Tool audit (keep):** do not disable `CampaignAgentToolAudit*`. UX must pass `conversationId` and `linkedCampaignId` so blob rows stay joinable. Next does not write a parallel audit file.

---

## 6. Pages and IA

| Href | Role |
|------|------|
| `/loyalty/campaigns` | Cards + filters + New + Archived + **resume agent** banner. Kebab per §2 |
| `/loyalty/campaigns/new` | Journey Builder + unlabeled agent panel |
| `/loyalty/campaigns/agent` | Agent-only (resume / new thread without requiring a campaign id) |
| `/loyalty/campaigns/[id]` | **Wizard** (Edit). `?campaignStatus=` required to fetch the partition |
| `/loyalty/campaigns/[id]/agent` | Agent panel linked to that campaign. **Route + kebab only for Live** (Draft uses New/resume). The agent still cannot upsert Live journey in place: it authors a new Draft (same `ExtCampaignId`) or uses `LivePromotionGuard` for promote |
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

---

## 7. Components (isolation)

| Unit | Does | Used how | Depends on |
|------|------|----------|------------|
| `CampaignsClient` + `CampaignCard` | List, filters, kebab, confirms | `/loyalty/campaigns` | campaign actions |
| Resume banner | Link to last `conversationId` | List | `GET .../conversations` or session storage of last id |
| Journey Builder shell | Graph authoring on New | `/new` | save/validate + agent panel |
| Unlabeled agent panel | SSE chat | New, `/agent`, Live `/[id]/agent` | SSE proxy |
| `CampaignWizard` | Step edit | `/[id]` | get/save/validate |
| Archived + versions clients | Lists | those routes | archived/versions/restore |
| `mapLoyaltyPath` / `journeysFetch` | Allowlist + envelope + audit header | all loyalty actions | session, env |
| `CampaignController` copy/restore + audit | HTTP | UX + future MCP | Core |
| `CampaignService` copy/restore | Lifecycle | controller | adapter, delete guard |
| Tool audit hosted service | Blob MCP audit | agent turns | existing DI |
| `Journeys.API` Campaign Agent | SSE + MCP | proxy | Core, LLM factory |

Each unit is testable without the others’ internals. Changing agent panel copy must not require a new capability id. Changing copy internals must not require the wizard to strip ids.

Bring in TanStack Query, the campaigns UI kit subset, and the SSE client **only as needed** for these routes.

---

## 8. Errors, logging, audit

**UX errors**

| Failure | Response |
|---------|----------|
| 401/403 | Existing “not authorized / check tenant or key” |
| `APIErrorsException` (one Draft per ext, cannot delete Live, Live immutable journey, cannot restore when Draft exists) | Toast field messages; refetch list |
| SSE `error` | Message in thread; keep `conversationId` |
| Publish/Delete confirm cancel | No-op |
| Network / timeout on getall | Visible error; **no** fake empty list |
| Path not allowlisted | Fail closed |
| Live Agent URL for a non-Live campaign | Redirect or toast; do not open the Live agent route |

**Logs** (`docs/developer/logging.md`): structured `TenantId`, `campaignId`, `conversationId`, `userId`, action. Next SSE proxy: status + duration only. **Never** API keys, JWT, full campaign JSON, event payloads, or prompt text.

**Agent audit:** existing blob compact + preview ndjson (tenant, user, conversation, tool name, arg/result hashes, outcome). Unmatched tool calls stay `LogWarning`.

**Operator audit:** AdminAudit rows for copy, save (including publish/unpublish/archive), restore, delete. Failed write → `RollbackAudit`.

**Live gate:** list Publish confirm is the human approval for kebab. Agent Live still `LivePromotionGuard`. Both produce AdminAudit on the resulting save.

---

## 9. Testing

**UX (Vitest)**

- Path map: getall, getmany, get, save, delete, versions, archived, live/draft by ext, copy, restore, campaign-agent conversations/stream/clear.
- Mutating calls send `X-Journeys-Audit` with `adminUserId`.
- Kebab rules: Delete Draft-only; Agent Live-only; Publish confirm required before Live save.
- Envelope parse matches Accounts. No `modelId` `"unknown"`. Tenant from env/session.

**API (`Journeys.Tests`)**

- Copy: new Draft, new id, new ext id, name suffix, journey copied; never two Drafts for one ext id because copy **changes** ext id.
- Restore: new Draft, **same** ext id; Archive unchanged; second restore fails if Draft exists.
- Save/delete/copy/restore write AdminAudit and roll back on failure.
- Keep `CampaignDeleteGuard` and `LivePromotionGuard` tests.
- Tool-audit recorder: hashes, no secret material in compact records.

**CI vs browser**

- `scripts/agent-verify.ps1` (or `aidlc-agent-verify-sensor.ps1`) + UX unit tests per slice.
- Browser (human or agent with browser tools): list kebab, New builder + SSE, wizard edit, Live confirm, Draft delete refusal, Pause unpublish, restore.
- LLM happy-path remains `docs/developer/campaign-agent-llm.md` (local). Not a required CI job.

**Out of scope for this spec’s tests:** live Anthropic/Ollama in pipeline; Neo4j; a second audit store.

---

## 10. Graph and docs (extend)

Do **not** invent capability ids.

- `docs/product/graph/path-map.yaml`: `Journeys.UX` prefix nodes become `[campaigns, journeys, campaign-agent]` (`meaningOptional: false`).
- Keep `campaigns` `IMPLEMENTED_AS` `proj-core`; `campaign-agent` `IMPLEMENTED_AS` `proj-api` / `proj-agent`. Do **not** add `IMPLEMENTED_AS` `proj-ux`.
- `docs/developer/journeys-ux.md`: Campaigns are no longer read-only; list the routes, copy/restore, Pause-for-Unpublish, unlabeled agent, audit headers.
- Pointer from `docs/developer/campaign-agent-llm.md` that UX now hosts the agent panel (API provider switch unchanged).
- Shell spec §12 “Campaign Agent / Journey Builder” is fulfilled by this file (do not rewrite that historical spec’s NonGoals except a one-line pointer if docs-impact requires it).
- `docs-impact` / `graph-impact` must pass on the implementation change set (not waived).

---

## 11. Error-handling and security invariants

- Session required on `/loyalty/*`.
- Allowlist fail-closed.
- `CampaignDeleteGuard` remains the delete authority.
- Anonymous Campaign Agent SSE stays Development-only (`CampaignAgent:AllowAnonymousUnauthenticatedAccess`); production UX always sends JWT or API key + audit user.
- Do not log or persist secrets in UX.

---

## 12. Implementation order (slices)

A separate plan under `docs/plans/` is written only after **this spec** is approved. Construction: say `execute` plus an intent name; `journeys-plan-to-aidlc` starts `/aidlc classic`. Linear unit issues land before any `Journeys.*` code.

1. **Allowlist + campaign actions + AdminAudit header** — map all §5 paths; mutating `journeysFetch` sends `X-Journeys-Audit`.
2. **API copy + restore + Campaign AdminAudit** — Core + controller + tests; no UX yet beyond what slice 3 needs.
3. **List + kebab** — lift cards/filters; Publish confirm; Unpublish=Pause; Duplicate=copy API; Delete Draft-only; omit promotions CTA.
4. **SSE proxy + unlabeled agent panel + resume** — `/loyalty/campaigns/agent` and list resume banner; Live `/[id]/agent`.
5. **New = Journey Builder + agent** — `/loyalty/campaigns/new`.
6. **Wizard edit** — `/loyalty/campaigns/[id]`; Live journey → new Draft same ext id.
7. **Archive + versions** — archived page + restore; versions page.
8. **Docs/graph** as §10. Browser pass on the flows in §9.

Do not skip slice 2 before kebab Duplicate/Restore. Do not ship Coach-branded strings.

---

## 13. Out of scope later (not forgotten)

- Enable other Loyalty nav items (one spec each)
- MCP `copy_campaign` tool wrapping the same Core method
- Playwright after the builder is back
- Account detail / deposits (already have AdminAudit on points)
- Per-tenant Auth0
