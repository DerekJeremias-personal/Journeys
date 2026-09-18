# Task 5 report: New Journey Builder + AgentChat rail

## Status

**DONE_WITH_CONCERNS**

The focused compiling stub at `/loyalty/campaigns/new` was **replaced** with the remapped EXP progressive Journey Builder tree (welcome canvas, stations, review modals, health sidebar, map canvas) plus an unlabeled `AgentChat` rail.

## What I implemented

- Copied EXP `campaign-journey-builder/**`, remapped `@exp/shared-types` → `@/lib/campaign-types`, and wired wizard contracts from Task 6.
- Kept unlabeled `AgentChat` + `onDiscoveredCampaignId`. `CampaignJsonDisclosure` stays inside chat when linked; no `CampaignTitleWithJsonInspector`; no second inspector dialog.
- Renamed Coach identifiers/copy to Agent (`useAgentCampaignSync`, conflict keep-local / apply-Agent). Omitted Coach-only collapsed `CampaignAgentClient` sheet.
- Save/create-empty use `updateCampaign` (+ `validateCampaign` on save). No EXP create composite, Prisma, or `bffFetch`.
- Criteria station uses Task 6 `LoyaltyRuleBuilder` (not CDP `UniversalRuleBuilder`).
- Did not add `immer` / `use-immer` (copied files do not import them). Did not replace `/loyalty/campaigns/[id]` wizard edit.

## TDD Evidence

**RED** (`npm test -- src/components/loyalty/campaign-journey-builder/agent-hydrate.test.ts` against the stub store):

```
FAIL  store hydrate from snapshot > does not call hydrateFromCampaign when dirty
TypeError: useJourneyBuilderStore.getState(...).actions.reset is not a function
Tests  1 failed | 2 passed (3)
```

**GREEN** (same file after the EXP store + `applyDiscoveredCampaignToBuilder`):

```
✓ builder hydrate (2)
✓ store hydrate from snapshot > does not call hydrateFromCampaign when dirty
```

Existing regression kept: dirty → `conflict` (hydrate not called); clean → `apply`.

## What I tested

- `npm test`: PASS — 29 files, 161 tests.
- `npx tsc --noEmit`: only allowed pre-existing errors:
  - `src/auth.ts(47,40)`
  - `src/lib/loyalty-model.test.ts(40,12)`
- `docs-impact.ps1`: PASS
- `graph-impact.ps1`: PASS (`campaigns`, `campaign-agent`, waiver)
- Browser (`http://localhost:3000/loyalty/campaigns/new`, already signed in):
  - Title “New campaign”, welcome canvas Agent copy, Hide Agent removes the rail, Show Agent restores it
  - Start from template opens the picker (Earning / Promotion / … + Use this template)
  - Did not click Start blank (would POST) or complete a live Agent hydrate turn

## Files changed (paths + purpose)

| Path | Purpose |
|------|---------|
| `src/app/loyalty/campaigns/new/page.tsx` | Unchanged title + shell (stub canvas removed via shell swap) |
| `src/components/loyalty/campaign-journey-builder/**` | Full EXP builder tree, remapped; Coach chrome stripped |
| `src/components/loyalty/campaign-journey-builder/use-agent-campaign-sync.ts` | Unlabeled sync; dirty snapshot does not hydrate |
| `src/components/loyalty/campaign-journey-builder/agent-hydrate.test.ts` | Policy + store-level dirty snapshot regression |
| `src/components/loyalty/campaign-journey-builder/use-save-campaign-draft.ts` | `validateCampaign` then `updateCampaign` |
| `src/components/loyalty/campaign-journey-builder/use-create-empty-draft.ts` | Empty draft via `updateCampaign` (no `createCampaign`); payload includes ISO `startDate` |
| `src/components/loyalty/campaign-journey-builder/use-create-empty-draft.test.ts` | Asserts empty-draft payload includes ISO `startDate` and omits `id` |
| `src/lib/campaign-agent/campaign-mode.ts` | Draft-only writable helper (EXP `campaign-mode` compile dep) |
| `docs/developer/journeys-ux.md` | `/new` is progressive builder + Agent rail |
| `docs/product/graph/waivers/2026-09-18-campaigns-new-builder.md` | No new capability |

Deleted from the lift: `coach-collapsed-rail.tsx`, `use-coach-campaign-sync.ts`, EXP tests that need `@testing-library/react` or Hayward `CampaignSchema` fixtures.

## Self-review

- HTTP-only; no Core/DAL/Infra references; no promotions marketing pages.
- Hydrate never silent-overwrites dirty local work.
- Wizard edit route untouched.
- React Flow remains on wizard canvas components; builder map is the EXP SVG/map canvas.

## Concerns

- ~~**Start blank** POSTs `updateCampaign` with `{ name, status: "draft" }` and no `id`. If the API requires `startDate` or other scalars, empty-draft create will toast-fail until those fields are filled in Setup.~~ **Fixed:** empty-draft payload now includes ISO-8601 UTC `startDate`.
- **Live Agent hydrate** (discovered id → GET Draft → canvas) was not exercised against a real SSE turn in this pass.
- **focusEdit** no longer uses a collapsed Agent sheet; Hide/Show Agent is the only rail chrome.
- Criteria/eligibility editors are loyalty `LoyaltyRuleBuilder`, not EXP CDP profile/event sections.

## Follow-up: empty-draft `startDate`

`CampaignShellValidator.ValidateRequiredFields` rejects missing `startDate`. `buildEmptyDraftPayload()` now POSTs `{ name: "Untitled campaign", status: "draft", startDate: new Date().toISOString() }` with no `id`.

**TDD**

RED (`npm test -- src/components/loyalty/campaign-journey-builder/use-create-empty-draft.test.ts`):

```
FAIL  buildEmptyDraftPayload > includes startDate so CampaignShellValidator accepts the draft
TypeError: (0 , buildEmptyDraftPayload) is not a function
Tests  1 failed (1)
```

GREEN (same command after adding `startDate`):

```
✓ src/components/loyalty/campaign-journey-builder/use-create-empty-draft.test.ts (1 test)
```

**Full suite:** `npm test` PASS — 30 files, 162 tests.

## Follow-up: Important review (form reset + linked GET skip)

1. **Apply Agent no longer leaves Setup on rejected local values.** `hydrateFromCampaign` increments `hydrateGeneration`. `JourneyBuilderFormProvider` resets RHF on that generation, not only `id`/`etag`, so a same-id same-etag Agent snapshot replaces the form before `form.watch` can patch stale scalars back.

2. **Start blank / first save no longer auto-GET their own draft.** `shouldAutoFetchLinkedCampaign` is false when `linkedCampaignId` is already the campaign in the store. The shell effect uses that guard; discover and Link draft still call `hydrateFromCampaignId` explicitly.

**TDD**

RED (`npm test -- src/components/loyalty/campaign-journey-builder/agent-hydrate.test.ts`):

```
FAIL  increments hydrateGeneration when applying a same-id same-etag snapshot
      actual value must be number or bigint, received "undefined"
FAIL  shouldAutoFetchLinkedCampaign (3 tests) — is not a function
Tests  4 failed | 3 passed (7)
```

GREEN (same command after store generation + skip helper):

```
✓ agent-hydrate.test.ts (7 tests)
✓ journey-builder-store.test.ts (12 tests)
Tests  19 passed (19)
```

**Full suite:** `npm test` PASS — 30 files, 166 tests.

## Git

No commits, pushes, merges, or PR changes were made.
