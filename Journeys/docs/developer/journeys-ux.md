# Journeys.UX

Standalone Next.js admin (React) for Loyalty lists and Campaigns IA. Open `Journeys.UX/` in its own Cursor window. It talks **HTTP only** to `Journeys.API`. It is not a C# project and must not reference Core, DAL, or Infra. Campaigns are **mutable over HTTP** (save, copy, restore, delete Draft, lifecycle). UX is not write authority — `Journeys.Core` owns those writes. Tailwind + shadcn on the shell is **Journeys-neutral** (no EXP brand/sidebar/Coach tokens).

**Specs:** `docs/specs/2026-09-14-Journeys-ux-loyalty-shell-design.md` (shell), `docs/specs/2026-09-18-Journeys-ux-campaigns-ia-design.md` (Campaigns IA), `docs/specs/2026-09-17-Journeys-ux-campaign-agent-chat-design.md` (agent chat), `docs/specs/2026-09-18-Journeys-ux-accounts-detail-design.md` (Accounts detail), `docs/specs/2026-09-18-Journeys-ux-backend-model-builder-handoff-design.md` (Model Builder handoff).

## Run

1. `Journeys.API` listening (typical `https://localhost:7001`).
2. Copy `Journeys.UX/.env.example` to `Journeys.UX/.env.local` and fill values. Never commit `.env.local`.
3. `JOURNEYS_TENANT_ID` is the product tenant id (not Auth0 `"hayward"`). The sign-in form prefills that value. API calls always use this env value when it is set, so a stale browser session cannot override it. The value is lowercased on the way to `Journeys.API` so it matches persistence partition keys. Local UX currently uses `TestTenant1`.
4. `JOURNEYS_UX_ALLOW_API_KEY_LOGIN=true` only on a trusted machine. Then sign-in can use `Journeys-API-KEY`. Leave `AUTH0_*` empty for that path — Auth0 is registered only when client id, secret, and issuer are all set. Empty Auth0 env used to fail every sign-in with NextAuth `InvalidEndpoints`.
5. From `Journeys.UX`: `npm install` then `npm run dev` (port 3000).
6. Open `/signin`, then `/loyalty`.

If Node fetch fails TLS to local HTTPS (`DEPTH_ZERO_SELF_SIGNED_CERT` / `Cannot reach Journeys.API (fetch failed)`), that is expected: Postman usually has SSL certificate verification off, while Node `fetch` rejects the ASP.NET self-signed cert. `journeysFetch` sets `NODE_TLS_REJECT_UNAUTHORIZED=0` only when `JOURNEYS_API_BASE_URL` is localhost/`127.0.0.1`. `npm run dev` also sets `NODE_OPTIONS=--use-system-ca` for child processes. Do not disable TLS verification for non-local API URLs. `JOURNEYS_UX_TLS_REJECT_UNAUTHORIZED` in `.env.example` is documentation; do not read it to disable TLS in production builds.

If swagger on `https://localhost:7001` itself times out, the API process is listening but stuck (Campaign `getall` has done this). Stop and start debugging in Visual Studio, then refresh the UX. A working Postman call to `/api/Events/.../process` does not prove Campaigns/Accounts `getall` is healthy.

`src/services/loyalty/actions.ts` is a `"use server"` module: export only async functions. Schema name constants live in `src/services/loyalty/schema-names.ts`.

Each Loyalty screen owns its Backend model. Do not send `modelId: "unknown"`.

| Screen | What the UX sends | Where the Backend model id comes from |
|--------|-------------------|----------------------------------------|
| **Campaigns** | `POST campaigns/{tenant}/getall` with page size only | `CampaignAdapter` constant `CAMPAIGN_MODEL_ID` `eeae67ca-7bf9-4d2b-9131-83717b219a3a` (platform campaign container). Model type is always `loyalty`. |
| **Accounts** | `POST events/{tenant}/LoyaltyAccountDetails/admin/query` | Screen name `LoyaltyAccountDetails` (customer event model). `EventService` loads that schema from the tenant catalog, then queries the **wrapper** model GUID from its `Wrapper` metadata. The loyalty-account container GUID `e2cc2404-c60b-4cbc-9f50-6db7ee58e01d` is used by DAL account adapters, not this list. |

## Accounts

`/loyalty/accounts` is the schema-driven list (`DynamicDataTable`, search, row click). Missing/not-Live `LoyaltyAccountDetails`: *The LoyaltyAccountDetails schema is missing or not Live.* No builder.

`/loyalty/accounts/[id]` is detail: profile, point balances, schema fields, campaign progress, inline eventable models (no Data Explorer).

Writes: deposit / spend / expire (`POST .../points/deposit` and `.../points/withdrawal`; expire is withdrawal + audit action Expire), assign/remove journey, preview + MoveTier. Mutating calls send `X-Journeys-Audit` built on the Next server. `/loyalty/accounts/builder` is not shipped.

