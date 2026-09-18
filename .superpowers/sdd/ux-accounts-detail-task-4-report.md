# Task 4 report: Deposit / spend / expire

**Status:** DONE_WITH_CONCERNS
**Commits:** none (working tree left dirty, as instructed)
**Work dir:** `C:\Dev\Journeys\Journeys\Journeys.UX`

---

## 1. What I implemented

### Server actions (`src/services/loyalty/actions.ts`)

`PointMutationInput` / `ExpirePointsInput` types plus three writes, all of which carry `audit` (the
reads above them still do not):

| Action | Path | Audit `actionType` / `action` |
|--------|------|-------------------------------|
| `depositPoints` | `POST accounts/session/points/deposit` | `Point Adjustment` / `Deposit` |
| `withdrawPoints` | `POST accounts/session/points/withdrawal` | `Point Adjustment` / `Spend` |
| `expirePoints` | `POST accounts/session/points/withdrawal` | `Point Adjustment` / `Expire` |

- `requireUserId()` reads `getJourneysSession()` (no `@/auth` import). Null → returns
  `{ success: false, error: "Session user id is required", timestamp }` **before** any fetch.
- Deposit body: `loyaltyAccountId`, `pointAccountTypeId`, `amount`, `eventId` (GUID), `eventType:
  "admin"`, `depositDate`, `userId`. No `status`, **no `expirationDate`**.
- Spend/expire body: same fields but `withdrawalDate` and `status: "shipped"`.
- Spend and expire share one private `postWithdrawal(input, amount, action)`; only the resolved
  amount and the audit `action` differ.
- Expire resolves the amount through `resolveExpireAmount` and converts a thrown percent-without-
  balance error into a failed `ApiResponse` (still no fetch).

### Files created

| File | Notes |
|------|-------|
| `src/services/loyalty/point-expire-amount.ts` + `.test.ts` | `resolveExpireAmount`; percent math lives here only |
| `src/components/loyalty/points/point-account-manage-modal.tsx` | Deposit / Spend / Expire tabs; also exports `ManageablePointAccount` + `isManageablePointAccount` (see §4a) |
| `src/components/loyalty/points/point-account-panels/deposit-panel.tsx` | Amount + comment + confirm only |
| `src/components/loyalty/points/point-account-panels/spend-panel.tsx` | Client guard: cannot spend more than `currentBalance` |
| `src/components/loyalty/points/point-account-panels/expire-panel.tsx` | Absolute or percent; `superRefine` uses Zod 4 `code: "custom"` (same idiom as `outcome-editor-modal.tsx`) |
| `src/components/loyalty/points/point-account-panels/confirm-point-adjustment.ts` | Exactly `` `${action} ${amount}? This will update the member balance.` `` |
| `src/components/loyalty/points/point-account-panels/invalidate-point-queries.ts` | Shared invalidation (see §4b) |

### Files modified

- `src/components/loyalty/points/points-accounts-card.tsx` — `loyaltyAccountId` + `onUpdated` props
  restored, Manage button (Settings2) shown, modal wired.
- `src/components/loyalty/points/index.ts` — modal + three panels exported.
- `src/components/loyalty/accounts/loyalty-account-detail-client.tsx` — passes `loyaltyAccountId`
  to the card (one line; see §4c for why `onUpdated` is not passed).

All panels are `useSession`-free; no `adminUserId`, audit JSON, or header name crosses the client
boundary. No `PermissionGuard`. No `/points/expire`.

---

## 2. What I tested and results

- **`npm test`** → **42 files, 210 tests, all passing** (Task 3 baseline was 41/207; +1 file, +3
  tests from `point-expire-amount`).
- **`npx tsc --noEmit`** → exit 2 with exactly the three pre-existing errors (`src/auth.ts`,
  `campaigns-client.tsx`, `loyalty-model.test.ts`). **No new errors in Task 4 files.**
- **`npm run build`** → `✓ Compiled successfully in 5.1s`, then stopped on the same three
  pre-existing type errors. I ran this specifically to confirm the Turbopack server-actions
  transform accepts `export type` in the `"use server"` module (it does — type exports are erased
  before that check). Nothing else was learned from it and nothing was fixed for it.
- **Lint (IDE diagnostics)** on every new/changed path: clean.
- **Greps** over `src/components/loyalty/points`: zero matches for `expirationDate`, `useSession`,
  `adminUserId`, `X-ELP-Audit`, `points/expire`, `PermissionGuard`, `@exp/`, `points.byAccount`,
  `journey.preview`, `adminAudit`, `dashboard.data`. Confirm copy verified as a single exact match.

---

## 3. TDD evidence — `resolveExpireAmount`

**RED** (`npm test -- src/services/loyalty/point-expire-amount.test.ts`):

