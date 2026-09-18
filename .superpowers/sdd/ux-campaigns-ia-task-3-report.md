# Task 3 Report: Campaign List + Kebab

## Status

Implemented Task 3 in `Journeys/Journeys.UX` without committing.

## Delivered

- Added the brief's kebab tests verbatim.
- Added pure status normalization, visibility, and lifecycle-save mappings.
- Lifted and remapped campaign cards, filters, query keys, and the React Query list client.
- Added a local campaign type alias and local status badge supporting `live`, `draft`, `pause`, and `archive`.
- Replaced the plain server-rendered campaign list with `CampaignsClient`.
- Preserved the `/loyalty/campaigns/agent` link.
- Added New and Archived links; later task routes may return 404 as allowed by the plan.

## Lifecycle remap

- `active` normalizes to `live`; `archived` normalizes to `archive`.
- Publish opens an AlertDialog titled `Publish to Live?` and saves `live`.
- Unpublish saves `pause`, never `draft`.
- Archive saves `archive`.
- Agent is Live-only and labeled `Open in Agent`.
- Delete is Draft-only and requires confirmation.
- Duplicate calls `copyCampaign(id, status)` with the current normalized partition status.
- Restore calls the dedicated `restoreCampaign(id)` endpoint if an Archive card appears.
- Versions is hidden when `extCampaignId` is absent.

## Error handling and exclusions

- A failed getall/getmany query renders a destructive alert and cannot fall through to the empty-list state.
- No EXP imports, Coach copy, promotions CTA, or client-side strip-and-save copy composite were added.
- No component-test dependencies were added; the required kebab coverage runs in the node environment.

## TDD evidence

1. RED: `npm test -- src/lib/campaign-kebab.test.ts` exited 1 because `./campaign-kebab` did not exist.
2. GREEN: the same command passed 7/7 tests after the minimal helper implementation.
3. Full suite: `npm test` passed 18 files and 82/82 tests.

## Verification

- Editor diagnostics for all Task 3 files: clean.
- `git diff --check`: clean.
- `docs-impact.ps1`: passed for all 10 Task 3 paths.
- `graph-impact.ps1`: failed because `campaigns` and `campaign-agent` require a product-doc update or waiver. The approved plan assigns that update to Task 8, so Task 3 did not modify product canon early.
- `npx tsc --noEmit`: blocked by three pre-existing errors outside Task 3 (`src/auth.ts:47`, `src/lib/campaign-agent/stream-route.ts:56`, `src/lib/loyalty-model.test.ts:40`). No Task 3 type errors were reported.
- Required `aidlc-agent-verify-sensor.ps1 -RunTests`: stopped at the same graph-impact requirement before its test stage.

## Files

- `src/lib/campaign-kebab.ts`
- `src/lib/campaign-kebab.test.ts`
- `src/lib/campaign-types.ts`
- `src/components/loyalty/status-badge.tsx`
- `src/components/loyalty/campaigns/campaign-card.tsx`
- `src/components/loyalty/campaigns/campaign-filters.tsx`
- `src/components/loyalty/campaigns/campaigns-client.tsx`
- `src/components/loyalty/campaigns/index.ts`
- `src/services/loyalty/query-keys.ts`
- `src/app/loyalty/campaigns/page.tsx`

## Commits

None.
