# Design: Journeys.UX → Backend.Model.UX model-builder handoff

**Date:** 2026-09-18  
**Status:** Approved (human 2026-09-18)  
**Scope:** Turn the disabled Loyalty **Model Builder** nav item into a new-tab open of **Backend.Model.UX** Modeler (`/models/coach`), same tenant, with a **one-time** Journeys event-signal brief (fixed `event-models` contract + tenant catalog snapshot from existing Model GetMany).  
**Depends on:** `docs/specs/2026-09-14-Journeys-ux-loyalty-shell-design.md` (nav, `journeysFetch`, `resolveTenantId`), `docs/product/ontology/event-model.md`. Existing `ModelController` `POST api/Model/{tenantId}/GetMany`. Backend product: `C:\Dev\Backend\Backend.Model.UX` (port 3145).  
**Placement authority:** `docs/product/` (`event-model.md`) and `AGENTS.md`.  
**If this spec and onion rules disagree:** `Journeys.UX` stays HTTP-only to `Journeys.API`. It does not call BackEnd.Web, does not iframe Model.UX, and does not become write authority for models.  
**If this spec and `event-model.md` disagree on required metadata or persist casing:** ontology + `EventService` / `WrappedEventPayload` win.

This spec **replaces** the shell NonGoal “Model Builder stays disabled” for the nav item only. It does **not** invent capability ids: `event-models` already exists. Do **not** add `IMPLEMENTED_AS` `proj-ux` on `event-models`.

---

## 1. Problem and goals

Operators author event models in **Backend.Model.UX** (Modeler chat → BackEnd.Web). Journeys.UX still shows **Model Builder** as a disabled span. Campaigns/wizard already **read** tenant models via GetMany. There is no Journeys form builder to port; EXP’s loyalty model wizard is out of scope. Backend Modeler has no Journeys event-signal primer today, so models often miss Wrapper / NaturalKey / AccountXId / TimeOfOccurrence.

| # | Goal | Success criterion |
|---|------|-------------------|
| G1 | **Nav opens Backend Modeler** | When `BACKEND_MODEL_UX_BASE_URL` is set, **Model Builder** is a live control that opens Model.UX `/models/coach` in a **new tab**. When unset, the item stays disabled |
| G2 | **Same tenant** | Open includes `?tenantId=` from `resolveTenantId` (`JOURNEYS_TENANT_ID` wins). Model.UX applies that id on load (not only localStorage / `mericantires` default) |
| G3 | **One-time Journeys brief** | That tab’s first Modeler turn receives the brief once. Opening Modeler from Model.UX’s own sidebar does not re-apply it |
| G4 | **HTTP-only Journeys** | Brief catalog comes from existing GetMany through `journeysFetch`. No BackEnd.Web calls from Journeys.UX. `ModelController` stays GetMany-only |
| G5 | **Safe payload** | Brief has no secrets, JWT, API keys, full campaign JSON, journey graphs, or `modelId` `"unknown"` |

### Non-goals (this spec)

- EXP `/loyalty/models/builder` form wizard or `/loyalty/accounts/builder`
- Iframe / rewrite / copy of Backend.Model.UX into this tree
- Model save/patch/delete on `Journeys.API`
- AdminAudit on Model writes
- Model.UX calling Journeys.API
- Data Explorer, Campaign Agent MCP `SaveModel`, or changing LLM providers
- New capability ids or `IMPLEMENTED_AS` `proj-ux` on `event-models`
- Playwright e2e, Prisma, PermissionGuard
- Invalidating `ModelCache` automatically after a Backend save (document restart / cache refresh only)

---

## 2. Decisions

| Topic | Choice |
|-------|--------|
| Vehicle | New-tab deep link + one-shot seed (approach A). Not iframe. Not a Journeys-hosted builder |
| Label | Keep nav copy **Model Builder** (existing disabled label) |
| Tenant | Query `tenantId` = `resolveTenantId` (lowercase). Model.UX persists it like today’s picker |
| Brief contents | (1) Fixed contract from `event-model.md`. (2) Tenant catalog rows from GetMany: id, name, status, tag, and which of `Wrapper`, `NaturalKeySymbols`, `AccountXIdSymbol`, `TimeOfOccurrence` are missing |
| Catalog filter | `modelType` loyalty (existing `getManyModelsListBody`). Include `tag === "eventable"` and `LoyaltyAccountDetails` so gaps on processable models are visible. Omit non-loyalty types |
| GetMany parse | Extend existing schema normalize to read `tag` / `Tag` and `modelMetaData` / `ModelMetaData` keys. No new API |
| Seed transport | Handoff is an HTML **form POST** (full navigation, new tab) to Model.UX `POST /api/model-builder/seed` with `tenantId`, `brief`, `source=journeys`. Model.UX stores the brief in `sessionStorage` and **303** to `/models/coach?tenantId=&source=journeys`. Inject as the first Modeler turn only when `source=journeys`. Do not put the brief in the query string. Form POST avoids CORS and does not call BackEnd.Web |
| GetMany failure | Still open Model.UX with **contract-only** brief plus one warning line (“Catalog snapshot unavailable”) |
| Env missing | Nav stays disabled. No 404 page required |
| Logging | TenantId + “model-builder-handoff”. No brief body, no catalog dump, no secrets |
| Commit of this spec | Human only |

