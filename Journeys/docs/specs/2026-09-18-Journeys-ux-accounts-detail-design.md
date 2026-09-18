# Design: Journeys.UX account detail + deposits/tier (surgical lift)

**Date:** 2026-09-18  
**Status:** Approved (human 2026-09-18)  
**Scope:** Lift EXP admin-web **Accounts list click-through**, **account detail**, and **operator writes** (deposit / spend / expire, manage tier, assign/remove journey) into `Journeys.UX`. HTTP-only to `Journeys.API`. Reuse existing Account and Journey REST (including AdminAudit on those writes).  
**Depends on:** `docs/specs/2026-09-14-Journeys-ux-loyalty-shell-design.md` (shell, `journeysFetch`), `docs/specs/2026-09-18-Journeys-ux-campaigns-ia-design.md` (neutral Tailwind / shadcn, campaign GET/getall, PAT `getall`). Existing `AccountController`, `JourneyController`, `EventsController` `admin/query`, `CampaignController` `pointaccounttype/getall`.  
**Placement authority:** `docs/product/` (`loyalty-account.md`, `outcome.md`, `journey.md`, `event-model.md`) and `AGENTS.md`.  
**If this spec and onion rules disagree:** `Journeys.UX` stays HTTP-only. No project-reference to Core/DAL/Infra. No BFF. Writes go through `Journeys.API` → Core.  
**If this spec and `loyalty-account.md` disagree on ledger types or PAT meaning:** ontology + Core win. UX does not invent ledger types or a second points store.

This spec **replaces** the shell NonGoals “account detail” and list mutations (deposit) for the routes in §6. It does **not** invent capability ids: `event-models`, `journeys`, `outcomes` already exist. `proj-ux` remains an HTTP client, never `IMPLEMENTED_AS` for those capabilities.

Ledger / money-like outcomes stay a **human-gated** risk surface (`AGENTS.md`): this spec **wires existing audited APIs**. It does not redesign `PointLedgerTypeStrings`, PAT ids, or AdminAudit persistence.

---

## 1. Problem and goals

Journeys.UX `/loyalty/accounts` is still the shell HTML table: no row links, no `/[id]`, no deposits, no tier moves. Operators still open EXP `admin-web` for that. Account GET, point balances/ledgers, deposit, withdrawal, journey enter/exit, PreviewTierMove, and MoveTier already exist. Deposit, withdrawal, enter/exit, and MoveTier already require `X-Journeys-Audit` (`auditRequired: true`).

| # | Goal | Success criterion |
|---|------|-------------------|
| G1 | **EXP accounts list** | `/loyalty/accounts` uses schema-driven `DynamicDataTable`, search, row → `/loyalty/accounts/[id]` |
| G2 | **EXP account detail** | Summary, points card, schema-driven fields, campaign/tier progress, inline eventable models |
| G3 | **Operator writes** | Deposit / spend / expire; manage tier (preview then commit); assign journey; remove journey |
| G4 | **HTTP-only** | All reads/writes through `journeysFetch` → `Journeys.API`. Secrets stay on the Next server |
| G5 | **Audit on money/journey writes** | Mutating account/journey calls send `X-Journeys-Audit` built on the Next server. The browser cannot inject the header. Campaign REST stays unaudited (Campaigns IA) |
| G6 | **Journeys look, EXP files** | Surgical lift from `temp/exp/apps/admin-web`. Neutral theme already on the shell. No EXP brand/Coach/Prisma/PermissionGuard |

### Non-goals (this spec)

- `/loyalty/accounts/builder` and generic Model Builder. `ModelController` stays GetMany-only. Missing-schema empty state is **one sentence, no CTA**
- Data Explorer routes. Eventable-model rows and footers do **not** link to `/loyalty/data-explorer/...`
- EXP `moveTier` client fallback (enter then exit when PAT types are missing). Call `POST .../MoveTier` only
- Deposit `expirationDate` (EXP form field). `PointDespositRequest` has no such property; do not add DTO fields this increment
- `JourneyPreviewController` (campaign progress is computed from account journeys + campaigns + ledgers, as in EXP `AccountCampaignProgress`)
- Account save/alias/delete, ledger admin upsert, PAT upsert, bulk withdrawal
- AdminAudit on Campaign REST
- Playwright, EXP permission catalog, Prisma, `@exp/prisma-*`
- New capability ids or a second graph
- Ledger-type redesign

---

## 2. Decisions

