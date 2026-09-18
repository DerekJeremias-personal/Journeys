# Task 5 brief: Assign / remove journey + manage tier

Plan: `C:\Dev\Journeys\Journeys\docs\plans\2026-09-18-Journeys-ux-accounts-detail.md` (Task 5)
Spec: `C:\Dev\Journeys\Journeys\docs\specs\2026-09-18-Journeys-ux-accounts-detail-design.md` §4 audit table, MoveTier = Preview then MoveTier only (no enter/exit fallback)

Work directory: `C:\Dev\Journeys\Journeys\Journeys.UX`

**Do not git commit.**

---

### Task 5

**Files:**
- Create: `src/components/loyalty/accounts/account-actions-dropdown.tsx`
- Create: `src/components/loyalty/accounts/account-journey-modal.tsx`
- Create: `src/components/loyalty/accounts/manage-tier-modal.tsx`
- Modify: `src/app/loyalty/accounts/[id]/page.tsx` — Actions in header, pass identifiers
- Modify: `src/services/loyalty/actions.ts` — enter, exit, preview, move
- Modify: `src/components/loyalty/accounts/index.ts` — export the new components

**Interfaces** (use existing `TENANT_SLUG` `"session"` and existing `requireUserId` / `missingUserId` helpers in `actions.ts` from Task 4):

```ts
export async function enterJourney(params: {
  campaignId: string;
  journeyId: string;
  loyaltyAccountId: string;
  loyaltyAccountXReference: string;
  comment: string;
}) {
  return journeysFetch(
    `journey/${TENANT_SLUG}/ManuallyEnter/${params.campaignId}/Journey/${params.journeyId}/ForAccount/${params.loyaltyAccountXReference}`,
    {
      method: "GET",
      audit: {
        loyaltyMemberId: params.loyaltyAccountId,
        actionType: "Journey Movement",
        action: "Add",
        comment: params.comment
      }
    }
  );
}
```

Same path shape for `ManuallyExit` / audit action `"Remove"`.

`previewTierMove` — POST `journey/${TENANT_SLUG}/PreviewTierMove`, body `MoveTierRequest` fields. **No `audit` on preview.** If `requireUserId()` is null, return the same missing-user error **without fetch**. Do **not** send `adminUserId: ""` (spec: never send empty adminUserId).

`moveTier` — POST `MoveTier` with `audit` action `"Move Tier"`, actionType `"Journey Movement"`. Fail before fetch if no user id. Include `adminUserId` from `requireUserId()` on the body.

**Do not** implement `moveTierViaJourneyFallback`. On MoveTier failure, surface the API error. Grep must find zero `moveTierViaJourneyFallback`.

- [ ] **Step 1: Copy the three EXP components** from `C:\Dev\Journeys\temp\exp\apps\admin-web\src\components\loyalty\accounts\` (`account-actions-dropdown.tsx`, `account-journey-modal.tsx`, `manage-tier-modal.tsx`).

Rewrite:
- Strip `PermissionGuard` (always show Actions).
- Strip `useSession` / client `adminUserId`. Modals call the server actions with ids + comment only.
- Tier comment min 3 (keep EXP zod). Journey comments as EXP (likely min 1).
- Preview then commit for tier. Do not add enter/exit fallback on MoveTier failure.
- Invalidate keys that **exist**: `loyaltyKeys.accounts.detail`, `detailByExt`, `loyaltyDetail`, `campaigns.list`, `points.balancesByAccount`, `points.ledgersByAccount`. Do not invent `loyaltyKeys.journey.preview` or `adminAudit`.
- Types from `@/lib/api-types` / local types. No `@exp/*`.
- Replace X-ELP-Audit comments; do not discuss JWT/session in comments.
- DropdownMenu already exists at `@/components/ui/dropdown-menu`.

- [ ] **Step 2: Put Actions in the detail page header** (flex row with the heading), passing `loyaltyAccountId` + `loyaltyAccountXReference` + `detailAccountId={id}` from the existing `resolveAccountIdentifiers` in `[id]/page.tsx`.

Current page already resolves identifiers. Add a header row:

```tsx
<div className="flex items-start justify-between gap-4">
  <div>
    <Link ...>← Accounts</Link>
    <h1>Loyalty account</h1>
    <p>{accountLabel}</p>
  </div>
  <AccountActionsDropdown
    loyaltyAccountId={identifiers.loyaltyAccountId}
    loyaltyAccountXReference={identifiers.loyaltyAccountXReference}
    detailAccountId={id}
  />
</div>
```

`AccountActionsDropdown` is a client component; the page is a server component — that's fine.

- [ ] **Step 3: `npm test` PASS. `npx tsc --noEmit`** — do not fix pre-existing `auth.ts` / `campaigns-client.tsx` / `loyalty-model.test.ts`. Grep `actions.ts` and the new modals for `moveTierViaJourneyFallback` — zero. Grep new files for `data-explorer` / `/builder` — zero.

- [ ] **Step 4: Do not commit.**

---

## Binding

- Enter/exit GET **with** `audit` (journeysFetch already attaches the header whenever `audit` is set).
- Preview POST **without** `audit`.
- MoveTier POST **with** `audit`.
- Campaign REST stays unaudited.
- No named product tenant. No auth commentary.
- Do not log raw audit JSON.
- Do not touch unrelated campaigns IA files.

## Out of scope

- Docs/graph/browser (Task 6)
- Builder
- Commits
