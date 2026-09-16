# Design: Journeys.UX loyalty shell (Accounts + Campaigns list)

**Date:** 2026-09-14  
**Status:** Approved (human 2026-09-14)  
**Scope:** First UX spec. Surgical extract from `temp/exp/apps/admin-web` into a React/Next app `Journeys.UX`: login, Loyalty-only left nav, Overview stub, read-only Accounts table, read-only Campaigns list. HTTP to `Journeys.API` only.  
**Depends on:** existing `Journeys.API` (Auth0 JWT, `Journeys-API-KEY`, Campaign and model/query endpoints). Does not depend on EXP BFF, identity Postgres, Aerospike, or the EXP monorepo as a workspace.  
**Placement authority:** `docs/product/` and `AGENTS.md`. This spec **flips** the old NonGoal `ui-in-this-sln` for a bounded Next app; it does not put a UI in `Journeys.sln` as a C# project.  
**If this spec and onion rules disagree:** UI still must not project-reference Core, DAL, or Infra. HTTP to `Journeys.API` only.  
**If this spec and `docs/platform/security.md` disagree on Auth0 `"hayward"`:** security.md wins; agents must not change that hardcode.

Later specs (not this file): remaining Loyalty nav screens, Campaign Agent / Journey Builder, Ollama `ILlmChatClientFactory`.

---

## 1. Problem and goals

Campaign Builder and Loyalty admin exist in deprecated EXP `admin-web` (`C:\Dev\Journeys\temp\exp`). Journeys has the engine and campaign-agent API, but no admin UI in this tree. Operators cannot sign in and see campaigns or accounts without the EXP platform (BFF, Prisma identity, Experiences, CDP).

This spec is the smallest path that works: a standalone React/Next folder you can open in its own Cursor window, with functional login and two live lists against `Journeys.API`.

| # | Goal | Success criterion |
|---|------|-------------------|
| G1 | **Isolated UX app** | `Journeys.UX/` is Next.js (React). Not a csproj. No project reference to Core/DAL/Infra. Runnable with `npm run dev` from that folder |
| G2 | **Bare-bones login** | Auth0 (same issuer the API already validates) **and** a Development API-key session behind an explicit env flag |
| G3 | **Loyalty is the product nav** | Left nav has only the Loyalty group (Loyalty first and only). Overview, Accounts, Campaigns are live; other Loyalty children are visible and disabled |
| G4 | **Campaigns list** | Signed-in user sees a read-only list from `POST api/Campaign/{tenantId}/getall` |
| G5 | **Accounts table** | Signed-in user sees a read-only table of `LoyaltyAccountDetails` via schema + `queryData` (no Account `getall` exists) |
| G6 | **No EXP platform** | No Aerospike, no EXP BFF process, no identity/admin Postgres, no Experiences/CDP copy |

### Non-goals (this spec)

- Campaign Agent UI, Journey Builder, SSE `/api/loyalty/campaign-agent/...`
- Ollama or any change to `AnthropicLlmChatClientFactory`
- Remaining Loyalty screens (promotions, analytics, action log, notifications, file ingestion, settings, data explorer, model builder, account detail, account builder)
- Mutating campaigns or accounts from the lists (publish, delete, copy, deposit)
- Playwright e2e, EXP permission catalog, Prisma, `@exp/prisma-*`
- Copying the EXP monorepo or adding UX as a C# project in `Journeys.sln`
- Per-tenant Auth0 (still human-gated). Pay-per-use billing
- Treating Auth0 key `"hayward"` as product `TenantId`

---

## 2. Decisions

| Topic | Choice |
|-------|--------|
| First slice | Shell + thin lists (brainstorming option 2): Overview, Accounts, Campaigns |
| Extract style | Surgical copy from `temp/exp/apps/admin-web`, not fork-all-then-delete, not greenfield lists |
| Stack | Next.js 16 + NextAuth 5 + TanStack Query + Vitest, matching admin-web generation |
| Auth | Auth0 via NextAuth **and** Development API-key login when `JOURNEYS_UX_ALLOW_API_KEY_LOGIN` is true |
| Identity store | None. No `prisma-admin`, no credentials/bcrypt user table |
| API transport | Thin Next server helper with EXP `bffFetch("loyalty", path)` **call shape**; guts call `Journeys.API`. No EXP BFF |
| Browser → API | Forbidden for secrets. API key and Auth0 access token stay server-side |
| Envelope | Wrap Journeys raw DTOs as `{ success, data, error }` so lifted parsers keep working |
| Permissions in UX | Session + `TenantId` opens the two lists. API 401/403 is the real gate. No second permission catalog |
| TenantId | Session/config product tenant id. **Not** Auth0 `"hayward"` |
| Nav extras | Disabled, not omitted, so later specs fill them without a new IA |
| Types | Small local zod/types module (Campaign, account row, `ApiResponse`). Not the whole `@exp/shared-types` package |
| Simplicity | Read-only lists. Prefer `simple-data-table` over `DynamicDataTable`. No coach-resume, no New campaign, no filters unless already required for getall |
| Commit of this spec | Human only |

