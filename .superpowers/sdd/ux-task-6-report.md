# Task 6 Report: Loyalty actions + Campaigns + Accounts pages

## Status

**DONE_WITH_CONCERNS** — all files created per brief; TDD order followed; shell blocked `npm test` / `npx tsc --noEmit` in this session.

## Commits

None (per instructions).

## Files created

| File | Role |
|------|------|
| `Journeys.UX/src/services/loyalty/parse-list.test.ts` | Vitest: `extractEntities` + `pickLiveSchema` (exact cases from brief) |
| `Journeys.UX/src/services/loyalty/parse-list.ts` | Pure helpers: `extractEntities`, `pickLiveSchema` |
| `Journeys.UX/src/services/loyalty/actions.ts` | `"use server"` wrappers: `getCampaigns`, `getAllSchemas`, `getSchemaByName`, `queryData` |
| `Journeys.UX/src/app/loyalty/campaigns/page.tsx` | Server component: read-only campaign list |
| `Journeys.UX/src/app/loyalty/accounts/page.tsx` | Server component: read-only accounts table |

## TDD order

1. **Tests first** — `parse-list.test.ts` written with brief's exact test bodies before implementation.
2. **parse-list.ts** — implements `extractEntities` (array / `entities` / `Entities` / `items` / `Items` → `[]`) and `pickLiveSchema` (first case-insensitive name + `"live"` status match).
3. **actions.ts** — thin `"use server"` layer calling `journeysFetch` + parse helpers.
4. **Pages** — server components consuming actions only.

## Actions (brief compliance)

| Export | Path | Body | Post-processing |
|--------|------|------|-----------------|
| `getCampaigns` | `campaigns/session/getall` | `{ pageSize: 100, continuationToken: null }` | `extractEntities` → `CampaignListItem[]` |
| `getAllSchemas` | `schemas/session/model/all` | `{}` | `extractEntities` → `SchemaListItem[]` |
| `getSchemaByName(name)` | (via `getAllSchemas`) | — | `pickLiveSchema` |
| `queryData(schemaName)` | `events/session/{schemaName}/admin/query` | `{ query: "", parameters: {}, pageSize: 50, continuationToken: null, sortBy: "", sortOrder: "ASC" }` | `extractEntities` → `Record<string, unknown>[]` |

Constant: `LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME = "LoyaltyAccountDetails"`.

Dummy slug `session` used throughout (path mapper ignores slug segment).

## Campaigns page

- Calls `getCampaigns()`.
- `!success` → `<p className="error">` with `error`, or `"not authorized / check tenant or key"` when error matches 401/403/unauthorized/forbidden/nope.
- Empty array → `<p className="empty">No campaigns found.</p>`.
- Else → `<ul>` items: `{name} — {status} — {id}`.
- No buttons, no builder CTA, no mutations.

## Accounts page

- Calls `getSchemaByName(LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME)`.
- Schema null → `<p className="empty">The LoyaltyAccountDetails schema is missing or not Live for this tenant.</p>` — no builder link.
- Schema ok → `queryData(schema.name ?? LOYALTY_ACCOUNT_DETAILS_SCHEMA_NAME)`.
- Table headers: `attribute.displayName ?? attribute.name`.
- Cells: `row[attr.name]`.
- Attributes missing/empty → columns `id` and `name` only.
- Empty rows → `<p className="empty">No loyalty accounts found.</p>`.
- API failure on `queryData` → same auth-aware error pattern as Campaigns.
- No buttons, no builder CTA, no mutations.

## Self-review

| Check | Result |
|-------|--------|
| TDD: tests written before `parse-list.ts` | Yes |
| `actions.ts` is `"use server"` wrappers only | Yes |
| Parse logic not in actions (in `parse-list.ts`) | Yes |
| Path strings use dummy slug `session` | Yes |
| Exact copy for empty/missing-schema messages | Yes |
| No builder CTA | Yes |
| No mutations | Yes |
| No git commit | Yes |
| Linter clean on touched files | Yes |

## Tests

Not run — terminal blocked in this session. Controller should run:

```powershell
cd C:\Dev\Journeys\Journeys\Journeys.UX
npm test
npx tsc --noEmit
```

Expected: 5 new tests in `parse-list.test.ts` pass; existing Tasks 2–4 Vitest suite remains green; `tsc` clean.

## Concerns

1. **`npm test` / `npx tsc --noEmit` not executed** — shell blocked; parent/controller must verify.
2. **`getSchemaByName` collapses API errors into null** — if `getAllSchemas` fails (401, network), Accounts page shows the missing-schema message rather than an auth error. Brief only specifies null → missing-schema copy; acceptable but slightly ambiguous UX.
3. **`formatFetchError` duplicated** in both pages — brief did not require a shared helper; kept inline to minimize scope.

## Manual smoke (when dev server + API available)

1. Sign in → `/loyalty/campaigns` shows list or empty/error state.
2. `/loyalty/accounts` shows table, missing-schema message, or empty/error state.
3. No New campaign / builder links on either page.
