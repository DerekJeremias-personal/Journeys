# Task 7 report: Archive + versions

## Status

**DONE_WITH_CONCERNS**

## What I implemented

- TDD helper `restoreArchived` with the brief signature: calls `restoreCampaign(id)` and never `updateCampaign`. Archived restore uses this helper (POST restore), not `updateCampaign({ status: "draft" })`.
- Lifted EXP archived + versions clients into Journeys.UX: remapped `@exp/shared-types` → `@/lib/campaign-types`, stripped PermissionGuard / PageWrapper / Coach / promotions CTA / `@exp/*`.
- Added `/loyalty/campaigns/archived` and `/loyalty/campaigns/versions/[extCampaignId]`. List **Archived** button and kebab **Versions** were already wired and were not changed.
- Toast on restore API errors. Load failures use the existing destructive Alert pattern.

## TDD Evidence

**RED** (`npm test -- src/lib/campaign-restore-action.test.ts`):

```
FAIL  src/lib/campaign-restore-action.test.ts
Error: Cannot find module './campaign-restore-action'
Test Files  1 failed (1)
Tests  no tests
```

**GREEN** (same command after implementing the helper):

```
✓ src/lib/campaign-restore-action.test.ts (1 test)
Test Files  1 passed (1)
Tests  1 passed (1)
```

Case: `restoreArchived({ id: "a1", restoreCampaign, updateCampaign })` → `restoreCampaign` called with `"a1"`; `updateCampaign` not called.

## What I tested

- `npm test`: PASS — 31 files, 167 tests (includes the restore helper).
- `npx tsc --noEmit`: only the allowed pre-existing errors remain:
  - `src/auth.ts(47,40)`
  - `src/lib/loyalty-model.test.ts(40,12)`
- `docs-impact.ps1`: PASS (with `-Files`; git root is `C:\Dev\Journeys`)
- `graph-impact.ps1`: PASS (`campaigns`, `campaign-agent`, waiver)
- Browser (signed-in localhost:3000):
  - List still has **Archived** → `/loyalty/campaigns/archived`
  - Archived page title + empty state: “No archived campaigns found.”
  - Kebab **Versions** on `testtenant1 tier system` → `/loyalty/campaigns/versions/tiersystem`
  - Versions page loads; API returned `No campaign versions found for ExtCampaignId 'tiersystem'.` (surfaced as query error, not empty-list)

Did not click Restore: archived GET returned no rows.

## Files changed (paths + purpose)

| Path | Purpose |
|------|---------|
| `src/lib/campaign-restore-action.ts` | Restore helper: POST restore, never save-as-draft |
| `src/lib/campaign-restore-action.test.ts` | Failing-then-passing restore seam test |
| `src/components/loyalty/campaigns/archived-campaigns-client.tsx` | Archived list + restore via helper |
| `src/components/loyalty/campaigns/campaign-versions-client.tsx` | Version history list |
| `src/app/loyalty/campaigns/archived/page.tsx` | Archived route (thin title, no PageWrapper) |
| `src/app/loyalty/campaigns/versions/[extCampaignId]/page.tsx` | Versions route; decodes `extCampaignId` |
| `src/components/loyalty/campaigns/index.ts` | Export the two new clients |
| `docs/developer/journeys-ux.md` | Archived + versions routes |
| `docs/product/graph/waivers/2026-09-18-campaigns-archived-versions.md` | No new capability |

## Self-review

- Restore injects real `restoreCampaign` / `updateCampaign` into `restoreArchived`. `updateCampaign` is wrapped `(c) => updateCampaign(c as Campaign)` only to satisfy TS parameter contravariance against the brief `(c: unknown)` signature. The helper still does not call it.
- `getArchivedCampaigns()` takes no `pageSize` (unlike EXP); query key is `loyaltyKeys.campaigns.archived()`.
- Static `archived` / `versions` segments do not collide with `[id]`.
- List kebab Versions and Archived hrefs left intact.

## Concerns

1. **Restore not clicked in the browser.** Archived list is empty on this tenant, so POST restore was not exercised end-to-end. Covered by the unit test + existing list restore mutation.
2. **Versions GET for `tiersystem` returned an API error** rather than an empty array. UI shows that error (same as EXP `throw` on `!success`). Empty-state copy is unused on this tenant.
3. **Archived kebab Edit is a no-op** (card title still uses `detailHref`). Matches EXP, which only handled restore + versions in that client.

## Git

No commits, pushes, merges, or PR changes were made. Nothing was staged.

---

# Task 7 review-fix

## Status

**DONE**

## What I fixed

Dropped `detailHref` on archived `CampaignCard`s so titles are plain text, not wizard links with `?campaignStatus=archive`. Restore kebab is unchanged. Wizard Live-edit helper was not modified.

**`npm test -- src/lib/campaign-restore-action.test.ts`**