---

## 3. Placement

```
C:\Dev\Journeys\Journeys\
  Journeys.sln          # unchanged C# onion; optional solution folder label only
  Journeys.UX/          # Next.js app — open this folder in a new Cursor window
    package.json
    src/...
  docs/specs/           # this file
```

Source of lifted files: `C:\Dev\Journeys\temp\exp\apps\admin-web` (Loyalty pages, layout/sidebar, NextAuth wiring, loyalty actions **subset**, `simple-data-table`). Rewrite imports that pointed at `@exp/shared-types` / `@exp/prisma-*` / EXP BFF.

`Journeys.UX` may depend on **HTTP** `Journeys.API`. It must not depend on Journeys C# projects.

---

## 4. Auth

NextAuth 5, two providers:

1. **Auth0** — default. Same issuer `Journeys.API` already validates (`docs/platform/security.md`). Session stores access token + product `TenantId` (from env `JOURNEYS_TENANT_ID` or a single configured mapping — not a new per-tenant Auth0 design).
2. **Development API-key** — only if `JOURNEYS_UX_ALLOW_API_KEY_LOGIN` is true. Sign-in form stores `Journeys-API-KEY` and product `TenantId` in the session.

Unauthenticated routes: `/signin` (and Auth0 callback). Everything under `/loyalty` and the app shell requires a session.

Do not change `Journeys.Infra.Auth` `"hayward"` in this spec.

Secrets (UX env, never committed live): `AUTH_SECRET`, Auth0 client id/secret/issuer, `JOURNEYS_API_BASE_URL`, `JOURNEYS_TENANT_ID`, optional `JOURNEYS_API_KEY` only for local documented examples in gitignored files.

---

## 5. Proxy and data flow

```
Browser (React)
  → Next server actions / allowlisted App Router routes
      → journeysFetch: session required
          → GET/POST {JOURNEYS_API_BASE_URL}/api/{Controller}/{tenantId}/...
              headers: Bearer {accessToken}  OR  Journeys-API-KEY
          ← raw DTO / problem JSON
      ← { success, data, error }
  ← page
```

Replace EXP `bffFetch`:

- Drop `SignJWT` BFF token, `BFF_URL`, `MANAGEMENT_BFF_URL`, Prisma `enabledServices`, permission string checks.
- Keep a **path allowlist**. Unknown loyalty paths fail closed (error, not open proxy).

**V1 allowlist (enough for G4–G5).** EXP split `loyalty` vs `loyalty-data` BFF services; both become `Journeys.API`.

| Lifted action | EXP BFF path (today) | Journeys.API |
|---------------|----------------------|--------------|
| `getCampaigns` | `POST loyalty/campaigns/{slug}/getall` | `POST api/Campaign/{tenantId}/getall` |
| `getAllSchemas` / `getSchemaByName` | `POST loyalty-data/schemas/{slug}/model/all` | `POST api/Model/{tenantId}/GetMany` (filter Live + name in the action) |
| `queryData` | `POST loyalty/events/{slug}/{schemaName}/admin/query` | `POST api/Events/{tenantId}/{modelName}/admin/query` |

Do not add `POST api/Campaign/{tenantId}` filters unless the read-only list cannot render without them (YAGNI: `getall` only).

Campaign Agent SSE is out of this spec.

Passthrough: do not forward `Cookie` or extra auth headers from the browser. Optional `X-ELP-Audit` is not required for read-only lists.

Local HTTPS: document how Next’s server fetch trusts `https://localhost:7001` (dev only). Do not disable TLS in production config.

---

## 6. Pages and nav

Left nav: **Loyalty only** (Heart icon / existing EXP Loyalty children). Loyalty is the first and only product group. No Dashboard, Experiences, Customers, Data Sources, Infrastructure, Admin users, Activity Insights, Reports.

| Href | This spec |
|------|-----------|
| `/loyalty` | Stub Overview: two cards linking to Accounts and Campaigns |
| `/loyalty/accounts` | Read-only `simple-data-table` of `LoyaltyAccountDetails`. Missing schema → one sentence, **no** link to model builder |
| `/loyalty/campaigns` | Read-only list from `getCampaigns`. No publish/copy/delete, no coach resume, no New campaign CTA |
| Other Loyalty children | Render in nav **disabled** |
| `/signin` | Auth0 + optional API-key form |

Do not ship `/loyalty/accounts/[id]`, `/loyalty/accounts/builder`, `/loyalty/campaigns/new`, or `/loyalty/campaigns/agent` in this spec (404 or omit routes).

---

## 7. Components (isolation)