| Topic | Choice |
|-------|--------|
| Vehicle | Surgical EXP lift + Journeys HTTP adapter (same as Campaigns IA) |
| Scope | Detail + deposits/tier. Builder later |
| List | Lift EXP `LoyaltyAccountsListClient` + `DynamicDataTable`. Client-side CSV from the loaded page is allowed (already in that component). No Events `/export` allowlist |
| Detail | Full EXP composition minus Data Explorer hrefs |
| Writes | Full EXP mutation set: deposit / spend / expire + manage tier + assign/remove journey |
| Expire | Same `POST .../points/withdrawal` as spend. Audit `action` is `"Expire"` vs `"Spend"`. Do not add `/points/expire` |
| Move tier | `PreviewTierMove` then `MoveTier`. No enter/exit composite fallback |
| Audit header | `X-Journeys-Audit` JSON matching `AdminAuditDto` fields the UX owns: `adminUserId`, `loyaltyMemberId`, `actionType`, `action`, `comment`. `adminUserId` comes from the existing Next session user id. Never forward a browser-supplied audit header |
| Campaign REST | Still no `X-Journeys-Audit` |
| Identifier resolution | EXP order: query `LoyaltyAccountDetails` by id/ext fields, then GET by internal id, then GET by ext id. Points/journey calls use resolved loyalty account id; enter/exit/move also send xref |
| Missing schema | One sentence; **no** builder link |
| EXP PermissionGuard | Drop it. Do not add a second permission catalog |
| Query for events | Reuse existing allowlisted `events/{slug}/{model}/admin/query`. Pass `query` / `parameters` and/or `loyaltyAccountId` (API already scopes `c.accountid` when that body field is set). Do not add `events/.../query` unless `admin/query` cannot filter |
| Logging | Action name and ids already used by the fetch helper. No secrets, full event payloads, audit JSON, or new account-xref fields (`docs/developer/logging.md`) |
| Commit of this spec | Human only |

---

## 3. Placement

```
C:\Dev\Journeys\Journeys\
  Journeys.UX/          # Next.js — HTTP client only
    src/app/loyalty/accounts/page.tsx
    src/app/loyalty/accounts/[id]/page.tsx
    src/components/loyalty/accounts/
    src/components/loyalty/points/
    src/components/loyalty/dynamic-data/
    src/services/loyalty/actions.ts
    src/lib/map-loyalty-path.ts
    src/lib/journeys-fetch.ts
  Journeys.API/         # unchanged REST this increment
  docs/specs/           # this file
```

Source of lifted files: `C:\Dev\Journeys\temp\exp\apps\admin-web` — `components/loyalty/accounts/*`, `components/loyalty/points/*`, `components/loyalty/dynamic-data/*`, `services/loyalty/utils/account-identifiers.ts`. Rewrite `@exp/shared-types` / PermissionGuard / `X-ELP-Audit` / `bffFetch` locally. Add missing shadcn primitives the lift needs (e.g. `collapsible`) in the existing Journeys-neutral set.

Do **not** create `/loyalty/accounts/builder`. Visiting it stays 404.

---

## 4. Audit

**`journeysFetch`:** add a dedicated `audit?: AdminAuditHeader` that the helper serializes to `X-Journeys-Audit`. Do **not** accept a free-form headers bag for this (the browser must not supply the audit JSON). Merge the serialized header onto the request without replacing existing credential headers. Server actions for deposit / withdraw / expire / enter / exit / MoveTier **must** attach audit. GET/list/query **must not**.

Allowlist of audit actions (pretty names for the header; API still writes `ActionPrettyPrint` itself):

| UX action | `actionType` | `action` |
|-----------|--------------|----------|
| Deposit | `Point Adjustment` | `Deposit` |
| Spend | `Point Adjustment` | `Spend` |
| Expire | `Point Adjustment` | `Expire` |
| Assign journey | `Journey Movement` | `Add` |
| Remove journey | `Journey Movement` | `Remove` |
| Move tier | `Journey Movement` | `Move Tier` |

`loyaltyMemberId` = loyalty account id (internal). `comment` is required in the forms (EXP: points comment min 1; tier comment min 3). Confirm dialog before deposit / spend / expire (EXP `confirmPointAdjustment`).

If the session has no user id, fail the mutation in UX before fetch. Do not send an empty `adminUserId`.

---

## 5. Proxy allowlist (additions)

Keep existing Campaign / schema / `admin/query` / campaign-agent maps. Add (EXP slug in the path is ignored; the route id comes from the existing fetch helper, same as Campaigns):

