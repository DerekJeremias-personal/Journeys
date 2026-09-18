# Task 5 report: Assign / remove journey + manage tier

- **Status:** DONE
- **Commits:** none (working tree left dirty as instructed)

## What shipped

- `actions.ts`: `enterJourney` / `exitJourney` (GET `ManuallyEnter` / `ManuallyExit`, audit `Journey Movement` + `Add` / `Remove`), `previewTierMove` (POST, **no** audit, fails before fetch without a session user id), `moveTier` (POST, audit `Move Tier`, `adminUserId` on the body). No fallback; MoveTier errors surface as-is.
- New `account-actions-dropdown.tsx` (always shown, owns post-write invalidation), `account-journey-modal.tsx`, `manage-tier-modal.tsx` (preview then commit, tier comment min 3).
- `[id]/page.tsx` header is now a flex row with Actions, fed by the existing `resolveAccountIdentifiers`; `index.ts` exports the three components.

## Tests

- `npm test`: 43 files / 214 tests PASS (includes new `account-actions.source.test.ts`, 4 tests).
- `npx tsc --noEmit`: only the three pre-existing errors (`auth.ts`, `campaigns-client.tsx`, `loyalty-model.test.ts`).
- Greps over the new files + `actions.ts`: zero `moveTierViaJourneyFallback`, `data-explorer`, `/builder`, `PermissionGuard`, `useSession`, `X-ELP-Audit`, `adminUserId: ""`.

## Concerns

- EXP picked tier campaigns from a hard-coded named-tenant ext-id set. Replaced with a neutral rule: non-draft campaigns whose name mentions a tier, falling back to all non-draft campaigns. Worth a browser check in Task 6 that the right campaign is offered.
- EXP's "preview unavailable" alert promised a journey-only fallback. Reworded: submit stays enabled on that one config-gap error and the API result is reported, since there is no fallback.
- Invalidation lives in the dropdown (one place for both modals) instead of duplicated per modal; no new helper file.

Report path: `C:\Dev\Journeys\.superpowers\sdd\ux-accounts-detail-task-5-report.md`

## Fix pass

- **Status:** DONE; no commit created.
- Extracted `selectTierCampaigns` and restricted candidates to campaigns with a non-empty id and case-insensitive `Live` status. The existing tier-name preference and all-Live fallback remain.
- Added focused tests for tier-name preference, exclusion of Pause/Draft/empty-id campaigns, and fallback to all Live campaigns.
- `npm test -- src/components/loyalty/accounts/select-tier-campaigns.test.ts`: PASS (1 file, 3 tests).
- `npm test`: PASS (44 files, 217 tests).
- `npx tsc --noEmit`: only the three acknowledged pre-existing errors in `src/auth.ts`, `campaigns-client.tsx`, and `src/lib/loyalty-model.test.ts`; no errors in touched files.
- `docs-impact.ps1`: PASS. `graph-impact.ps1`: PASS using the branch's existing `docs/product/ontology/draft-live.md` product update.
- Files changed in this fix: `manage-tier-modal.tsx`, `select-tier-campaigns.ts`, `select-tier-campaigns.test.ts`, and this report.