```
 FAIL  src/services/loyalty/point-expire-amount.test.ts
Error: Cannot find module './point-expire-amount' imported from
'C:/Dev/Journeys/Journeys/Journeys.UX/src/services/loyalty/point-expire-amount.test.ts'
 Test Files  1 failed (1)
      Tests  no tests
```

**GREEN** (after creating `point-expire-amount.ts`):

```
 ✓ src/services/loyalty/point-expire-amount.test.ts (3 tests) 2ms
 Test Files  1 passed (1)
      Tests  3 passed (3)
```

The three cases are the plan's exact cases: absolute `5` → `5`; `50%` of `200` → `100`; percent
without `currentBalance` throws `/currentBalance/`.

---

## 4. Deviations from the brief (all deliberate)

### 4a. The modal takes a narrowed point-account type

`AccountPointBalance.pointAccountTypeId` is `string | undefined` in Journeys (EXP's `PointAccountType`
was required). Rather than coerce a missing id to `""` and POST it, the modal accepts
`ManageablePointAccount = AccountPointBalance & { pointAccountTypeId: string }`, and the card
narrows with the `isManageablePointAccount` predicate — a row with no point account type id simply
shows no Manage button. Same approach as the `NamedSchema` predicate in Task 3.

### 4b. One extra file: `invalidate-point-queries.ts`

EXP repeats the same eight-line invalidation block in all three panels. Since four of EXP's six keys
do not exist in Journeys, every block had to be rewritten anyway, so I extracted the surviving three
(`points.balancesByAccount`, `points.ledgersByAccount`, the eventable predicate) into one helper.
This is one file beyond the brief's create list; it is trivially inlinable if a reviewer prefers the
EXP shape.

### 4c. The detail client does not pass `onUpdated`

The prop is restored on the card (the brief asked for it) and the modal still closes itself on
success, but I did not wire a `balancesQuery.refetch()` from the detail client: the panels already
invalidate `loyaltyKeys.points.balancesByAccount(loyaltyAccountId)`, which is the exact key that
query uses, so passing `onUpdated` would have fired a second, redundant request per adjustment. The
prop stays available for any future caller that needs it.

### 4d. Deposit comment helper text

EXP's deposit form says "Recorded in the admin audit log." I shortened it to "Recorded with the
adjustment." — Journeys UX copy does not otherwise name an audit log surface, and the brief asks
not to discuss the audit header in user-facing text.

---

## 5. Self-review findings (fixed before reporting)

- **Removed the redundant `onUpdated` refetch** (§4c) after tracing that the invalidation key and
  the detail client's query key are identical.
- **Replaced the `""` fallback for a missing point account type id** with the type predicate in
  §4a; the first pass would have silently POSTed an empty PAT id.
- **Collapsed spend and expire onto one private `postWithdrawal`** rather than two near-identical
  bodies, so the `status`/`withdrawalDate`/`eventType` contract has one definition.
- **Moved the percent guard to the action.** The expire panel does not pre-check
  percent-without-balance; `expirePoints` returns the failure without fetching, and the panel
  surfaces it as a toast. That keeps the rule in one place.
- **Checked the Zod 4 idiom against the repo** before writing `superRefine` — `outcome-editor-modal.tsx`
  already uses `ctx.addIssue({ code: "custom", ... })`, so no `z.ZodIssueCode` import and no zod
  downgrade.

---

## 6. Concerns

1. **Three pre-existing tsc errors remain** (`src/auth.ts`, `campaigns-client.tsx`,
   `loyalty-model.test.ts`), untouched per the brief. This is the only reason the status is
   DONE_WITH_CONCERNS rather than DONE.
2. **No component-level tests for the panels or the modal.** Vitest still runs with
   `environment: "node"` and the project has no jsdom / Testing Library, so the confirm copy, the
   "no `expirationDate`" rule, and the invalidation keys are verified by reading and grep, not by a
   test. A source-guard test file (Task 3 precedent) would cover the first two cheaply if you want
   one; I left it out to stay inside the brief's file list.
3. **No live payload seen for `points/deposit` or `points/withdrawal`.** The bodies follow the spec
   §5 DTO list (`PointDespositRequest` / `PointWithdrawlRequest`), but nothing here has been
   exercised against a running Journeys.API. The browser pass is Task 6.
4. **`status: "shipped"` is carried over from EXP** with no Journeys-side documentation of what the
   value means for a withdrawal. Spec §5 prescribes it, so I kept it, but it is worth a second pair
   of eyes.
5. **Docs and graph impact not run** — Task 6 per the plan.

**Report path:** `C:\Dev\Journeys\.superpowers\sdd\ux-accounts-detail-task-4-report.md`