| Lifted path shape | Journeys.API |
|-------------------|--------------|
| `GET accounts/{slug}/{id}` | `GET api/Account/{tenantId}/{id}` |
| `GET accounts/{slug}/ext/{id}` | `GET api/Account/{tenantId}/ext/{id}` |
| `GET accounts/{slug}/points/balances/{id}` | `GET api/Account/{tenantId}/points/balances/{id}` |
| `GET accounts/{slug}/points/{id}` | `GET api/Account/{tenantId}/points/{id}` |
| `POST accounts/{slug}/points/deposit` | `POST api/Account/{tenantId}/points/deposit` |
| `POST accounts/{slug}/points/withdrawal` | `POST api/Account/{tenantId}/points/withdrawal` |
| `GET journey/{slug}/ManuallyEnter/{campaignId}/Journey/{journeyId}/ForAccount/{xref}` | `GET api/Journey/{tenantId}/ManuallyEnter/...` (same suffix) |
| `GET journey/{slug}/ManuallyExit/{campaignId}/Journey/{journeyId}/ForAccount/{xref}` | `GET api/Journey/{tenantId}/ManuallyExit/...` |
| `POST journey/{slug}/PreviewTierMove` | `POST api/Journey/{tenantId}/PreviewTierMove` |
| `POST journey/{slug}/MoveTier` | `POST api/Journey/{tenantId}/MoveTier` |

Match **more specific** account paths (`points/balances`, `points/deposit`, `ext/`) before `accounts/{slug}/{id}`.

**Still rejected:** `pointaccounttype/upsert`, account `admin/save`, alias, remove, `points/admin`, `points/expire`, `accounts/builder`, Data Explorer, unknown paths.

Deposit/withdraw bodies follow existing DTOs (`PointDespositRequest`, `PointWithdrawlRequest`): `loyaltyAccountId`, `pointAccountTypeId`, `amount`, `eventId` (UX generates a GUID), `eventType: "admin"`, `userId` (session), `depositDate` / `withdrawalDate` (now). Spend/expire set `status: "shipped"` as EXP does. Drop `expirationDate`.

MoveTier / preview bodies: `MoveTierRequest` (`loyaltyAccountId`, `loyaltyAccountXReference`, `targetCampaignId`, `targetJourneyId`, `comment`, `adminUserId`).

`queryData` gains optional `query`, `parameters`, `loyaltyAccountId`, `pageSize`, `continuationToken`, `sortBy`, `sortOrder`. Identifier lookup uses EXP `LOYALTY_ACCOUNT_IDENTIFIER_QUERY` + `@id`. Eventable panels pass `loyaltyAccountId` and a small page size (25). List query may stay unfiltered with schema-driven paging as the lifted table already does.

---

## 6. Pages and nav

Left nav unchanged: Accounts stays a live link. Model Builder stays disabled. No new nav items.

| Href | This spec |
|------|-----------|
| `/loyalty/accounts` | Lifted list. Missing/not-Live `LoyaltyAccountDetails`: *The LoyaltyAccountDetails schema is missing or not Live.* No builder CTA |
| `/loyalty/accounts/[id]` | Detail. Header Actions: assign journey, remove journey, manage tier |
| `/loyalty/accounts/builder` | **404** (omit route) |

Detail layout (EXP, minus explorer links):

1. Account profile summary + points card (Manage → deposit/spend/expire tabs)
2. `DynamicEntityDetails` for remaining schema fields
3. `AccountCampaignProgress`
4. `EventableModelsSection` — collapsible `SimpleDataTable` per Live schema where EXP `isEventableSchema` is true (`tag === "eventable"` **or** name `LoyaltyAccountDetails`); **delete** the “View all in data explorer” link; tables are not click-through. Lift that helper; do not invent a second filter.

Empty balances: “No point accounts found.” Account not found: “No account exists with ID {id}.”

---

## 7. Components (isolation)

| Unit | Does | Used how | Depends on |
|------|------|----------|------------|
| `mapLoyaltyPath` | Allowlist §5 | `journeysFetch` | Existing fetch helper route id |
| `journeysFetch` | Envelope + optional audit header | Server actions | Never browser audit |
| Account/journey actions | GET account, balances, ledgers, deposit, withdraw/expire, enter/exit, preview/move, query | Pages/modals | `journeysFetch` |
| Identifier helpers | Resolve internal id vs xref | List detail id + detail load | Query + GET fallbacks |
| `dynamic-data` | Schema-driven list/detail/simple tables | List + detail | Schema + `queryData` |
| Detail client | Compose §6 | `/loyalty/accounts/[id]` | Actions + points + journey modals |
| Points manage modal | Three tabs; confirm; comment | Points card | Deposit/withdrawal APIs |
| Account actions + journey/tier modals | Assign/remove/move | Detail header | Journey APIs |
| `Journeys.API` | Write authority + AdminAudit | HTTP | Unchanged onion |