---

## 3. Placement

```
C:\Dev\Journeys\Journeys\
  Journeys.UX/
    .env.example
    src/components/loyalty-nav.tsx
    src/app/loyalty/models/handoff/page.tsx          # thin: build brief, client open+POST
    src/lib/model-builder-brief.ts                   # pure brief builder
    src/lib/model-builder-brief.test.ts
    src/lib/model-builder-handoff.ts                 # base URL + open URL helpers
    src/services/loyalty/parse-list.ts               # tag + modelMetaData on schemas
    src/services/loyalty/actions.ts                  # reuse getAllSchemas (or equivalent GetMany)

C:\Dev\Backend\Backend.Model.UX\
  components/layout/tenant-context.tsx               # honor ?tenantId=
  app/api/model-builder/seed/route.ts                # POST brief → session cookie/storage handshake
  components/model-coach/model-coach-client.tsx      # inject first turn once

docs/developer/journeys-ux.md
docs/specs/2026-09-14-Journeys-ux-loyalty-shell-design.md   # note NonGoal replaced for nav only
```

`Journeys.API` / Core / DAL: **no** file changes.

---

## 4. Brief shape

Markdown (or equivalent structured text the Modeler treats as the first user message). Required sections:

1. **Source:** “Opened from Journeys.UX for tenant `{tenantId}`. Author event-signal (processable) models for this tenant.”
2. **Contract (fixed):**
   - Payload vs wrapper (`WrappedEventPayload.event` vs engine envelope).
   - Processable models need metadata: `Wrapper` (wrapper model id), `NaturalKeySymbols`, `AccountXIdSymbol` (payload-root path unless loyalty-account-shaped), `TimeOfOccurrence`.
   - Persist wrapper symbols lowercase: `naturalkey`, `timeofoccurrence`, `lastprocessed`, `accountid`, `appliedcampaigns`, `appliedrulesetids`, `providerstates`, `journeystates`, `outcomestates`.
   - `Campaign.Events` lists payload model **GUIDs**, not display names.
   - `modelType` `loyalty`. Processable payload models use tag `eventable`.
   - Never use `modelId` `"unknown"`.
3. **Catalog snapshot:** one line per included model: `name`, `id`, `status`, `tag`, `missing: […metadata keys…]` or `ok`.
4. **Optional warning** if GetMany failed.

Tests must assert the four metadata names appear in the contract section; a fixture model missing `Wrapper` is listed under `missing`; journey/campaign JSON keys are absent; `"unknown"` does not appear as a model id.

---

## 5. Runtime flow

```
Loyalty nav Model Builder
  → GET /loyalty/models/handoff  (session required, same as /loyalty/*)
  → server: resolveTenantId + GetMany + build brief
  → page: form POST (target=_blank) to {BACKEND_MODEL_UX_BASE_URL}/api/model-builder/seed
  → Model.UX: sessionStorage seed, 303 /models/coach?tenantId=&source=journeys
  → Modeler: consume seed once as first turn
```

If Model.UX is down, the new tab shows a browser network error. Do not silently fall back to a query-string brief. Operator can still open Model.UX manually; tenant will not be pre-seeded that way.

New tab: `form target="_blank"` + `rel`/`noopener` on the opener where the browser allows it.

---

## 6. Errors

| Case | Behavior |
|------|----------|
| No `BACKEND_MODEL_UX_BASE_URL` | Nav disabled |
| Not signed in | Existing `/loyalty/*` gate |
| GetMany error | Open with contract + warning line |
| Seed POST / Model.UX down | New tab fails visibly (browser error). No query-string brief fallback |
| Model.UX missing `source=journeys` | No brief injection |
| Stale ModelCache after save | Documented: restart API or wait for cache; not auto-invalidated this increment |

---

## 7. Tests

- `model-builder-brief.test.ts`: contract keys; missing-metadata fixture; no `"unknown"`; no journey blob.
- Nav: URL set → handoff href; URL unset → disabled span (source or render test).
- Parse-list: `tag` + metadata keys from PascalCase GetMany rows.
- Backend.Model.UX (in that repo): `?tenantId=` wins over default; seed injects once; second navigation without `source=journeys` does not inject.

`npm test` in Journeys.UX. No `Journeys.API` test changes.

---

## 8. Docs / graph

- `docs/developer/journeys-ux.md`: env, handoff route, new tab, brief contents (summary), cache note.
- Graph: no new nodes. `event-models` already on the Journeys.UX path-map prefix from the accounts spec. Do **not** add `IMPLEMENTED_AS` `proj-ux`.
- Waiver only if `docs-impact` / `graph-impact` require a no-meaning-change receipt.

---

## 9. Human-gated

Opening a second product UI is not a ledger/auth redesign. Tenant id in the query is the same product tenant already used on every Journeys API call. Seed body is model names/ids/metadata flags only.

---

## 10. Approval

Human reviews this file, then a plan under `docs/plans/`. Construction does not start without that plan’s approval. Human merge/release only.