## Campaigns

Campaigns are mutable through Campaign REST (`getall` plus `query` for draft/pause / `get` / `save` / `delete` / `copy` / `restore` / versions / archived). The list merges **live** (`getall`) with **draft** and **pause** so Duplicate/Unpublish results are visible. This increment does **not** send `X-Journeys-Audit` on those writes (no Campaign AdminAudit). Keep existing Campaign Agent tool audit on SSE.

| Href | Role |
|------|------|
| `/loyalty/campaigns` | Cards + filters + kebab + New + Archived + **Resume agent** when conversations exist |
| `/loyalty/campaigns/new` | Journey Builder + unlabeled `AgentChat` rail + collapsed Campaign JSON when linked |
| `/loyalty/campaigns/agent` | Agent-only. Optional `?conversationId=` to resume. Inspector after a campaign id is discovered |
| `/loyalty/campaigns/[id]?campaignStatus=` | **Wizard edit**. Query is required to fetch the status partition |
| `/loyalty/campaigns/[id]/agent` | Live-only agent rail (`linkedCampaignId`). Non-Live redirects away |
| `/loyalty/campaigns/archived` | Archived list + restore (`POST .../restore`, not save-as-draft on the archive row) |
| `/loyalty/campaigns/versions/[extCampaignId]` | Version history for a program identity |

**Copy** (`POST .../copy`): new Draft program — new `Id`, **new** `ExtCampaignId`, name `{name} Copy` unless overridden. UX does not strip-and-save locally.

**Restore** (`POST .../restore`): new Draft, **same** `ExtCampaignId`, new `Id`. Archive row unchanged. Fails if a Draft already exists for that ext id.

**Unpublish** is **Pause** (`status: pause` on the **same** Live id). Not Live → Draft.

**Delete** is Draft-only. **Publish** requires confirm before Live save.

`/new` is the EXP progressive Journey Builder (stations, review modals, health sidebar, welcome canvas) with an unlabeled Agent rail. Title is page text plus `CampaignJsonDisclosure` inside `AgentChat` when a campaign id is linked — no second JSON inspector. Discovered ids `GET` Draft and hydrate; dirty local work shows keep-local / apply-Agent and never silent-overwrite. Save/create-empty persist through `updateCampaign` (no EXP create composite).

Wizard Live-edit does not upsert the Live journey in place. If a Draft already exists for that `extCampaignId`, the wizard edits the Draft id. If not, save POSTs a **new** id (omit `id`) with the **same** `extCampaignId` and `status: draft`.

## Agent

Unlabeled in-product name: **agent** (strings: “Agent”, “Open in Agent”, “Resume agent”, “Campaign JSON”). Browser POSTs to Next `/api/loyalty/campaign-agent/messages/stream`; the server forwards to `POST /api/v1/{tenantId}/campaign-agent/messages/stream`. Secrets stay on the Next server. LLM provider is API startup config (`docs/developer/campaign-agent-llm.md`), not a UX control.

Stream body sends `conversationId`, `linkedCampaignId` (once known), and `clientMessageId`. The client parses `progress`, `mcp`, `workflow`, `delta`, `done`, and `error`. Discovered campaign ids hydrate the builder. Collapsed-by-default **Campaign JSON** (`<details>`) shows GET campaign JSON only — never stream frames or LLM text.

Live smoke: API + Ollama tray → sign in → Campaigns → Agent → one turn with streamed assistant text and at least one successful MCP tool. Not required: Draft upsert or Live publish.

The composer is a textarea with **Send** under it. **Enter** sends; **Shift+Enter** inserts a newline. After `started`, the conversation id is labeled **Conversation**, shown in monospace, with a copy icon.

Turns persist as Backend model `AgentMessage` (`12f6e8d4-5b3a-491c-9f2e-8a7d6c5b4a31`, type `loyalty`, partition `owneruserid` + `conversationid`). If that container is missing for the tenant, the API fails at load history with a generic Backend 500 (`An unexpected error occurred`). Create it via Backend `POST /api/{tenantId}/Model/save` (local `TestTenant1` already has it).

Catalog list (`/model/all`) sends `modelType: loyalty` and **omits** `ModelId`. Entity routes require a real GUID and never coalesce a missing id to `"unknown"`. After C# adapter changes, **restart `Journeys.API`** in Visual Studio.

## Model Builder

Set `BACKEND_MODEL_UX_BASE_URL` (no trailing slash required) to enable the Loyalty **Model Builder** nav item. The link opens `/loyalty/models/handoff`, which POSTs `tenantId`, `source=journeys`, and a one-time brief to Backend Modeler in a new tab. The brief is the event-model contract plus catalog gaps from existing GetMany — not a query string. After a Backend model save, `ModelCache` may need an API restart before Journeys lists the new schema.

## This spec’s screens

Overview, Accounts detail, Campaigns IA, Model Builder handoff. Other Loyalty nav items may still be disabled.