Each unit is understandable without the others’ internals. Changing table layout must not require a new capability id. Changing PAT meaning must not happen in UX.

---

## 8. Error handling

| Failure | Response |
|---------|----------|
| Schema missing / not Live | One sentence on the list; no CTA |
| Account not found after identifier fallbacks | Detail alert; no fake profile |
| Mutation fail (including missing audit header 400) | Toast + modal/page error; no silent retry |
| Eventable query fail | Error on that panel only |
| Confirm cancel | No-op |
| Path not allowlisted | Fail closed |
| Logs | §2 logging row |

Do not log secrets, connection strings, full event payloads, or the raw `X-Journeys-Audit` JSON.

---

## 9. Testing / verification

**UX (Vitest)**

1. Allowlist: every §5 path maps with the existing fetch helper’s route id, not the EXP path slug. Unknown paths throw `not-allowlisted:`. Empty route id rejected.
2. More-specific account paths win over `accounts/{slug}/{id}` (e.g. `points/deposit` is not treated as an account id).
3. Spend and expire both map to `points/withdrawal`.
4. Mutating `journeysFetch` attaches `X-Journeys-Audit` with non-empty `adminUserId`; GET balances does not. A free-form `headers` option is not added.
5. Identifier helpers: GUID vs ext id vs merged `event` payload (`account-identifiers` tests, lift EXP cases).
6. Missing-schema copy contains no `/builder` href.
7. Eventable section has no `data-explorer` href (component test or string assert on the lifted footer).

**API:** no new Core/controller tests unless a binding bug forces a DTO fix (out of scope by default). Existing AdminAudit `auditRequired: true` stays.

**CI vs browser**

- UX `npm test` + `npx tsc --noEmit` per slice.
- `docs-impact` / `graph-impact` on the implementation change set (not waived).
- Browser (human or agent with browser tools): list search + row click; detail summary/points/progress/eventable; deposit/spend/expire confirm; manage tier preview+commit; assign/remove journey; missing-schema sentence; builder URL 404.

No Playwright in this spec.

---

## 10. Graph and docs (extend)

Do **not** invent capability ids.

- `docs/product/graph/path-map.yaml`: `Journeys.UX` prefix nodes become `[campaigns, journeys, campaign-agent, event-models, outcomes]` (`meaningOptional: false`).
- Do **not** add `event-models` / `outcomes` / `journeys` `IMPLEMENTED_AS` `proj-ux`.
- `scripts/path-docs-map.yaml`: keep existing Journeys.UX docs; add `docs/product/ontology/loyalty-account.md`.
- `docs/developer/journeys-ux.md`: Accounts is no longer list-only. Document routes, writes, audit header, expire=withdrawal, no builder, no Data Explorer.
- Campaigns IA §13 “Account detail / deposits” is fulfilled by **this** file (historical spec stays; implementation docs point here).
- Shell spec NonGoal “account detail” is fulfilled here; do not rewrite that file except a one-line pointer if docs-impact requires it.
- `docs-impact` / `graph-impact` must pass on the **implementation** change set. Writing this spec file alone does not require a graph change.

---

## 11. Implementation order (slices)

A separate plan under `docs/plans/` is written only after **this spec** is approved. Construction: say `execute` plus an intent name; `journeys-plan-to-aidlc` starts `/aidlc classic`. Linear unit issues land before any `Journeys.*` code.

1. **Allowlist + audit header + `queryData` options** — §5 maps; Vitest for audit attach/non-attach and path specificity. Campaign REST still unaudited.
2. **`dynamic-data` + identifiers + list** — click-through; missing-schema sentence; no builder CTA.
3. **Read-only detail** — summary, schema fields, campaign progress, eventable inline (no explorer links).
4. **Points manage** — deposit / spend / expire; confirm; comment; balances refetch.
5. **Journey/tier actions** — assign/remove; preview + MoveTier; no PAT-missing fallback.
6. **Docs/graph** as §10. Browser pass on the flows in §9.

Do not ship list click-through without identifier resolution (wrong id on points/journey calls). Do not ship writes without `X-Journeys-Audit`. Do not add a builder route “just for the empty state.”

---

## 12. Out of scope later (not forgotten)

- Account model builder + Model save API
- Generic Model Builder nav
- Data Explorer
- Deposit expiration on the DTO
- EXP MoveTier PAT-missing fallback
- Campaign REST AdminAudit
- Account alias / delete / PAT upsert from UX
- Playwright