| Unit | Does | Used how | Depends on |
|------|------|----------|------------|
| `Journeys.UX` Next app | Shell, nav, pages | `npm run dev`; own Cursor window | Node, env |
| NextAuth session | Auth0 or API-key | Middleware / `auth()` | Auth0 or flag + key |
| `journeysFetch` | Allowlisted proxy + envelope | Loyalty server actions | `JOURNEYS_API_BASE_URL`, session |
| Campaigns list UI | Read-only cards/rows | `/loyalty/campaigns` | `getCampaigns` |
| Accounts table UI | Read-only table | `/loyalty/accounts` | schema + `queryData` |
| Local types/zod | Campaign, account row, envelope | Actions + pages | None of `@exp/prisma-*` |
| `Journeys.API` | Authority for data and 401/403 | HTTP | Unchanged onion |

Each unit is understandable without the others’ internals. Changing Auth0 client settings must not require Core code. Changing list layout must not require a new capability id.

---

## 8. Error handling

| Failure | Response |
|---------|----------|
| No session on `/loyalty/*` | Redirect to `/signin` |
| Missing `TenantId` in session | Sign-in error; do not call the API with an empty tenant |
| `JOURNEYS_UX_ALLOW_API_KEY_LOGIN` false | Hide API-key form; Auth0 only |
| API 401/403 | Page error: not authorized / check tenant or key |
| API down or non-JSON | `{ success: false, error }` one line; no stack to the browser |
| Path not on allowlist | Fail closed; do not proxy |
| Schema `LoyaltyAccountDetails` missing | One sentence on Accounts; no builder CTA |
| Empty campaign list | Empty state, not a fake 404 card of EXP promotions |
| Logs | No tokens, API keys, connection strings, or full event payloads |

---

## 9. Testing / verification

1. Vitest: allowlist maps `campaigns/{tenant}/getall` to `api/Campaign/{tenantId}/getall`; unknown path rejected.
2. Vitest: raw array/object from a stub API becomes `{ success: true, data }`; HTTP error becomes `{ success: false, error }`.
3. Vitest: `journeysFetch` without session throws/returns unauthorized (no network).
4. Manual: Development API-key login → Overview → Campaigns list and Accounts table against running `Journeys.API`.
5. Manual: with flag off, API-key form is absent.
6. `docs-impact` / `graph-impact` exit 0 on this change set (docs + graph updated, not waived).
7. `Journeys.sln` still builds; UX is not part of `dotnet build`.

No Playwright in this spec. No Journeys.Core unit-test changes.

---

## 10. Graph and docs (extend)

**Do not invent a capability id.** Existing ids stay: `event-models`, `campaigns`, `journeys`, `rules-engine`, `outcomes`, `campaign-agent`, `mcp-api`.

- Remove or retire NonGoal node `ui-in-this-sln` (UI in this **tree** is now allowed as `Journeys.UX`; UI still is not a C# project in the sln).
- Add Project `proj-ux`, name `Journeys.UX`.
- Edges: `proj-ux` `CONSTRAINED_BY` `tenant-id-required`. Do **not** add `campaigns` `IMPLEMENTED_AS` `proj-ux` — campaigns stay `IMPLEMENTED_AS` `proj-core`; the UX is an HTTP client.
- `path-map.yaml`: prefix `Journeys.UX` → `campaigns` (`meaningOptional: false`).
- `scripts/path-docs-map.yaml`: `Journeys.UX` requires `docs/developer/journeys-ux.md`, `docs/platform/architecture.md`, `docs/platform/overlays.md`.
- `docs/roadmap/non-goals.md`: drop “UI / Next.js admin in `Journeys.sln`”; keep “do not copy EXP monorepo files.”
- `docs/platform/overlays.md`: UI row `{Product}.UX` → `Journeys.UX` (HTTP only).
- `docs/platform/architecture.md`: UI is `Journeys.UX`; must not project-reference Core/Adapters.
- `docs/developer/journeys-ux.md` + link from `docs/developer/index.md` and a short pointer in `local-ops.md`.
- `AGENTS.md`: UI exists as `Journeys.UX`; still not in the onion write path.

---

## 11. Implementation order

A separate `docs/plans/2026-09-14-Journeys-ux-loyalty-shell.md` is written only after this spec is approved.

1. Scaffold `Journeys.UX` (package.json, Next app, env example).
2. NextAuth Auth0 + flagged API-key session (no Prisma).
3. `journeysFetch` allowlist + envelope + Vitest.
4. Shell + Loyalty-only nav (disabled children).
5. Overview stub; Campaigns read-only list; Accounts simple table.
6. Docs/graph/overlays as §10.
7. Manual login + two lists against `Journeys.API`.

---

## 12. Out of scope later (not forgotten)

- Campaign Agent / Journey Builder + SSE proxy (next UX spec)
- Ollama leaf behind `ILlmChatClientFactory` (API spec)
- Other Loyalty nav items (enable one spec at a time)
- Account detail, mutations, model builder
- Playwright after the builder is back
- Per-tenant Auth0
