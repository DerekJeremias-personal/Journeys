# Task 4 brief: Deposit / spend / expire

Plan: `C:\Dev\Journeys\Journeys\docs\plans\2026-09-18-Journeys-ux-accounts-detail.md` (Task 4)
Spec: `C:\Dev\Journeys\Journeys\docs\specs\2026-09-18-Journeys-ux-accounts-detail-design.md` §4 audit + §5 deposit/withdraw bodies

Work directory: `C:\Dev\Journeys\Journeys\Journeys.UX`

**Do not git commit.**

---

### Task 4: Deposit / spend / expire

**Files:**
- Create: `src/components/loyalty/points/point-account-manage-modal.tsx`
- Create: `src/components/loyalty/points/point-account-panels/deposit-panel.tsx`
- Create: `src/components/loyalty/points/point-account-panels/spend-panel.tsx`
- Create: `src/components/loyalty/points/point-account-panels/expire-panel.tsx`
- Create: `src/components/loyalty/points/point-account-panels/confirm-point-adjustment.ts`
- Create: `src/services/loyalty/point-expire-amount.ts` + `.test.ts`
- Modify: `src/components/loyalty/points/points-accounts-card.tsx` — restore Manage
- Modify: `src/components/loyalty/points/index.ts`
- Modify: `src/services/loyalty/actions.ts`
- Modify: `src/components/loyalty/accounts/loyalty-account-detail-client.tsx` only if the card needs extra props passed (`loyaltyAccountId` is already computed there)

**Interfaces:**

```ts
export type PointMutationInput = {
  loyaltyAccountId: string;
  pointAccountTypeId: string;
  amount: number;
  comment: string;
};

export type ExpirePointsInput = PointMutationInput & {
  isPercent?: boolean;
  currentBalance?: number;
};
```

`depositPoints` / `withdrawPoints` / `expirePoints` as in the plan. `getJourneysSession` is **already exported** from `src/lib/journeys-fetch.ts`. Do **not** import `@/auth` from actions.

```ts
import { getJourneysSession } from "@/lib/journeys-fetch";

async function requireUserId(): Promise<string | null> {
  const s = await getJourneysSession();
  return s?.userId ?? null;
}
```

If `requireUserId()` is null, return `{ success: false, error: "Session user id is required", timestamp: new Date().toISOString() }` **without** calling fetch. Include `userId` on the deposit/withdrawal body when present.

Expire uses `resolveExpireAmount` from `point-expire-amount.ts` (do not duplicate the percent math). Same path as withdraw: `POST accounts/${TENANT_SLUG}/points/withdrawal`. Audit `action: "Expire"`. **Do not add `/points/expire` to the allowlist.**

Spend/expire body: `loyaltyAccountId`, `pointAccountTypeId`, `amount`, `eventId` (GUID), `eventType: "admin"`, `withdrawalDate` (now), `status: "shipped"`, `userId`. **No `expirationDate` on deposit.** Deposit body: `depositDate` not `withdrawalDate`, no `status`.

Audit (server action only):

| UX | actionType | action |
|----|------------|--------|
| Deposit | Point Adjustment | Deposit |
| Spend | Point Adjustment | Spend |
| Expire | Point Adjustment | Expire |

`loyaltyMemberId` = loyalty account id. Comment from the form.

- [ ] **Step 1: Write failing tests** `src/services/loyalty/point-expire-amount.test.ts` — exact cases from the plan (absolute, percent of 200 → 100, percent without balance throws `/currentBalance/`). `npm test -- src/services/loyalty/point-expire-amount.test.ts` FAIL.

- [ ] **Step 2: Implement `point-expire-amount.ts`. Tests PASS.**

- [ ] **Step 3: Copy points panels + modal + confirm helper** from `C:\Dev\Journeys\temp\exp\apps\admin-web\src\components\loyalty\points\`.

Rewrite:
- Remove `expirationDate` from deposit schema / form / defaultValues / UI. Amount + comment + confirm only.
- Confirm helper **must** be: `` window.confirm(`${action} ${amount}? This will update the member balance.`) `` — drop EXP “and record the change in the Action Log.”
- **Strip `useSession`.** Do not send `adminUserId` from the client. Panels call `depositPoints` / `withdrawPoints` / `expirePoints` with account id, PAT id, amount, comment (expire also `isPercent` + `currentBalance`).
- Comments: replace `X-ELP-Audit` with `X-Journeys-Audit` only if you mention the header at all. Prefer not discussing session/JWT in comments.
- Invalidate on success: `loyaltyKeys.points.balancesByAccount(loyaltyAccountId)` and `loyaltyKeys.points.ledgersByAccount(loyaltyAccountId)`. Optional eventable predicate (same as EXP). **Do not** reference `loyaltyKeys.points.byAccount`, `loyaltyKeys.journey.preview`, or `loyaltyKeys.adminAudit` — those keys do not exist in Journeys.
- Toast via `react-hot-toast` (already in Providers).
- Types: `AccountPointBalance` / `PointAccountTypeListItem` from `@/lib/api-types`.
- Comment required (zod min 1). Zod is v4 in this app — if EXP `z.ZodIssueCode.custom` fails typecheck, use the Zod 4 equivalent already used in campaign wizard forms. Do not downgrade zod.
- Strip `PermissionGuard`.

- [ ] **Step 4: Wire Manage on `PointsAccountsCard`.** Restore `loyaltyAccountId` and `onUpdated`. Show the Manage button (Settings2). Open the modal. Pass `loyaltyAccountId` from the detail client (it already resolves it). Invalidate balances on success.

- [ ] **Step 5: `npm test` PASS.** `npx tsc --noEmit` — do not fix pre-existing `auth.ts` / `campaigns-client.tsx` / `loyalty-model.test.ts`. New errors in Task 4 files are blockers.

- [ ] **Step 6: Do not commit.**

---

## Binding constraints

- Campaign REST stays unaudited. Only these point writes pass `audit`.
- No free-form headers bag. No browser-supplied audit JSON.
- No `/points/expire` path.
- No named product tenant. No auth/OIDC/JWT discussion in new copy.
- Do not log secrets, payloads, or raw audit JSON.
- Do not touch campaigns IA files except if you must import an existing UI primitive.
- Existing `getJourneysSession` already exists — reuse it.

## TDD

RED then GREEN for `point-expire-amount`. Capture output in the report.

Optional extra unit test for expire/spend mapping is welcome but not required if it would need to mock fetch heavily; the allowlist already rejects `points/expire`.

## Out of scope

- Journey enter/exit / MoveTier (Task 5)
- Docs/graph/browser (Task 6)
- Builder / Data Explorer
- Commits
