# Journeys.UX

Standalone Next.js admin (React) for Loyalty lists. Open `Journeys.UX/` in its own Cursor window. It talks **HTTP only** to `Journeys.API`. It is not a C# project and must not reference Core, DAL, or Infra.

**Spec:** `docs/specs/2026-09-14-Journeys-ux-loyalty-shell-design.md`

## Run

1. `Journeys.API` listening (typical `https://localhost:7001`).
2. Copy `Journeys.UX/.env.example` to `Journeys.UX/.env.local` and fill values. Never commit `.env.local`.
3. `JOURNEYS_TENANT_ID` is the product tenant id (not Auth0 `"hayward"`). The sign-in form prefills that value. API calls always use this env value when it is set, so a stale browser session cannot override it. Local UX currently uses `TestTenant1`.
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

Catalog list (`/model/all`) sends `modelType: loyalty` and **omits** `ModelId`. Entity routes require a real GUID and never coalesce a missing id to `"unknown"`. After C# adapter changes, **restart `Journeys.API`** in Visual Studio.

## This spec’s screens

Overview, Accounts table, Campaigns list. Other Loyalty nav items are disabled on purpose.
