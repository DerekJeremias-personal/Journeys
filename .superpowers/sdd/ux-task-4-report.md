# Task 4 Report: NextAuth (Auth0 + flagged API-key)

## Status

**DONE_WITH_CONCERNS** — files created; controller later ran tests/tsc (see verification below).

## Commits

None (per instructions).

## Files created

| File | Role |
|------|------|
| `Journeys.UX/src/lib/api-key-login-enabled.test.ts` | Vitest: unset → false; `"true"` → true (brief verbatim) |
| `Journeys.UX/src/lib/api-key-login-enabled.ts` | `process.env.JOURNEYS_UX_ALLOW_API_KEY_LOGIN === "true"` |
| `Journeys.UX/src/types/next-auth.d.ts` | Session/User/JWT: `tenantId`, optional `accessToken`/`apiKey` |
| `Journeys.UX/src/auth.ts` | NextAuth: Auth0 + optional Credentials id `api-key`; exports `handlers`, `auth`, `signIn`, `signOut` |
| `Journeys.UX/src/app/api/auth/[...nextauth]/route.ts` | `{ GET, POST }` from handlers |
| `Journeys.UX/src/proxy.ts` | Next 16 Auth wrapper; matcher `["/loyalty/:path*", "/"]` |
| `Journeys.UX/src/app/signin/page.tsx` | Server page: `allowApiKey={apiKeyLoginEnabled()}` |
| `Journeys.UX/src/app/signin/sign-in-form.tsx` | Client: title `Sign in to Journeys`; Auth0 always; API-key form only if `allowApiKey` |

Did **not** create `middleware.ts` (Next 16 docs: middleware renamed to `proxy`; one file only).

## TDD cycle

### Step 1 — Failing tests (RED, intended)

Created `api-key-login-enabled.test.ts` before `api-key-login-enabled.ts`.

**Expected RED** (not captured — shell denied): `Cannot find module './api-key-login-enabled'`.

### Step 2 — Implementation (GREEN, intended)

Added `api-key-login-enabled.ts` exactly as specified, then remaining NextAuth files.

**Expected GREEN** (not captured — shell denied): existing Task 2–3 tests plus 2 `apiKeyLoginEnabled` cases.

## Self-review

| Check | Result |
|-------|--------|
| `apiKeyLoginEnabled()` exact `"true"` | Yes |
| Credentials provider id `api-key` | Yes |
| Session fields `tenantId`, optional `accessToken`, `apiKey` | Yes |
| Sign-in title `Sign in to Journeys` | Yes |
| Auth0 button always; API-key form only if `allowApiKey` | Yes |
| API key not in HTML default value | Yes — empty controlled state |
| Server page passes flag into client form | Yes |
| `proxy.ts` only (no `middleware.ts`) | Yes — Next 16 file convention |
| Env names from `.env.example` | Yes (`AUTH0_*`, `JOURNEYS_TENANT_ID`, `JOURNEYS_UX_ALLOW_API_KEY_LOGIN`) |
| No Core/DAL/Infra references | Yes |

## Concerns

1. **Tests not run** — parent/human should run `cd C:\Dev\Journeys\Journeys\Journeys.UX && npm test` then `npx tsc --noEmit`.
2. **TDD RED log missing** — tests written before impl, but terminal output unavailable.
3. **`apiKeyLoginEnabled()` is evaluated at NextAuth module load** — toggling `JOURNEYS_UX_ALLOW_API_KEY_LOGIN` requires a server restart to add/remove the Credentials provider (sign-in form still reads the env per request).
4. **`AUTH_SECRET` required at runtime** — `.env.example` has the name; live value stays in gitignored env.

## Controller verification (2026-09-14)

```
npm test → 16/16 pass
npx tsc --noEmit → exit 0 after:
- auth.ts session: typeof string guards on token.tenantId / accessToken / apiKey
- journeys-fetch.test.ts: as unknown as [string, RequestInit] (Task 3 test types)
```
