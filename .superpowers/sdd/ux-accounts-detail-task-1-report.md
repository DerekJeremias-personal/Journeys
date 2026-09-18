# Task 1 Report: Allowlist + audit + queryData params

## Status

**DONE_WITH_CONCERNS** — All task-scoped tests pass (183/183). `npx tsc --noEmit` reports 3 pre-existing errors in unrelated files (`auth.ts`, `campaigns-client.tsx`, `loyalty-model.test.ts`); none in Task 1 files.

## Summary

Extended the loyalty HTTP client foundation for accounts detail work:

- Account and Journey path allowlists in `mapLoyaltyPath`
- `AdminAuditHeader` / `X-Journeys-Audit` on `journeysFetch` (POST only when `audit` option set)
- `extractContinuationToken`, `QueryDataParams`, and expanded `queryData` action
- Exported `getJourneysSession` for later actions
- Stubbed `accounts/page.tsx` to a thin host pending Task 2

## TDD Evidence

### RED — Step 2: allowlist tests (before implementation)

```
npm test -- src/lib/map-loyalty-path.test.ts
```

```
 ❯ src/lib/map-loyalty-path.test.ts (14 tests | 3 failed)
   × mapLoyaltyPath > maps account get, ext, balances, ledgers, deposit, withdrawal
     → not-allowlisted: accounts/session/acc-1
   × mapLoyaltyPath > does not treat points/deposit as an account id
     → not-allowlisted: accounts/session/points/deposit
   × mapLoyaltyPath > maps journey enter, exit, preview, move
     → not-allowlisted: journey/session/ManuallyEnter/camp-1/Journey/node-1/ForAccount/xref-1
 Test Files  1 failed (1)
      Tests  3 failed | 11 passed (14)
```

### RED — Step 6: audit + continuation tests (before implementation)

```
npm test -- src/lib/journeys-fetch.test.ts src/services/loyalty/parse-list.test.ts
```

```
 ❯ src/services/loyalty/parse-list.test.ts (14 tests | 3 failed)
   × extractContinuationToken > reads continuationToken
     → (0 , extractContinuationToken) is not a function
 ❯ src/lib/journeys-fetch.test.ts (8 tests | 2 failed)
   × journeysFetch > attaches X-Journeys-Audit on audit option and uses session user id
     → undefined is not iterable (fetch not called — path not allowlisted)
   × journeysFetch > does not attach X-Journeys-Audit on GET balances
     → undefined is not iterable (fetch not called — path not allowlisted)
 Test Files  2 failed (2)
      Tests  5 failed | 17 passed (22)
```

### GREEN — Step 4 + Step 8: full suite after implementation

```
npm test
```

```
 Test Files  34 passed (34)
      Tests  183 passed (183)
   Duration  2.35s
```

```
npx tsc --noEmit
```

Pre-existing failures (not introduced by Task 1):

```
src/auth.ts(47,40): error TS2345
src/components/loyalty/campaigns/campaigns-client.tsx(124,9): error TS2322
src/lib/loyalty-model.test.ts(40,12): error TS18048
```

No errors in any Task 1 file.

## Files Changed

| File | Change |
|------|--------|
| `src/lib/map-loyalty-path.ts` | Account/Journey allowlist maps before final throw |
| `src/lib/map-loyalty-path.test.ts` | 4 new test cases (account, deposit guard, journey, rejections) |
| `src/lib/journeys-fetch.ts` | `AdminAuditHeader`, `audit` option, `getJourneysSession` export |
| `src/lib/journeys-fetch.test.ts` | 2 audit header tests |
| `src/lib/api-types.ts` | `meta` on `ApiResponse`, `AccountPointBalance`, `QueryDataParams`, `tag` on `SchemaListItem` |
| `src/services/loyalty/parse-list.ts` | `extractContinuationToken`, `tag` in `normalizeSchema` |
| `src/services/loyalty/parse-list.test.ts` | 3 continuation token tests |
| `src/services/loyalty/actions.ts` | `queryData(params: QueryDataParams)` with meta |
| `src/app/loyalty/accounts/page.tsx` | Thin stub host ("Loading accounts…") |

## Implementation Notes

### mapLoyaltyPath

Specific account paths checked before generic account-id capture:

- `points/deposit`, `points/withdrawal` (literal endpoints)
- `points/balances/{id}` (balances)
- `points/{id}` excluding deposit/withdrawal/balances/admin/expire (ledgers)
- `ext/{xref}` (external lookup)
- `{accountId}` excluding points/ext/builder

Journey paths: ManuallyEnter/Exit with full segment capture, PreviewTierMove, MoveTier.

Rejected: `builder`, `points/expire`, `points/admin`.

### journeysFetch audit

- Only `audit?: AdminAuditHeader` added — no free-form `headers` bag
- Header JSON: `{ loyaltyMemberId, adminUserId, actionType, action, comment }`
- `adminUserId` from session; empty string comment when omitted
- Campaign REST actions unchanged (no `audit` passed)

### queryData

- Signature: `queryData<T>(params: QueryDataParams): Promise<ApiResponse<T[]>>`
- Returns `meta.continuationToken` and `meta.pageSize`
- Never sends `modelId` `"unknown"` (existing `isUsableModelId` guard retained)

### accounts/page.tsx

Stubbed per brief because `queryData` signature change would break the old `queryData(schemaName)` call. Task 2 replaces with full list UI.

## Self-Review

| Constraint | Met? |
|------------|------|
| HTTP-only, no Core/DAL/Infra | Yes |
| No `@exp/*` | Yes |
| No free-form `headers` on journeysFetch | Yes |
| Campaign REST stays unaudited | Yes — no `audit` in campaign actions |
| Export `getJourneysSession` | Yes |
| Never `modelId` `"unknown"` | Yes — guard unchanged |
| No product tenant names in new copy | Yes |
| Existing comments preserved | Yes |
| No git commit | Yes |
| TDD RED then GREEN | Yes — evidence above |
| Reuse `const t = "acme"` in tests | Yes |
| No string overload on queryData | Yes |

**Concerns:**

1. `npx tsc --noEmit` fails on 3 pre-existing errors outside Task 1 scope.
2. Accounts page is intentionally a stub until Task 2.

## Commits

None (per instructions).
